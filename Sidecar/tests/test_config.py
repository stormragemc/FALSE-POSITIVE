import importlib.util
import os
from pathlib import Path
import unittest
from unittest.mock import patch

import config


class ConfigTests(unittest.TestCase):
    def test_blank_boolean_uses_declared_default(self):
        with patch.dict(os.environ, {"TEST_BOOLEAN": ""}):
            self.assertFalse(config._env_bool("TEST_BOOLEAN", False))
            self.assertTrue(config._env_bool("TEST_BOOLEAN", True))

    def test_minimum_confidence_cannot_exceed_emitted_signal_cap(self):
        path = Path(__file__).resolve().parents[1] / "config.py"
        spec = importlib.util.spec_from_file_location("sidecar_config_cap_test", path)
        module = importlib.util.module_from_spec(spec)
        with patch.dict(os.environ, {"PROSODY_MIN_CONFIDENCE": "0.99"}):
            spec.loader.exec_module(module)

        self.assertEqual(module.PROSODY_MIN_CONFIDENCE, 0.75)


if __name__ == "__main__":
    unittest.main()
