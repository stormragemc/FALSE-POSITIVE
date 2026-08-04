"""Session-relative orchestration for local HuBERT and audio features.

This module deliberately separates measurement from interpretation. It emits
bounded affect impressions that can influence dialogue pacing, but it has no
schema field or prompt instruction for deception, guilt, intent, or truth.
"""

from collections import OrderedDict
from dataclasses import asdict, dataclass, field
import math
from typing import Protocol

import numpy as np

from features_classical import ClassicalFeatures


class HubertObservation(Protocol):
    confidence: float
    probabilities: dict[str, float]
    normalized_entropy: float
    top_two_margin: float
    embedding: np.ndarray
    frame_instability: float


ORCHESTRATION_VERSION = "1.1"
MAX_SIGNAL_CONFIDENCE = 0.75
REFERENCE_SOMEWHAT_MIN_DISTANCE = 0.10
REFERENCE_CLEAR_MIN_DISTANCE = 0.20


@dataclass(frozen=True)
class ProsodySignal:
    version: str = ORCHESTRATION_VERSION
    available: bool = False
    reliable: bool = False
    reliability_reason: str = "unavailable"
    calibration_state: str = "calibrating"
    reference_turns: int = 0
    reference_comparison_available: bool = False
    duration_seconds: float = 0.0
    onset_delay_ms: int = 0
    speech_ratio: float = 0.0
    long_pause_count: int = 0
    speech_rate_delta: float = 0.0
    pitch_variability: float = 0.0
    energy_variability: float = 0.0
    hubert_instability: float = 0.0
    hubert_baseline_distance: float = 0.0
    hubert_reference_change: float = 0.0
    arousal: float = 0.0
    tension: float = 0.0
    confidence_in_signal: float = 0.0
    trend: str = "unknown"
    class_probabilities: dict[str, float] = field(default_factory=dict)
    flags: list[str] = field(default_factory=list)

    def to_dict(self) -> dict:
        return asdict(self)

    def prompt_context(self, minimum_confidence: float) -> str:
        marker = "[LOCAL AFFECT SIGNAL — sidecar generated]"
        if not self.available or not self.reliable or self.confidence_in_signal < minimum_confidence:
            return (
                f"{marker}\nNo reliable vocal-affect impression is available for this turn. "
                "Do not adapt the question based on vocal delivery."
            )

        dominant = max(self.class_probabilities.items(), key=lambda item: item[1], default=None)
        tone = dominant[0] if dominant else "unclear"
        change = "No stable early-session comparison is available yet."
        if self.reference_comparison_available:
            if (
                self.hubert_baseline_distance >= REFERENCE_CLEAR_MIN_DISTANCE
                and self.hubert_reference_change >= 2.5
            ):
                change = "Delivery differs clearly from the early-session reference."
            elif (
                self.hubert_baseline_distance >= REFERENCE_SOMEWHAT_MIN_DISTANCE
                and self.hubert_reference_change >= 1.25
            ):
                change = "Delivery differs somewhat from the early-session reference."
            else:
                change = "No clear departure from the early-session reference was established."
        trend = (
            "The recent tension impression is rising."
            if self.trend == "rising"
            else "No strong rising pattern is established."
        )
        pacing = (
            "There were notable internal pauses."
            if self.long_pause_count
            else "No long internal pause stood out."
        )
        timing = (
            "The response began after a noticeable delay."
            if self.onset_delay_ms >= 2500
            else ""
        )
        rate = ""
        if self.reference_comparison_available:
            if self.speech_rate_delta >= 0.25:
                rate = "Speech was faster than the early-session reference."
            elif self.speech_rate_delta <= -0.25:
                rate = "Speech was slower than the early-session reference."
        activation = (
            "Overall vocal activation appeared elevated."
            if self.arousal >= 0.65
            else ""
        )
        clauses = [
            f"Fallible acoustic impression: tone leaned {tone}.",
            change,
            trend,
            pacing,
            timing,
            rate,
            activation,
            "Use this only for subtle pacing or one follow-up choice.",
            "Do not mention the sensor, labels, scores, or comparison to the witness.",
        ]
        return f"{marker}\n" + " ".join(clause for clause in clauses if clause)


class ProsodyTracker:
    def __init__(self, reference_turns: int = 3, minimum_confidence: float = 0.40):
        self.reference_target = max(1, int(reference_turns))
        self.minimum_confidence = min(
            MAX_SIGNAL_CONFIDENCE, max(0.0, float(minimum_confidence))
        )
        self._reference_embeddings: list[np.ndarray] = []
        self._reference_speech_rates: list[float] = []
        self._recent_tension: list[float] = []

    @property
    def reference_count(self) -> int:
        return len(self._reference_embeddings)

    def _reference_centroid(self, embedding: np.ndarray) -> np.ndarray | None:
        if self.reference_count < self.reference_target:
            return None
        if any(item.shape != embedding.shape for item in self._reference_embeddings):
            return None
        return np.mean(np.stack(self._reference_embeddings), axis=0)

    @staticmethod
    def _cosine_distance(left: np.ndarray, right: np.ndarray) -> float:
        denominator = float(np.linalg.norm(left) * np.linalg.norm(right))
        if denominator <= 1e-8:
            return 0.0
        return min(2.0, max(0.0, 1.0 - float(np.dot(left, right) / denominator)))

    def _reference_distance_scale(self, centroid: np.ndarray) -> float:
        distances = [
            self._cosine_distance(reference, centroid)
            for reference in self._reference_embeddings
        ]
        # HuBERT mean-pooled vectors are anisotropic, so a raw cosine cutoff is
        # not portable. Compare the turn with this session's observed reference
        # spread, retaining a floor for nearly identical calibration turns.
        return max(0.02, max(distances, default=0.0) * 2.0)

    @staticmethod
    def _speech_rate(transcript: str, features: ClassicalFeatures) -> float:
        words = len([word for word in transcript.strip().split() if word])
        speech_seconds = max(0.25, features.duration_seconds * max(features.speech_ratio, 0.1))
        return min(400.0, words * 60.0 / speech_seconds)

    @staticmethod
    def _confidence(observation: HubertObservation, features: ClassicalFeatures) -> float:
        top_quality = np.clip((observation.confidence - 0.25) / 0.50, 0.0, 1.0)
        margin_quality = np.clip(observation.top_two_margin / 0.45, 0.0, 1.0)
        entropy_quality = np.clip((1.0 - observation.normalized_entropy) / 0.45, 0.0, 1.0)
        certainty = 0.45 * top_quality + 0.30 * margin_quality + 0.25 * entropy_quality

        duration_quality = np.clip(features.duration_seconds / 2.0, 0.0, 1.0)
        speech_quality = np.clip(features.speech_ratio / 0.60, 0.0, 1.0)
        quality = 0.55 * duration_quality + 0.45 * speech_quality
        confidence = float(certainty * quality)
        if "near_silence" in features.flags:
            confidence *= 0.10
        if "clipping" in features.flags:
            confidence *= 0.35
        if "audio_too_short" in features.flags:
            confidence *= 0.35
        # The checkpoint's published in-domain accuracy is only about 64%,
        # and player speech is out of domain. This is signal usability, not a
        # calibrated probability of correctness, so never present it as near-certain.
        return min(MAX_SIGNAL_CONFIDENCE, max(0.0, confidence))

    def update(
        self,
        features: ClassicalFeatures,
        observation: HubertObservation | None,
        transcript: str,
        onset_delay_ms: int,
        unavailable_reason: str = "hubert_unavailable",
        commit: bool = True,
    ) -> ProsodySignal:
        flags = list(features.flags)
        if observation is None:
            if unavailable_reason not in flags:
                flags.append(unavailable_reason)
            return ProsodySignal(
                reliability_reason=unavailable_reason,
                reference_turns=self.reference_count,
                duration_seconds=features.duration_seconds,
                onset_delay_ms=max(0, int(onset_delay_ms)),
                speech_ratio=features.speech_ratio,
                long_pause_count=features.long_pause_count,
                pitch_variability=features.pitch_variability,
                energy_variability=features.energy_variability,
                flags=flags,
            )

        confidence = self._confidence(observation, features)
        critical_quality_flags = {"near_silence", "clipping", "audio_too_short", "low_speech_ratio"}
        reliable = confidence >= self.minimum_confidence and not critical_quality_flags.intersection(flags)
        if not reliable and "low_signal_confidence" not in flags:
            flags.append("low_signal_confidence")

        angry = observation.probabilities.get("angry", 0.0)
        happy = observation.probabilities.get("happy", 0.0)
        sad = observation.probabilities.get("sad", 0.0)
        instability = 1.0 - math.exp(-2.0 * max(0.0, observation.frame_instability))
        energy = min(1.0, features.energy_variability / 1.2)
        pitch = min(1.0, features.pitch_variability / 0.35)
        arousal = min(
            1.0,
            0.45 * (angry + happy)
            + 0.20 * instability
            + 0.20 * energy
            + 0.15 * pitch,
        )
        tension = min(1.0, 0.55 * angry + 0.20 * sad + 0.15 * instability + 0.10 * energy)

        embedding = np.asarray(observation.embedding, dtype=np.float32).reshape(-1)
        centroid = self._reference_centroid(embedding)
        baseline_distance = self._cosine_distance(embedding, centroid) if centroid is not None else 0.0
        reference_change = (
            min(10.0, baseline_distance / self._reference_distance_scale(centroid))
            if centroid is not None
            else 0.0
        )
        speech_rate_available = "affect_window_truncated" not in flags
        speech_rate = self._speech_rate(transcript, features) if speech_rate_available else 0.0
        speech_rate_delta = 0.0
        if centroid is not None and speech_rate_available and self._reference_speech_rates:
            reference_rate = float(np.mean(self._reference_speech_rates))
            if reference_rate > 1e-6:
                speech_rate_delta = float(np.clip(speech_rate / reference_rate - 1.0, -2.0, 2.0))

        trend = "unknown"
        if reliable:
            trend = "stable"
            if len(self._recent_tension) >= 2:
                if tension >= float(np.mean(self._recent_tension[-2:])) + 0.12:
                    trend = "rising"
                    flags.append("rising_tension")
                elif tension <= float(np.mean(self._recent_tension[-2:])) - 0.12:
                    trend = "falling"

        if reliable and commit:
            self._recent_tension.append(tension)
            self._recent_tension = self._recent_tension[-5:]
            if self.reference_count < self.reference_target:
                self._reference_embeddings.append(embedding.copy())
                if speech_rate_available:
                    self._reference_speech_rates.append(speech_rate)
                if self.reference_count == self.reference_target:
                    flags.append("reference_established")

        # Session readiness is post-commit state. Comparison availability is
        # turn-specific: the turn that completes the reference was not compared
        # with itself, but the following turn can use the now-ready reference.
        calibration_state = "ready" if self.reference_count >= self.reference_target else "calibrating"
        reason = "usable" if reliable else (flags[0] if features.flags else "low_signal_confidence")
        return ProsodySignal(
            available=True,
            reliable=reliable,
            reliability_reason=reason,
            calibration_state=calibration_state,
            reference_turns=self.reference_count,
            reference_comparison_available=centroid is not None,
            duration_seconds=features.duration_seconds,
            onset_delay_ms=max(0, int(onset_delay_ms)),
            speech_ratio=features.speech_ratio,
            long_pause_count=features.long_pause_count,
            speech_rate_delta=round(speech_rate_delta, 4),
            pitch_variability=features.pitch_variability,
            energy_variability=features.energy_variability,
            hubert_instability=observation.frame_instability,
            hubert_baseline_distance=round(baseline_distance, 4),
            hubert_reference_change=round(reference_change, 4),
            arousal=round(arousal, 4),
            tension=round(tension, 4),
            confidence_in_signal=round(confidence, 4),
            trend=trend,
            class_probabilities=dict(observation.probabilities),
            flags=flags,
        )


class ProsodyRegistry:
    """Bounded LRU registry so arbitrary local session IDs cannot grow memory forever."""

    def __init__(self, max_sessions: int, reference_turns: int, minimum_confidence: float):
        self.max_sessions = max(1, int(max_sessions))
        self.reference_turns = reference_turns
        self.minimum_confidence = minimum_confidence
        self._trackers: OrderedDict[str, ProsodyTracker] = OrderedDict()

    def get(self, session_id: str) -> ProsodyTracker:
        tracker = self.preview(session_id)
        self.commit(session_id, tracker)
        return tracker

    def preview(self, session_id: str) -> ProsodyTracker:
        tracker = self._trackers.get(session_id)
        if tracker is None:
            tracker = ProsodyTracker(self.reference_turns, self.minimum_confidence)
        return tracker

    def commit(self, session_id: str, tracker: ProsodyTracker) -> None:
        self._trackers.pop(session_id, None)
        self._trackers[session_id] = tracker
        while len(self._trackers) > self.max_sessions:
            self._trackers.popitem(last=False)

    def reset(self, session_id: str) -> None:
        self._trackers.pop(session_id, None)

    def __len__(self) -> int:
        return len(self._trackers)
