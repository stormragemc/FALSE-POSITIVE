"""Officer speech, using Google Gemini TTS on Vertex by default.

Google uses the existing project/ADC and returns raw PCM16 at 24kHz. The legacy
ElevenLabs voice remains opt-in. app.py isolates every provider failure so a
valid detective reply is still delivered as text.

Primary request/PCM contract:
https://docs.cloud.google.com/text-to-speech/docs/gemini-tts#use-vertex-ai-api
"""

import io
import base64
import time

from google import genai
from google.genai import types

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs
from pydub import AudioSegment

import config

_client: ElevenLabs | None = None
_google_client: genai.Client | None = None

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


def failure_reason(error: Exception) -> str:
    """Bounded diagnostics: provider exception text may contain secrets."""
    cause = error
    for _ in range(3):
        status = getattr(cause, "status_code", None) or getattr(cause, "status", None) or getattr(cause, "code", None)
        if isinstance(status, int) and 100 <= status <= 599:
            return f"provider_http_{status}"
        if isinstance(cause, TimeoutError):
            return "provider_timeout"
        cause = cause.__cause__
        if cause is None:
            break
    return type(error).__name__


def _get_client() -> ElevenLabs:
    global _client
    if _client is None:
        _client = ElevenLabs(
            api_key=config.ELEVENLABS_API_KEY,
            timeout=config.SIDECAR_TTS_TIMEOUT_SECONDS,
        )
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
        status = getattr(e, "status_code", None) or getattr(e, "status", None)
        if _is_voice_permission_error(e) or status in {401, 403, 404, 429}:
            # Fail loud and specific rather than retrying into the same 402
            # via the MP3 fallback below — a permission error isn't a
            # transport error, retrying a different format won't fix it.
            raise RuntimeError(
                f"ElevenLabs rejected voice_id={voice_id!r}: your plan doesn't grant API access to this "
                "voice (this happens even for 'premade' voices you didn't create). Run "
                "`tools/probe_tts.py` to find a voice_id your account can actually use over the API, "
                "then set ELEVENLABS_VOICE_ID in Sidecar/.env to it."
            ) from e

        print(f"[Sidecar] PCM TTS request unavailable ({failure_reason(e)}); trying MP3 decode.")
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


def _get_google_client() -> genai.Client:
    global _google_client
    if _google_client is None:
        _google_client = genai.Client(
            vertexai=True,
            project=config.GCP_PROJECT,
            location=config.GOOGLE_TTS_LOCATION,
            http_options=types.HttpOptions(timeout=int(config.SIDECAR_TTS_TIMEOUT_SECONDS * 1000)),
        )
    return _google_client


def _google_prompt(text: str, mood: str | None) -> str:
    delivery = mood.lower().strip() if mood and mood.lower().strip() in ALLOWED_MOODS else "restrained and watchful"
    return (
        "Speak in English as Officer Spassky, a mature male police investigator. "
        "Use a low register and a clear Russian accent, measured pacing and contained anger. "
        f"The delivery for this line is {delivery}. "
        "Recite only the following dialogue exactly; do not read these directions aloud:\n"
        + text
    )


def _synthesize_google(text: str, mood: str | None) -> tuple[bytes, int, int]:
    response = _get_google_client().models.generate_content(
        model=config.GOOGLE_TTS_MODEL,
        contents=_google_prompt(text, mood),
        config=types.GenerateContentConfig(
            speech_config=types.SpeechConfig(
                language_code="en-US",
                voice_config=types.VoiceConfig(
                    prebuilt_voice_config=types.PrebuiltVoiceConfig(voice_name=config.GOOGLE_TTS_VOICE)
                ),
            ),
        ),
    )
    for candidate in response.candidates or []:
        content = getattr(candidate, "content", None)
        for part in getattr(content, "parts", None) or []:
            inline = getattr(part, "inline_data", None)
            if inline is None or not getattr(inline, "data", None):
                continue
            mime = (getattr(inline, "mime_type", "") or "").lower()
            if mime and not (mime.startswith("audio/l16") or mime.startswith("audio/pcm")):
                raise ValueError("unexpected speech encoding")
            pcm = inline.data
            if isinstance(pcm, str):
                pcm = base64.b64decode(pcm, validate=True)
            if not isinstance(pcm, bytes) or not pcm or len(pcm) % 2 or pcm.startswith(b"RIFF"):
                raise ValueError("invalid speech PCM")
            return pcm, 24000, 1
    raise ValueError("speech provider returned no audio")


def synthesize(text: str, mood: str | None = None) -> tuple[bytes, int, int, int]:
    """Return raw PCM16 bytes, sample rate, channels and elapsed milliseconds.

    A provider is selected explicitly. Failures never switch to another paid
    provider; app.py returns the valid dialogue as text instead.
    """
    t0 = time.perf_counter()
    if not config.TTS_ENABLED or config.TTS_PROVIDER == "none":
        return b"", 0, 0, 0
    if config.TTS_PROVIDER == "google":
        pcm, rate, channels = _synthesize_google(text, mood)
    elif config.TTS_PROVIDER == "elevenlabs":
        pcm, rate, channels = _fetch_audio(_get_client(), _tagged(text, mood))
    else:
        raise ValueError("unsupported speech provider")
    ms = int((time.perf_counter() - t0) * 1000)
    return pcm, rate, channels, ms
