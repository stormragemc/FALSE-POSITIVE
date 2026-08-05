import unittest
from types import SimpleNamespace
from unittest.mock import patch

import numpy as np

import ser


class _FailingModel:
    def to(self, _device):
        raise RuntimeError("device initialization failed")


class _ReadyModel:
    def to(self, _device):
        return self

    def eval(self):
        return self


class HubertLoadStateTests(unittest.TestCase):
    def test_load_pins_revision_and_requires_safe_weights(self):
        previous = (ser._feature_extractor, ser._model, ser._device)
        ser._feature_extractor = None
        ser._model = None
        ser._device = "unavailable"
        extractor_calls = []
        model_calls = []

        def capture_extractor(*args, **kwargs):
            extractor_calls.append((args, kwargs))
            return object()

        def capture_model(*args, **kwargs):
            model_calls.append((args, kwargs))
            return _ReadyModel()

        try:
            with patch.object(ser.config, "HUBERT_DEVICE", "cpu"), patch.object(
                ser.config, "HUBERT_MODEL_REVISION", "reviewed-sha"
            ), patch.object(
                ser.config, "HUBERT_LOCAL_FILES_ONLY", True
            ), patch.object(
                ser.AutoFeatureExtractor, "from_pretrained", side_effect=capture_extractor
            ), patch.object(
                ser.AutoModelForAudioClassification, "from_pretrained", side_effect=capture_model
            ), patch.object(
                ser,
                "_analyze_impl",
                return_value=SimpleNamespace(
                    probabilities={"neutral": 0.7, "happy": 0.1, "angry": 0.1, "sad": 0.1}
                ),
            ):
                ser.load()

            self.assertEqual(
                extractor_calls,
                [((ser.config.HUBERT_MODEL_ID,), {"revision": "reviewed-sha", "local_files_only": True})],
            )
            self.assertEqual(
                model_calls,
                [
                    (
                        (ser.config.HUBERT_MODEL_ID,),
                        {
                            "revision": "reviewed-sha",
                            "local_files_only": True,
                            "use_safetensors": True,
                        },
                    )
                ],
            )
        finally:
            ser._feature_extractor, ser._model, ser._device = previous

    def test_failed_load_rolls_back_partial_model_state(self):
        previous = (ser._feature_extractor, ser._model, ser._device)
        ser._feature_extractor = None
        ser._model = None
        ser._device = "cpu"
        try:
            with patch.object(ser.config, "HUBERT_DEVICE", "cuda"), patch.object(
                ser.AutoFeatureExtractor,
                "from_pretrained",
                return_value=object(),
            ), patch.object(
                ser.AutoModelForAudioClassification,
                "from_pretrained",
                return_value=_FailingModel(),
            ):
                with self.assertRaisesRegex(RuntimeError, "device initialization failed"):
                    ser.load()

            self.assertFalse(ser.is_loaded())
            self.assertIsNone(ser._feature_extractor)
            self.assertEqual(ser.device(), "unavailable")
        finally:
            ser._feature_extractor, ser._model, ser._device = previous

    def test_load_rejects_checkpoint_with_incompatible_emotion_labels(self):
        previous = (ser._feature_extractor, ser._model, ser._device)
        ser._feature_extractor = None
        ser._model = None
        ser._device = "unavailable"
        try:
            with patch.object(ser.config, "HUBERT_DEVICE", "cpu"), patch.object(
                ser.AutoFeatureExtractor,
                "from_pretrained",
                return_value=object(),
            ), patch.object(
                ser.AutoModelForAudioClassification,
                "from_pretrained",
                return_value=_ReadyModel(),
            ), patch.object(
                ser,
                "_analyze_impl",
                return_value=SimpleNamespace(probabilities={"joy": 0.6, "fear": 0.4}),
            ):
                with self.assertRaisesRegex(RuntimeError, "incompatible emotion labels"):
                    ser.load()

            self.assertFalse(ser.is_loaded())
            self.assertEqual(ser.device(), "unavailable")
        finally:
            ser._feature_extractor, ser._model, ser._device = previous


class AudioConditioningTests(unittest.TestCase):
    """Guards on the two steps that run before the encoder sees a waveform.

    Both exist because this checkpoint ships do_normalize=false and mean-pools
    over every frame, so input level and dead air both leak into the prediction.
    """

    @staticmethod
    def _tone(seconds, amplitude=0.2):
        t = np.arange(int(seconds * 16000), dtype=np.float32)
        return (np.sin(t * 0.05) * amplitude).astype(np.float32)

    def test_normalize_reaches_target_rms(self):
        for amplitude in (0.005, 0.05, 0.4):
            with self.subTest(amplitude=amplitude):
                out = ser._normalize_level(self._tone(1.0, amplitude))
                rms = float(np.sqrt(np.mean(out.astype(np.float64) ** 2)))
                self.assertAlmostEqual(rms, ser._TARGET_RMS, places=4)

    def test_normalize_leaves_near_silence_untouched(self):
        quiet = (np.ones(16000, dtype=np.float32) * 1e-6)
        self.assertIs(ser._normalize_level(quiet), quiet)
        silence = np.zeros(16000, dtype=np.float32)
        self.assertIs(ser._normalize_level(silence), silence)

    def test_normalize_output_stays_in_range(self):
        spiky = self._tone(1.0, 0.001).copy()
        spiky[100] = 0.9  # a lone transient must not push the scaled clip past 1.0
        out = ser._normalize_level(spiky)
        self.assertLessEqual(float(np.max(np.abs(out))), 1.0)

    def test_trim_removes_edge_silence_but_keeps_internal_pauses(self):
        pad = np.zeros(16000, dtype=np.float32)
        gap = np.zeros(8000, dtype=np.float32)
        speech = self._tone(0.5)
        clip = np.concatenate([pad, speech, gap, speech, pad])

        trimmed = ser._trim_silence(clip)

        self.assertLess(trimmed.size, clip.size)
        # Both bursts and the 0.5s pause between them survive; only the 1s ends go.
        self.assertGreaterEqual(trimmed.size, speech.size * 2 + gap.size)
        # Upper bound carries the frame width as well as the margins, since the
        # last voiced window extends _TRIM_FRAME past its own start offset.
        self.assertLessEqual(
            trimmed.size,
            speech.size * 2 + gap.size + 2 * ser._TRIM_MARGIN_SAMPLES
            + ser._TRIM_FRAME + ser._TRIM_HOP,
        )

    def test_trim_returns_input_when_nothing_crosses_the_floor(self):
        silence = np.zeros(16000, dtype=np.float32)
        self.assertIs(ser._trim_silence(silence), silence)

    def test_trim_always_leaves_the_encoder_a_usable_window(self):
        """The margins are what make the min-length question moot."""
        clip = np.zeros(16000, dtype=np.float32)
        clip[8000] = 0.5  # a lone click is the narrowest possible voiced span
        trimmed = ser._trim_silence(clip)
        self.assertLess(trimmed.size, clip.size)
        self.assertGreaterEqual(trimmed.size, 2 * ser._TRIM_MARGIN_SAMPLES)

    def test_trim_leaves_short_clips_alone(self):
        tiny = self._tone(0.01)
        self.assertIs(ser._trim_silence(tiny), tiny)


if __name__ == "__main__":
    unittest.main()
