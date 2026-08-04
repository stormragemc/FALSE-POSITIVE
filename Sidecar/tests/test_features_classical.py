import unittest

import numpy as np

from features_classical import extract


SAMPLE_RATE = 16000


def tone(seconds: float, frequency: float = 200.0, amplitude: float = 0.2) -> np.ndarray:
    samples = int(seconds * SAMPLE_RATE)
    time = np.arange(samples, dtype=np.float32) / SAMPLE_RATE
    return (amplitude * np.sin(2.0 * np.pi * frequency * time)).astype(np.float32)


class ClassicalFeatureTests(unittest.TestCase):
    def test_stable_tone_has_speech_and_stable_pitch(self):
        result = extract(tone(2.0), SAMPLE_RATE)

        self.assertGreater(result.speech_ratio, 0.95)
        self.assertLess(result.pitch_variability, 0.03)
        self.assertNotIn("audio_too_short", result.flags)
        self.assertNotIn("near_silence", result.flags)

    def test_internal_silence_counts_as_long_pause(self):
        audio = np.concatenate([tone(1.0), np.zeros(int(0.6 * SAMPLE_RATE)), tone(1.0)])

        result = extract(audio, SAMPLE_RATE)

        self.assertEqual(result.long_pause_count, 1)
        self.assertGreater(result.speech_ratio, 0.6)
        self.assertLess(result.speech_ratio, 0.9)

    def test_silence_and_clipping_are_explicit_quality_flags(self):
        silent = extract(np.zeros(SAMPLE_RATE * 2, dtype=np.float32), SAMPLE_RATE)
        clipped = extract(np.ones(SAMPLE_RATE * 2, dtype=np.float32), SAMPLE_RATE)

        self.assertIn("near_silence", silent.flags)
        self.assertIn("low_speech_ratio", silent.flags)
        self.assertIn("clipping", clipped.flags)
        self.assertGreaterEqual(clipped.clipping_ratio, 0.99)

    def test_non_finite_samples_do_not_poison_metrics(self):
        audio = tone(2.0)
        audio[10] = np.nan
        audio[20] = np.inf

        result = extract(audio, SAMPLE_RATE)

        self.assertTrue(np.isfinite(result.rms))
        self.assertTrue(np.isfinite(result.energy_variability))
        self.assertTrue(np.isfinite(result.pitch_variability))


if __name__ == "__main__":
    unittest.main()
