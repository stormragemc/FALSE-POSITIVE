import importlib.util
import os
from pathlib import Path
import unittest
from unittest.mock import patch

import config


class ConfigTests(unittest.TestCase):
    def _load_isolated(self, module_name: str, environment: dict[str, str]):
        path = Path(__file__).resolve().parents[1] / "config.py"
        spec = importlib.util.spec_from_file_location(module_name, path)
        module = importlib.util.module_from_spec(spec)
        with patch.dict(os.environ, environment, clear=True), patch(
            "dotenv.load_dotenv", return_value=False
        ):
            spec.loader.exec_module(module)
        return module

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

    def test_gemini_api_key_is_gone_so_no_stale_secret_is_expected(self):
        self.assertFalse(
            hasattr(config, "GEMINI_API_KEY"),
            "Vertex authenticates by service account; a GEMINI_API_KEY constant "
            "left behind invites someone to reintroduce a key-based client.",
        )

    def test_missing_gcp_project_is_a_startup_failure(self):
        path = Path(__file__).resolve().parents[1] / "config.py"
        spec = importlib.util.spec_from_file_location("sidecar_config_validate_test", path)
        module = importlib.util.module_from_spec(spec)
        with patch.dict(os.environ, {"GCP_PROJECT": ""}):
            spec.loader.exec_module(module)

        with self.assertRaises(SystemExit):
            module.validate()

    def test_missing_client_key_is_a_startup_failure(self):
        module = self._load_isolated(
            "sidecar_config_client_key_test",
            {
                "GCP_PROJECT": "test-project",
                "ELEVENLABS_API_KEY": "test-elevenlabs-key",
                "ELEVENLABS_VOICE_ID": "test-voice",
                "FP_CLIENT_KEY": "",
            },
        )

        with self.assertRaises(SystemExit):
            module.validate()

    def test_turn_limit_defaults_are_bounded_and_positive(self):
        module = self._load_isolated("sidecar_config_limit_defaults_test", {})

        self.assertEqual(module.MAX_TURNS_PER_SESSION, 40)
        self.assertEqual(module.MAX_TURNS_PER_DAY, 2000)

    def test_hubert_checkpoint_revision_is_immutable_by_default(self):
        module = self._load_isolated("sidecar_config_hubert_revision_test", {})

        self.assertEqual(
            module.HUBERT_MODEL_REVISION,
            "9a456581e0147a2b7fdaf56d77a9e8fce3865eaa",
        )
        self.assertFalse(module.HUBERT_LOCAL_FILES_ONLY)

    def test_session_retention_and_turn_deadline_have_bounded_defaults(self):
        module = self._load_isolated("sidecar_config_retention_deadline_test", {})

        self.assertEqual(module.SESSION_IDLE_TTL_SECONDS, 3600.0)
        self.assertEqual(module.TURN_DEADLINE_SECONDS, 50.0)
        self.assertEqual(module.SIDECAR_MAX_AUDIO_SECONDS, 20.0)
        self.assertEqual(module.MAX_TURN_REQUEST_BYTES, 700000)

    def test_local_binding_defaults_to_loopback_and_cloud_run_port(self):
        module = self._load_isolated("sidecar_config_binding_defaults_test", {})

        self.assertEqual(module.HOST, "127.0.0.1")
        self.assertEqual(module.PORT, 8080)

    def test_cloud_run_port_takes_precedence_over_local_override(self):
        module = self._load_isolated(
            "sidecar_config_cloud_run_port_test",
            {"PORT": "9090", "SIDECAR_PORT": "8765"},
        )

        self.assertEqual(module.PORT, 9090)

    def test_example_environment_documents_security_and_cost_controls_once(self):
        text = (Path(__file__).resolve().parents[1] / ".env.example").read_text(
            encoding="utf-8"
        )

        self.assertEqual(text.count("GCP_PROJECT="), 1)
        self.assertEqual(text.count("GCP_LOCATION="), 1)
        self.assertIn("FP_CLIENT_KEY=", text)
        self.assertIn("MAX_TURNS_PER_SESSION=40", text)
        self.assertIn("MAX_TURNS_PER_DAY=2000", text)


if __name__ == "__main__":
    unittest.main()
