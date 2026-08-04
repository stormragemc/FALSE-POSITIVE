from dataclasses import dataclass
import json
import unittest

import numpy as np

from features_classical import ClassicalFeatures
from prosody import ProsodyRegistry, ProsodySignal, ProsodyTracker


@dataclass(frozen=True)
class FakeHubertObservation:
    label: str
    confidence: float
    probabilities: dict[str, float]
    normalized_entropy: float
    top_two_margin: float
    embedding: np.ndarray
    frame_instability: float = 0.10
    elapsed_ms: int = 12
    model_id: str = "test/hubert"


def features(**overrides) -> ClassicalFeatures:
    values = {
        "duration_seconds": 3.0,
        "speech_ratio": 0.85,
        "long_pause_count": 0,
        "pitch_variability": 0.10,
        "energy_variability": 0.30,
        "clipping_ratio": 0.0,
        "rms": 0.12,
        "flags": (),
    }
    values.update(overrides)
    return ClassicalFeatures(**values)


def observation(
    embedding=(1.0, 0.0, 0.0, 0.0),
    probabilities=None,
) -> FakeHubertObservation:
    probabilities = probabilities or {
        "neutral": 0.82,
        "happy": 0.06,
        "angry": 0.07,
        "sad": 0.05,
    }
    ranked = sorted(probabilities.values(), reverse=True)
    return FakeHubertObservation(
        label=max(probabilities, key=probabilities.get),
        confidence=ranked[0],
        probabilities=probabilities,
        normalized_entropy=0.40,
        top_two_margin=ranked[0] - ranked[1],
        embedding=np.asarray(embedding, dtype=np.float32),
    )


class ProsodyTrackerTests(unittest.TestCase):
    def test_reference_calibrates_then_enables_relative_distance(self):
        tracker = ProsodyTracker(reference_turns=3, minimum_confidence=0.35)
        for _ in range(3):
            signal = tracker.update(features(), observation(), "six ordinary words are spoken", 500)

        self.assertEqual(signal.calibration_state, "ready")
        self.assertEqual(signal.reference_turns, 3)
        self.assertFalse(signal.reference_comparison_available)
        changed = tracker.update(
            features(),
            observation(embedding=(0.0, 1.0, 0.0, 0.0)),
            "six ordinary words are spoken",
            900,
        )
        self.assertGreater(changed.hubert_baseline_distance, 0.9)
        self.assertTrue(changed.reference_comparison_available)
        self.assertIn("differs clearly", changed.prompt_context(0.35))

    def test_reference_change_is_calibrated_to_session_embedding_spread(self):
        tracker = ProsodyTracker(reference_turns=3, minimum_confidence=0.35)
        for embedding in (
            (1.0, -0.08, 0.0, 0.0),
            (1.0, 0.0, 0.0, 0.0),
            (1.0, 0.08, 0.0, 0.0),
        ):
            tracker.update(
                features(), observation(embedding=embedding), "six ordinary words are spoken", 500
            )

        changed = tracker.update(
            features(),
            observation(embedding=(1.0, 0.55, 0.0, 0.0)),
            "six ordinary words are spoken",
            500,
        )

        self.assertGreater(changed.hubert_baseline_distance, 0.10)
        self.assertGreater(changed.hubert_reference_change, 2.5)
        self.assertIn("differs somewhat", changed.prompt_context(0.35))

    def test_tight_reference_does_not_amplify_small_absolute_change(self):
        tracker = ProsodyTracker(reference_turns=3, minimum_confidence=0.35)
        for _ in range(3):
            tracker.update(
                features(), observation(), "six ordinary words are spoken", 500
            )

        changed = tracker.update(
            features(),
            observation(embedding=(1.0, 0.34, 0.0, 0.0)),
            "six ordinary words are spoken",
            500,
        )

        self.assertLess(changed.hubert_baseline_distance, 0.10)
        self.assertGreater(changed.hubert_reference_change, 2.5)
        self.assertIn("no clear departure", changed.prompt_context(0.35).lower())

    def test_low_quality_turn_is_suppressed_and_not_added_to_reference(self):
        tracker = ProsodyTracker(reference_turns=3, minimum_confidence=0.35)
        signal = tracker.update(
            features(duration_seconds=0.5, speech_ratio=0.1, flags=("audio_too_short", "low_speech_ratio")),
            observation(),
            "brief",
            100,
        )

        self.assertFalse(signal.reliable)
        self.assertLessEqual(signal.confidence_in_signal, 0.75)
        self.assertEqual(signal.reference_turns, 0)
        self.assertIn("No reliable", signal.prompt_context(0.35))

    def test_unreliable_turn_does_not_report_a_stable_trend(self):
        signal = ProsodyTracker(reference_turns=1, minimum_confidence=0.35).update(
            features(duration_seconds=0.3, flags=("audio_too_short",)),
            observation(),
            "brief",
            0,
        )

        self.assertFalse(signal.reliable)
        self.assertEqual(signal.trend, "unknown")

    def test_minimum_confidence_is_capped_at_maximum_signal_confidence(self):
        signal = ProsodyTracker(reference_turns=1, minimum_confidence=0.99).update(
            features(), observation(), "six ordinary words are spoken", 0
        )

        self.assertEqual(signal.confidence_in_signal, 0.75)
        self.assertTrue(signal.reliable)

    def test_preview_does_not_mutate_reference_or_trend_state(self):
        tracker = ProsodyTracker(reference_turns=1, minimum_confidence=0.25)

        preview = tracker.update(
            features(), observation(), "six ordinary words are spoken", 0, commit=False
        )

        self.assertTrue(preview.reliable)
        self.assertEqual(tracker.reference_count, 0)

    def test_pitch_variability_contributes_to_arousal_impression(self):
        low_pitch = ProsodyTracker(reference_turns=1, minimum_confidence=0.25).update(
            features(pitch_variability=0.01), observation(), "one two three", 0
        )
        high_pitch = ProsodyTracker(reference_turns=1, minimum_confidence=0.25).update(
            features(pitch_variability=0.50), observation(), "one two three", 0
        )

        self.assertGreater(high_pitch.arousal, low_pitch.arousal + 0.10)

    def test_truncated_affect_window_does_not_claim_speech_rate_change(self):
        tracker = ProsodyTracker(reference_turns=1, minimum_confidence=0.25)
        tracker.update(features(), observation(), "one two three four five six", 0)
        signal = tracker.update(
            features(duration_seconds=30.0, flags=("affect_window_truncated",)),
            observation(),
            " ".join(["word"] * 90),
            0,
        )

        prompt = signal.prompt_context(0.25).lower()
        self.assertEqual(signal.speech_rate_delta, 0.0)
        self.assertNotIn("faster than", prompt)
        self.assertNotIn("slower than", prompt)

    def test_unavailable_hubert_keeps_audio_debug_context(self):
        signal = ProsodyTracker().update(
            features(long_pause_count=2), None, "text remains usable", 700, "hubert_inference_failed"
        )

        self.assertFalse(signal.available)
        self.assertEqual(signal.long_pause_count, 2)
        self.assertIn("hubert_inference_failed", signal.flags)

    def test_rising_tension_is_a_temporal_impression(self):
        tracker = ProsodyTracker(reference_turns=1, minimum_confidence=0.25)
        calm = observation(probabilities={"neutral": 0.82, "happy": 0.06, "angry": 0.07, "sad": 0.05})
        elevated = observation(probabilities={"neutral": 0.05, "happy": 0.05, "angry": 0.85, "sad": 0.05})
        tracker.update(features(), calm, "one two three four", 100)
        tracker.update(features(), calm, "one two three four", 100)
        signal = tracker.update(features(), elevated, "one two three four", 100)

        self.assertEqual(signal.trend, "rising")
        self.assertIn("rising_tension", signal.flags)

    def test_registry_is_bounded_and_reset_discards_reference(self):
        registry = ProsodyRegistry(max_sessions=2, reference_turns=1, minimum_confidence=0.25)
        registry.get("a").update(features(), observation(), "one two", 0)
        registry.get("b")
        registry.get("c")

        self.assertEqual(len(registry), 2)
        self.assertEqual(registry.get("a").reference_count, 0)
        registry.get("b").update(features(), observation(), "one two", 0)
        registry.reset("b")
        self.assertEqual(registry.get("b").reference_count, 0)

    def test_payload_schema_has_no_diagnostic_claim_keys(self):
        payload = json.dumps(ProsodySignal().to_dict()).lower()
        for forbidden in ("deception", "guilt", "truth", "intent", "lie_score", "is_lying"):
            self.assertNotIn(forbidden, payload)

    def test_populated_signal_payload_is_json_serializable(self):
        tracker = ProsodyTracker(reference_turns=1, minimum_confidence=0.25)
        tracker.update(features(), observation(), "one two three", 0)
        signal = tracker.update(
            features(long_pause_count=1, pitch_variability=0.35),
            observation(embedding=(0.0, 1.0, 0.0, 0.0)),
            "one two three four five",
            2800,
        )

        encoded = json.dumps(signal.to_dict())
        self.assertIn('"reference_comparison_available": true', encoded)
        self.assertIn('"hubert_reference_change":', encoded)

    def test_actionable_prompt_stays_bounded_and_non_diagnostic(self):
        tracker = ProsodyTracker(reference_turns=1, minimum_confidence=0.25)
        signal = tracker.update(features(long_pause_count=1), observation(), "one two three", 250)
        prompt = signal.prompt_context(0.25).lower()

        self.assertIn("local affect signal", prompt)
        self.assertIn("subtle pacing", prompt)
        for forbidden in ("deception", "guilt", "truth", "intent", "lying"):
            self.assertNotIn(forbidden, prompt)

    def test_prompt_omits_low_probability_runner_up_label(self):
        tracker = ProsodyTracker(reference_turns=1, minimum_confidence=0.25)
        signal = tracker.update(features(), observation(), "one two three", 250)
        prompt = signal.prompt_context(0.25).lower()

        self.assertIn("tone leaned neutral", prompt)
        self.assertNotIn("tone leaned neutral and angry", prompt)
        self.assertNotIn("  ", prompt)

    def test_prompt_uses_timing_rate_and_arousal_without_diagnostic_claims(self):
        signal = ProsodySignal(
            available=True,
            reliable=True,
            confidence_in_signal=0.70,
            reference_comparison_available=True,
            hubert_reference_change=1.5,
            onset_delay_ms=3000,
            speech_rate_delta=0.35,
            arousal=0.70,
            class_probabilities={"neutral": 0.70, "angry": 0.20, "happy": 0.05, "sad": 0.05},
        )

        prompt = signal.prompt_context(0.40).lower()
        self.assertIn("noticeable delay", prompt)
        self.assertIn("faster than", prompt)
        self.assertIn("vocal activation appeared elevated", prompt)
        self.assertNotIn("  ", prompt)
        for forbidden in ("deception", "guilt", "truth", "intent", "lying"):
            self.assertNotIn(forbidden, prompt)


if __name__ == "__main__":
    unittest.main()
