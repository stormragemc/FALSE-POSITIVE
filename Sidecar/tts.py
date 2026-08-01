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

from elevenlabs.client import ElevenLabs
from pydub import AudioSegment

import config

_client: ElevenLabs | None = None

# Low-latency model family — a 1-2s difference on the critical turn-latency
# path versus the high-quality multilingual models. Confirm this is still the
# current flash/turbo model id via ElevenLabs' /v1/models endpoint if TTS
# calls start failing with an unknown-model error.
_MODEL_ID = "eleven_flash_v2_5"


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
        )
        mp3_bytes = b"".join(chunks)
        return _decode_mp3_to_pcm16(mp3_bytes)


def synthesize(text: str) -> tuple[bytes, int, int, int]:
    """Returns (pcm16_le_bytes, sample_rate, channels, elapsed_ms)."""
    client = _get_client()
    t0 = time.perf_counter()
    pcm, rate, channels = _fetch_audio(client, text)
    ms = int((time.perf_counter() - t0) * 1000)
    return pcm, rate, channels, ms
