"""The officer's voice: ElevenLabs TTS.

Chosen specifically because it can return raw PCM, which is what keeps an MP3
decoder out of Unity entirely — but PCM output is gated to some account
tiers. We try PCM first and silently fall back to MP3 (decoded here via
pydub/ffmpeg) if the account doesn't have it; either way the result gets
normalized to one canonical format in audio_utils.normalize_to_canonical, so
the tier question is harmless to the rest of the pipeline.
"""

import io
import time

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs
from pydub import AudioSegment

import config

_client: ElevenLabs | None = None

# The multilingual model, chosen over eleven_flash_v2_5 by ear in an A/B at
# matched settings: it is materially the more Russian-sounding of the two, and
# the accent is the casting brief. It costs 950-2100ms per line against flash's
# ~215ms, which is the 1-2s turn-latency difference this comment used to warn
# about — accepted deliberately, see §4.6 of the design spec for what that
# costs a full playthrough. Confirm the id via ElevenLabs' /v1/models endpoint
# if TTS calls start failing with an unknown-model error.
_MODEL_ID = "eleven_v3"

# Spassky's delivery: contained anger, not shouting — slow and held down rather
# than loud. Low stability keeps the emotional variance (and the accent, which
# high stability flattens toward neutral English); similarity_boost at 1.00 is
# the strongest accent carrier available and measured free.
#
# This is the LOW register of the design spec's §4.3 table, applied uniformly.
# Script-driven register selection is specced but not yet implemented; until it
# is, every line is delivered in this one.
# Variant 12, chosen by ear against the previous eleven_multilingual_v2 render.
#
# These are not free parameters. eleven_v3 does NOT carry this voice's Russian
# accent on its own — the accent comes from the ACCENT_TAG below — and style
# above 0 drags the render away from the source voice hard enough to lose it
# entirely. stability 1.0 (Robust) preserves identity best of the three v3
# settings. Changing any of these without listening will produce a British
# Spassky, which is exactly what happened in testing.
_VOICE_SETTINGS = VoiceSettings(
    stability=1.0,
    similarity_boost=1.00,
    style=0.0,
    use_speaker_boost=True,
)

# Prepended to every line server-side rather than left to the model. If the
# model forgets it, Spassky loses his accent mid-interrogation, so it is not
# something a prompt is allowed to be responsible for.
ACCENT_TAG = "strong Russian accent"

# The moods the officer may ask for. Deliberately a closed set: an open
# vocabulary means the model invents directions nobody has heard, and an
# untested tag can drop the accent with no warning in front of a player.
ALLOWED_MOODS = frozenset({"angry", "shouting", "quietly menacing", "tired", "impatient"})


def _tagged(text: str, mood: str | None) -> str:
    """Builds the v3 audio tag. Mood rides inside the same bracket as the
    accent — two separate tags fight each other."""
    if mood and mood.lower().strip() in ALLOWED_MOODS:
        return f"[{ACCENT_TAG}, {mood.lower().strip()}] {text}"
    return f"[{ACCENT_TAG}] {text}"


def _get_client() -> ElevenLabs:
    global _client
    if _client is None:
        _client = ElevenLabs(api_key=config.ELEVENLABS_API_KEY)
    return _client


def _decode_mp3_to_pcm16(mp3_bytes: bytes) -> tuple[bytes, int, int]:
    seg = AudioSegment.from_file(io.BytesIO(mp3_bytes), format="mp3")
    seg = seg.set_channels(1).set_sample_width(2)
    return seg.raw_data, seg.frame_rate, 1


def _is_voice_permission_error(e: Exception) -> bool:
    """True for ElevenLabs' free-tier gate on non-owned voices: a plain
    voices.get_all() listing includes Voice Library / professional voices
    that still 402 on an actual convert() call — see tools/probe_tts.py,
    which is the only reliable way to tell usable voices from merely-visible
    ones. Detected by status code rather than message text, which can change
    wording across API versions."""
    status = getattr(e, "status_code", None) or getattr(e, "status", None)
    return status == 402 or "402" in str(e) or "payment_required" in str(e).lower()


def _fetch_audio(client: ElevenLabs, text: str) -> tuple[bytes, int, int]:
    voice_id = config.ELEVENLABS_VOICE_ID
    try:
        chunks = client.text_to_speech.convert(
            voice_id=voice_id,
            text=text,
            model_id=_MODEL_ID,
            output_format="pcm_24000",
            voice_settings=_VOICE_SETTINGS,
        )
        raw = b"".join(chunks)
        if not raw:
            raise ValueError("empty PCM response")
        return raw, 24000, 1
    except Exception as e:
        if _is_voice_permission_error(e):
            # Fail loud and specific rather than retrying into the same 402
            # via the MP3 fallback below — a permission error isn't a
            # transport error, retrying a different format won't fix it.
            raise RuntimeError(
                f"ElevenLabs rejected voice_id={voice_id!r}: your plan doesn't grant API access to this "
                "voice (this happens even for 'premade' voices you didn't create). Run "
                "`tools/probe_tts.py` to find a voice_id your account can actually use over the API, "
                "then set ELEVENLABS_VOICE_ID in Sidecar/.env to it."
            ) from e

        print(f"[Sidecar] PCM TTS request failed ({e}); falling back to MP3 + decode.")
        chunks = client.text_to_speech.convert(
            voice_id=voice_id,
            text=text,
            model_id=_MODEL_ID,
            output_format="mp3_44100_128",
            # Same settings as the PCM path above: the fallback must not be
            # audibly a different performance from the primary.
            voice_settings=_VOICE_SETTINGS,
        )
        mp3_bytes = b"".join(chunks)
        return _decode_mp3_to_pcm16(mp3_bytes)


def synthesize(text: str, mood: str | None = None) -> tuple[bytes, int, int, int]:
    """Returns (pcm16_le_bytes, sample_rate, channels, elapsed_ms).

    `text` is the spoken line with no tag; the accent tag is applied here so
    every call gets it whether or not the caller remembered."""
    client = _get_client()
    t0 = time.perf_counter()
    pcm, rate, channels = _fetch_audio(client, _tagged(text, mood))
    ms = int((time.perf_counter() - t0) * 1000)
    return pcm, rate, channels, ms
