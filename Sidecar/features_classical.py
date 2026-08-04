"""Deterministic, local audio features used beside HuBERT.

These measurements are quality/timing context, not psychological diagnoses.
They intentionally depend only on NumPy so orchestration tests never need to
download a model or send player audio anywhere.
"""

from dataclasses import dataclass

import numpy as np


@dataclass(frozen=True)
class ClassicalFeatures:
    duration_seconds: float
    speech_ratio: float
    long_pause_count: int
    pitch_variability: float
    energy_variability: float
    clipping_ratio: float
    rms: float
    flags: tuple[str, ...]


def _frames(audio: np.ndarray, frame_length: int, hop_length: int) -> np.ndarray:
    if audio.size == 0:
        return np.zeros((0, frame_length), dtype=np.float32)
    if audio.size < frame_length:
        return np.pad(audio, (0, frame_length - audio.size))[None, :]
    count = 1 + (audio.size - frame_length) // hop_length
    shape = (count, frame_length)
    strides = (audio.strides[0] * hop_length, audio.strides[0])
    return np.lib.stride_tricks.as_strided(audio, shape=shape, strides=strides).copy()


def _count_long_pauses(mask: np.ndarray, hop_seconds: float, minimum_seconds: float = 0.40) -> int:
    if mask.size < 3:
        return 0
    voiced_indices = np.flatnonzero(mask)
    if voiced_indices.size < 2:
        return 0
    interior = ~mask[voiced_indices[0] : voiced_indices[-1] + 1]
    minimum_frames = max(1, int(round(minimum_seconds / hop_seconds)))
    count = run = 0
    for silent in interior:
        if silent:
            run += 1
        else:
            if run >= minimum_frames:
                count += 1
            run = 0
    if run >= minimum_frames:
        count += 1
    return count


def _pitch_variability(frames: np.ndarray, voiced: np.ndarray, sample_rate: int) -> float:
    """Return voiced-frame pitch coefficient of variation, capped for stability."""
    pitches: list[float] = []
    minimum_lag = max(1, int(sample_rate / 350.0))
    maximum_lag = min(frames.shape[1] - 1, int(sample_rate / 75.0))
    window = np.hanning(frames.shape[1]).astype(np.float32)
    for frame in frames[voiced]:
        centered = (frame - np.mean(frame)) * window
        energy = float(np.dot(centered, centered))
        if energy <= 1e-8:
            continue
        correlation = np.correlate(centered, centered, mode="full")[frame.size - 1 :]
        region = correlation[minimum_lag : maximum_lag + 1]
        if region.size == 0:
            continue
        offset = int(np.argmax(region))
        peak = float(region[offset] / max(correlation[0], 1e-8))
        if peak >= 0.30:
            pitches.append(sample_rate / float(minimum_lag + offset))
    if len(pitches) < 2:
        return 0.0
    mean_pitch = float(np.mean(pitches))
    return min(2.0, float(np.std(pitches) / max(mean_pitch, 1e-6)))


def extract(audio_f32: np.ndarray, sample_rate: int = 16000) -> ClassicalFeatures:
    if sample_rate <= 0:
        raise ValueError("sample_rate must be positive")
    audio = np.asarray(audio_f32, dtype=np.float32).reshape(-1)
    audio = np.nan_to_num(audio, nan=0.0, posinf=1.0, neginf=-1.0)
    duration = audio.size / float(sample_rate)
    overall_rms = float(np.sqrt(np.mean(np.square(audio)))) if audio.size else 0.0
    clipping_ratio = float(np.mean(np.abs(audio) >= 0.995)) if audio.size else 0.0

    frame_length = max(1, int(round(0.025 * sample_rate)))
    hop_length = max(1, int(round(0.010 * sample_rate)))
    framed = _frames(audio, frame_length, hop_length)
    frame_rms = (
        np.sqrt(np.mean(np.square(framed), axis=1))
        if framed.size
        else np.zeros(0, dtype=np.float32)
    )
    upper_energy = float(np.percentile(frame_rms, 90)) if frame_rms.size else 0.0
    speech_threshold = max(0.008, upper_energy * 0.15)
    voiced = frame_rms >= speech_threshold
    speech_ratio = float(np.mean(voiced)) if voiced.size else 0.0
    long_pauses = _count_long_pauses(voiced, hop_length / float(sample_rate))

    if np.count_nonzero(voiced) >= 2:
        voiced_energy = np.log(np.maximum(frame_rms[voiced], 1e-6))
        energy_variability = min(4.0, float(np.std(voiced_energy)))
    else:
        energy_variability = 0.0

    flags: list[str] = []
    if duration < 1.5:
        flags.append("audio_too_short")
    if overall_rms < 0.008:
        flags.append("near_silence")
    if clipping_ratio >= 0.01:
        flags.append("clipping")
    if speech_ratio < 0.25:
        flags.append("low_speech_ratio")

    return ClassicalFeatures(
        duration_seconds=round(duration, 4),
        speech_ratio=round(speech_ratio, 4),
        long_pause_count=long_pauses,
        pitch_variability=round(_pitch_variability(framed, voiced, sample_rate), 4),
        energy_variability=round(energy_variability, 4),
        clipping_ratio=round(clipping_ratio, 6),
        rms=round(overall_rms, 6),
        flags=tuple(flags),
    )
