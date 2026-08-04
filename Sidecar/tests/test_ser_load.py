import unittest
from types import SimpleNamespace
from unittest.mock import patch

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


if __name__ == "__main__":
    unittest.main()
