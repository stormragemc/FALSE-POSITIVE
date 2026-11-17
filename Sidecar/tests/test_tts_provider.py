"""Offline provider contracts: Vertex PCM, explicit selection, no hidden vendor fallback."""

from types import SimpleNamespace
import unittest
from unittest.mock import patch

import config
import tts


def audio_response(data=b"\x01\x00\x02\x00", mime="audio/L16;codec=pcm;rate=24000"):
    return SimpleNamespace(candidates=[SimpleNamespace(content=SimpleNamespace(parts=[
        SimpleNamespace(inline_data=SimpleNamespace(data=data, mime_type=mime)),
    ]))])


class TtsProviderTests(unittest.TestCase):
    def test_google_default_uses_vertex_adc_and_returns_raw_24k_pcm(self):
        captured = {}
        def generate(**kwargs):
            captured.update(kwargs)
            return audio_response()
        fake_client = SimpleNamespace(models=SimpleNamespace(generate_content=generate))
        with patch.object(config, "TTS_ENABLED", True), patch.object(config, "TTS_PROVIDER", "google"), patch.object(
            config, "GCP_PROJECT", "fixture-project"
        ), patch.object(config, "GOOGLE_TTS_LOCATION", "global"), patch.object(tts, "_google_client", None), patch.object(
            tts.genai, "Client", return_value=fake_client
        ) as client, patch.object(tts, "_get_client", side_effect=AssertionError("Google mode touched ElevenLabs")):
            pcm, rate, channels, elapsed = tts.synthesize("What happened next?", "impatient")
        self.assertEqual((pcm, rate, channels), (b"\x01\x00\x02\x00", 24000, 1))
        self.assertGreaterEqual(elapsed, 0)
        self.assertTrue(client.call_args.kwargs["vertexai"])
        self.assertEqual(client.call_args.kwargs["project"], "fixture-project")
        self.assertEqual(client.call_args.kwargs["location"], "global")
        self.assertIsNone(client.call_args.kwargs.get("api_key"))
        self.assertEqual(captured["model"], "gemini-2.5-flash-tts")
        self.assertEqual(captured["config"].speech_config.voice_config.prebuilt_voice_config.voice_name, "Charon")
        self.assertEqual(captured["config"].speech_config.language_code, "en-US")
        self.assertIn("Russian accent", captured["contents"])
        self.assertIn("impatient", captured["contents"])
        self.assertTrue(captured["contents"].endswith("What happened next?"))

    def test_google_failure_never_switches_to_expired_elevenlabs(self):
        fake_client = SimpleNamespace(models=SimpleNamespace(generate_content=lambda **_kw: (_ for _ in ()).throw(PermissionError("fixture permission denied"))))
        with patch.object(config, "TTS_ENABLED", True), patch.object(config, "TTS_PROVIDER", "google"), patch.object(
            tts, "_get_google_client", return_value=fake_client
        ), patch.object(tts, "_get_client", side_effect=AssertionError("provider switched")):
            with self.assertRaises(PermissionError):
                tts.synthesize("Tell me about the door.")

    def test_google_empty_or_wrong_encoding_is_rejected_not_faked(self):
        for response in (SimpleNamespace(candidates=[]), audio_response(b"\x00"), audio_response(b"RIFF\x00\x00"), audio_response(mime="audio/mpeg")):
            with self.subTest(response=response), patch.object(tts, "_get_google_client", return_value=SimpleNamespace(models=SimpleNamespace(generate_content=lambda **_kw: response))):
                with self.assertRaises(ValueError):
                    tts._synthesize_google("What do you remember?", None)

    def test_explicit_none_does_not_call_any_provider(self):
        with patch.object(config, "TTS_ENABLED", False), patch.object(config, "TTS_PROVIDER", "none"), patch.object(
            tts, "_get_google_client", side_effect=AssertionError("disabled Google called")
        ), patch.object(tts, "_get_client", side_effect=AssertionError("disabled ElevenLabs called")):
            self.assertEqual(tts.synthesize("What happened?"), (b"", 0, 0, 0))

    def test_legacy_elevenlabs_is_only_used_when_explicitly_selected(self):
        fake_client = object()
        with patch.object(config, "TTS_ENABLED", True), patch.object(config, "TTS_PROVIDER", "elevenlabs"), patch.object(
            tts, "_get_client", return_value=fake_client
        ), patch.object(tts, "_fetch_audio", return_value=(b"\x00\x00", 24000, 1)) as synth, patch.object(
            tts, "_get_google_client", side_effect=AssertionError("legacy mode touched Google")
        ):
            self.assertEqual(tts.synthesize("Your account?", None)[:3], (b"\x00\x00", 24000, 1))
        self.assertIs(synth.call_args.args[0], fake_client)

    def test_provider_failure_reason_does_not_expose_exception_body(self):
        error = RuntimeError("fixture-secret-that-must-not-be-logged")
        for field in ("status_code", "code"):
            with self.subTest(provider_status_field=field):
                error = RuntimeError("fixture-secret-that-must-not-be-logged")
                setattr(error, field, 403)
                self.assertEqual(tts.failure_reason(error), "provider_http_403")
        self.assertEqual(tts.failure_reason(TimeoutError("fixture-secret")), "provider_timeout")


if __name__ == "__main__":
    unittest.main()
