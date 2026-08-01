"""Speech-to-text via faster-whisper — local, free, no API key.

Loaded once at process startup and warmed with a short silent buffer so the
first real utterance of a playtest isn't several times slower than the rest.
"""

import time

import numpy as np
from faster_whisper import WhisperModel

_MODEL_NAME = "small.en"  # English-only: faster and more accurate than
                            # multilingual "small" for an English-only game.

_model: WhisperModel | None = None


def load() -> WhisperModel:
    global _model
    if _model is None:
        print(f"[Sidecar] Loading STT model '{_MODEL_NAME}' (downloads on first run)...")
        _model = WhisperModel(_MODEL_NAME, device="cpu", compute_type="int8")
        # Warm up: the first inference call pays a one-time JIT/cache cost.
        list(_model.transcribe(np.zeros(8000, dtype=np.float32), language="en")[0])
        print("[Sidecar] STT model ready.")
    return _model


def transcribe(audio_f32_16k: np.ndarray) -> tuple[str, int]:
    """audio_f32_16k: mono float32 samples at 16kHz. Returns (text, elapsed_ms)."""
    model = load()
    t0 = time.perf_counter()
    segments, _info = model.transcribe(
        audio_f32_16k,
        language="en",
        beam_size=1,
        vad_filter=False,  # Unity already segmented the utterance via its own VAD.
    )
    text = " ".join(seg.text.strip() for seg in segments).strip()
    ms = int((time.perf_counter() - t0) * 1000)
    return text, ms
