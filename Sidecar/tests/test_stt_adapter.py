"""STT adapter contract: the Google backend must keep stt.py's promises.

Google's client is faked so this test needs no network and no credentials —
the same property features_classical relies on.
"""

import asyncio
import sys
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import patch


def _fake_speech_modules(recognize):
    """Build the minimal google.cloud.speech_v2 surface stt.py imports."""

    class _AudioEncoding:
        LINEAR16 = "LINEAR16"

    class _ExplicitDecodingConfig:
        AudioEncoding = _AudioEncoding

        def __init__(self, **kwargs):
            self.kwargs = kwargs

    cloud_speech = ModuleType("google.cloud.speech_v2.types.cloud_speech")
    cloud_speech.ExplicitDecodingConfig = _ExplicitDecodingConfig
    cloud_speech.RecognitionConfig = lambda **kwargs: SimpleNamespace(**kwargs)
    cloud_speech.RecognizeRequest = lambda **kwargs: SimpleNamespace(**kwargs)

    class _SpeechAsyncClient:
        def __init__(self, *_args, **_kwargs):
            pass

        async def recognize(self, request):
            return await recognize(request)

    speech_v2 = ModuleType("google.cloud.speech_v2")
    speech_v2.SpeechAsyncClient = _SpeechAsyncClient
    types_module = ModuleType("google.cloud.speech_v2.types")
    types_module.cloud_speech = cloud_speech
    speech_v2.types = types_module

    # `from google.cloud import speech_v2` resolves the intermediate package
    # first, so faking only the leaves would fall through to the real one --
    # and google-cloud-speech must not need to be installed to run this suite.
    cloud_namespace = ModuleType("google.cloud")
    cloud_namespace.speech_v2 = speech_v2

    return {
        "google.cloud": cloud_namespace,
        "google.cloud.speech_v2": speech_v2,
        "google.cloud.speech_v2.types": types_module,
        "google.cloud.speech_v2.types.cloud_speech": cloud_speech,
    }


def _result(transcript):
    return SimpleNamespace(alternatives=[SimpleNamespace(transcript=transcript)])


def _load_stt(recognize):
    from pathlib import Path
    import importlib.util

    path = Path(__file__).resolve().parents[1] / "stt.py"
    spec = importlib.util.spec_from_file_location("sidecar_stt_under_test", path)
    module = importlib.util.module_from_spec(spec)
    stubs = dict(_fake_speech_modules(recognize))
    stubs["config"] = ModuleType("config")
    stubs["config"].GCP_PROJECT = "test-project"
    stubs["config"].GCP_LOCATION = "global"
    stubs["config"].STT_MODEL = "short"
    stubs["config"].STT_LANGUAGE = "en-US"
    with patch.dict(sys.modules, stubs):
        spec.loader.exec_module(module)
    return module


class SttAdapterTests(unittest.TestCase):
    def test_joins_result_alternatives_into_one_transcript(self):
        async def recognize(_request):
            return SimpleNamespace(results=[_result(" I was "), _result("at home. ")])

        stt = _load_stt(recognize)
        text, elapsed_ms = asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        self.assertEqual(text, "I was at home.")
        self.assertGreaterEqual(elapsed_ms, 0)

    def test_silence_returns_empty_string_not_an_error(self):
        async def recognize(_request):
            return SimpleNamespace(results=[])

        stt = _load_stt(recognize)
        text, _elapsed_ms = asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        self.assertEqual(text, "")

    def test_results_without_alternatives_are_skipped(self):
        async def recognize(_request):
            return SimpleNamespace(results=[SimpleNamespace(alternatives=[]), _result("ok")])

        stt = _load_stt(recognize)
        text, _elapsed_ms = asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        self.assertEqual(text, "ok")

    def test_api_failure_propagates_so_the_turn_handler_can_catch_it(self):
        async def recognize(_request):
            raise RuntimeError("google says no")

        stt = _load_stt(recognize)
        with self.assertRaises(RuntimeError):
            asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

    def test_request_targets_the_configured_project_with_linear16_at_16k(self):
        captured = {}

        async def recognize(request):
            captured["request"] = request
            return SimpleNamespace(results=[])

        stt = _load_stt(recognize)
        asyncio.run(stt.transcribe(b"\x00\x00" * 16000))

        request = captured["request"]
        self.assertEqual(
            request.recognizer,
            "projects/test-project/locations/global/recognizers/_",
        )
        self.assertEqual(request.config.model, "short")
        self.assertEqual(request.config.language_codes, ["en-US"])
        decoding = request.config.explicit_decoding_config
        self.assertEqual(decoding.kwargs["sample_rate_hertz"], 16000)
        self.assertEqual(decoding.kwargs["audio_channel_count"], 1)


if __name__ == "__main__":
    unittest.main()
