"""PCM decode/resample/encode helpers.

The sidecar is the single place in the whole pipeline that knows about audio
formats. Unity always sends and receives raw little-endian PCM16 mono at a
known sample rate — never a container format — so every conversion lives here.
"""

import numpy as np
import soxr

# Canonical output format returned to Unity for every TTS reply, regardless of
# what the mic recorded at or what ElevenLabs actually handed back.
TARGET_SR = 24000


def pcm16_bytes_to_float32(data: bytes) -> np.ndarray:
    if not data:
        return np.zeros(0, dtype=np.float32)
    arr = np.frombuffer(data, dtype="<i2").astype(np.float32) / 32768.0
    return arr


def float32_to_pcm16_bytes(arr: np.ndarray) -> bytes:
    clipped = np.clip(arr, -1.0, 1.0)
    ints = (clipped * 32767.0).astype("<i2")
    return ints.tobytes()


def resample_float32(arr: np.ndarray, orig_sr: int, target_sr: int) -> np.ndarray:
    if orig_sr == target_sr or arr.size == 0:
        return arr.astype(np.float32)
    return soxr.resample(arr, orig_sr, target_sr).astype(np.float32)


def normalize_to_canonical(pcm_bytes: bytes, sample_rate: int, channels: int) -> tuple[bytes, int]:
    """Collapse whatever TTS gave us (any rate, mono or stereo) down to the
    one format Unity always receives: PCM16 LE mono @ TARGET_SR."""
    arr = pcm16_bytes_to_float32(pcm_bytes)
    if channels == 2 and arr.size > 0:
        arr = arr.reshape(-1, 2).mean(axis=1)
    arr = resample_float32(arr, sample_rate, TARGET_SR)
    return float32_to_pcm16_bytes(arr), TARGET_SR
