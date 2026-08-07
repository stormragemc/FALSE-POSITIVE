"""Speech-to-text via Google Cloud Speech-to-Text v2.

Authenticates as the runtime service account through Application Default
Credentials — there is no API key for this vendor. Locally, run
`gcloud auth application-default login` once.

Audio arrives as the same LINEAR16 16kHz mono bytes Unity uploads, so no
decode or resample happens here; app.py has already normalized the buffer.
"""

import time

from google.cloud import speech_v2
from google.cloud.speech_v2.types import cloud_speech

import config

# Utterances are bounded by SIDECAR_MAX_AUDIO_SECONDS (20s), comfortably under
# the synchronous recognizer's 60s ceiling — streaming would complicate the
# turn boundary for no latency win at this length.
_SAMPLE_RATE = 16000

_client: speech_v2.SpeechAsyncClient | None = None


def _get_client() -> speech_v2.SpeechAsyncClient:
    global _client
    if _client is None:
        _client = speech_v2.SpeechAsyncClient()
    return _client


def _recognizer_path() -> str:
    # The trailing "_" means "no stored recognizer, use the inline config".
    return f"projects/{config.GCP_PROJECT}/locations/{config.GCP_LOCATION}/recognizers/_"


async def transcribe(pcm16_le_bytes: bytes) -> tuple[str, int]:
    """pcm16_le_bytes: mono little-endian PCM16 at 16kHz. Returns (text, elapsed_ms).

    Raises on API failure. app.py's /turn handler converts that into a failed
    turn the client can retry, exactly as a local STT crash used to.
    """
    client = _get_client()
    t0 = time.perf_counter()

    request = cloud_speech.RecognizeRequest(
        recognizer=_recognizer_path(),
        config=cloud_speech.RecognitionConfig(
            explicit_decoding_config=cloud_speech.ExplicitDecodingConfig(
                encoding=cloud_speech.ExplicitDecodingConfig.AudioEncoding.LINEAR16,
                sample_rate_hertz=_SAMPLE_RATE,
                audio_channel_count=1,
            ),
            language_codes=[config.STT_LANGUAGE],
            model=config.STT_MODEL,
        ),
        content=pcm16_le_bytes,
    )

    response = await client.recognize(
        request=request,
        timeout=config.SIDECAR_STT_TIMEOUT_SECONDS,
    )

    parts = [
        result.alternatives[0].transcript.strip()
        for result in response.results
        if result.alternatives
    ]
    text = " ".join(part for part in parts if part).strip()
    ms = int((time.perf_counter() - t0) * 1000)
    return text, ms
