"""A dependency-stubbed turn test for the sidecar's optional HuBERT boundary."""

import asyncio
import base64
from contextlib import redirect_stdout
import importlib.util
import io
from pathlib import Path
import sys
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import patch

import numpy as np

from tests.test_unity_contract import DTO_PATH, _class_fields


class _FakeFastAPI:
    def __init__(self, **_kwargs):
        pass

    def get(self, _path):
        return lambda function: function

    def post(self, _path):
        return lambda function: function

    def middleware(self, _kind):
        return lambda function: function


class _FakeJSONResponse:
    def __init__(self, status_code, content):
        self.status_code = status_code
        self.content = content


class _FakeUpload:
    def __init__(self, data: bytes):
        self.data = data

    async def read(self, size: int = -1) -> bytes:
        return self.data if size < 0 else self.data[:size]


def _module(name: str, **attributes) -> ModuleType:
    result = ModuleType(name)
    for key, value in attributes.items():
        setattr(result, key, value)
    return result


class AppFailureIsolationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        fastapi = _module(
            "fastapi",
            FastAPI=_FakeFastAPI,
            File=lambda default=None: default,
            Form=lambda default=None: default,
            UploadFile=object,
        )
        fastapi_responses = _module("fastapi.responses", JSONResponse=_FakeJSONResponse)
        config = _module(
            "config",
            validate=lambda: None,
            SIDECAR_MAX_SESSIONS=4,
            PROSODY_BASELINE_TURNS=3,
            PROSODY_MIN_CONFIDENCE=0.4,
            PROSODY_ENABLED=True,
            HUBERT_MODEL_ID="test/hubert",
            HUBERT_MAX_SECONDS=20.0,
            SIDECAR_MAX_AUDIO_SECONDS=30.0,
            HOST="127.0.0.1",
            PORT=8765,
            GCP_PROJECT="test-project",
            GCP_LOCATION="global",
            STT_MODEL="short",
            STT_LANGUAGE="en-US",
            FP_CLIENT_KEY="test-key",
            MAX_TURNS_PER_SESSION=40,
            MAX_TURNS_PER_DAY=2000,
        )
        cls.captured_signals = []

        def generate_reply(**kwargs):
            cls.captured_signals.append(kwargs["prosody_signal"])
            return "Next question.", 3

        llm = _module(
            "llm",
            OPENING_KICKOFF_TEXT="opening",
            HISTORY_KIND_SCENE="scene_instruction",
            HISTORY_KIND_WITNESS="witness_transcript",
            generate_reply=generate_reply,
        )
        async def fake_transcribe(_pcm_bytes):
            return "fixture transcript", 5

        stt = _module("stt", transcribe=fake_transcribe)

        def failing_hubert(_audio):
            raise RuntimeError("forced HuBERT failure")

        ser = _module(
            "ser",
            load=lambda: None,
            analyze=failing_hubert,
            device=lambda: "cpu",
        )
        tts = _module(
            "tts",
            synthesize=lambda _text: (b"\x00\x00", 24000, 1, 7),
        )
        audio_utils = _module(
            "audio_utils",
            pcm16_bytes_to_float32=lambda data: (
                np.frombuffer(data, dtype="<i2").astype(np.float32) / 32768.0
            ),
            resample_float32=lambda audio, _source, _target: audio,
            normalize_to_canonical=lambda pcm, rate, _channels: (pcm, rate),
            float32_to_pcm16_bytes=lambda audio: (
                (np.clip(audio, -1.0, 1.0) * 32767.0).astype("<i2").tobytes()
            ),
        )
        uvicorn = _module("uvicorn", run=lambda *_args, **_kwargs: None)

        stubs = {
            "audio_utils": audio_utils,
            "config": config,
            "fastapi": fastapi,
            "fastapi.responses": fastapi_responses,
            "llm": llm,
            "ser": ser,
            "stt": stt,
            "tts": tts,
            "uvicorn": uvicorn,
        }
        path = Path(__file__).resolve().parents[1] / "app.py"
        spec = importlib.util.spec_from_file_location("sidecar_app_under_test", path)
        module = importlib.util.module_from_spec(spec)
        with patch.dict(sys.modules, stubs):
            spec.loader.exec_module(module)
        cls.app = module
        cls.app._prosody_model_available = True

    @classmethod
    def tearDownClass(cls):
        cls.app._ser_pool.shutdown(wait=True)

    def setUp(self):
        self.app._session_store.clear()
        self.app._prosody_registry = self.app.prosody.ProsodyRegistry(4, 3, 0.4)
        self.app._session_reset_epoch = 0
        self.captured_signals.clear()

    def test_hubert_exception_degrades_to_transcript_only_turn(self):
        sample_count = 16000 * 2
        pcm = (np.sin(np.arange(sample_count) * 0.1) * 8000).astype("<i2").tobytes()

        result = asyncio.run(self.app.turn("session-a", 16000, 450, _FakeUpload(pcm)))

        self.assertTrue(result["ok"])
        self.assertEqual(result["transcript"], "fixture transcript")
        self.assertEqual(result["emotion"], "")
        self.assertFalse(result["prosody"]["available"])
        self.assertEqual(
            result["prosody"]["reliability_reason"], "hubert_inference_failed"
        )
        self.assertEqual(result["prosody"]["onset_delay_ms"], 450)
        self.assertEqual(base64.b64decode(result["audio_b64"]), b"\x00\x00")
        self.assertEqual(len(self.captured_signals), 1)
        self.assertFalse(self.captured_signals[0].available)

    def test_disabled_and_load_failed_affect_modes_are_explicit(self):
        audio = np.zeros(16000 * 2, dtype=np.float32)
        with patch.object(self.app.config, "PROSODY_ENABLED", False):
            _features, observation, reason = self.app._analyze_affect(audio)
        self.assertIsNone(observation)
        self.assertEqual(reason, "prosody_disabled")

        self.app._prosody_model_available = False
        try:
            _features, observation, reason = self.app._analyze_affect(audio)
        finally:
            self.app._prosody_model_available = True
        self.assertIsNone(observation)
        self.assertEqual(reason, "hubert_load_failed")

    def test_health_reports_loading_while_enabled_model_is_not_ready(self):
        previous = (
            self.app._models_loaded,
            self.app._prosody_model_available,
            self.app._prosody_load_error,
        )
        self.app._models_loaded = False
        self.app._prosody_model_available = False
        self.app._prosody_load_error = "disabled"
        try:
            result = self.app.health()
        finally:
            (
                self.app._models_loaded,
                self.app._prosody_model_available,
                self.app._prosody_load_error,
            ) = previous

        self.assertTrue(result["prosody"]["enabled"])
        self.assertFalse(result["prosody"]["available"])
        self.assertEqual(result["prosody"]["error"], "loading")

    def test_unity_turn_and_health_dtos_match_live_python_response_keys(self):
        source = DTO_PATH.read_text(encoding="utf-8")
        turn_fields = _class_fields(source, "SidecarTurnResponse")
        health_fields = _class_fields(source, "SidecarHealthResponse")
        prosody_health_fields = _class_fields(source, "SidecarProsodyHealth")

        self.assertEqual(set(turn_fields), set(self.app._empty_response()))
        health_payload = self.app.health()
        self.assertEqual(set(health_fields), set(health_payload))
        self.assertEqual(set(prosody_health_fields), set(health_payload["prosody"]))

    def test_reset_clears_dialogue_and_prosody_state(self):
        self.app._session_store.commit("session-a", [{"role": "user", "content": "hello"}])
        original_tracker = self.app._prosody_registry.get("session-a")

        result = asyncio.run(self.app.session_reset("session-a"))

        self.assertEqual(result, {"ok": True})
        self.assertEqual(self.app._session_store.history("session-a"), [])
        self.assertIsNot(self.app._prosody_registry.get("session-a"), original_tracker)

    def test_invalid_pcm_does_not_create_session_state(self):
        response = asyncio.run(
            self.app.turn("session-a", 16000, 0, _FakeUpload(b"\x00"))
        )

        self.assertEqual(response.status_code, 400)
        self.assertEqual(self.app._session_store.history("session-a"), [])

    def test_internal_value_error_is_not_exposed_as_client_input_error(self):
        with patch.object(
            self.app.audio_utils,
            "pcm16_bytes_to_float32",
            side_effect=ValueError("internal decoder detail"),
        ):
            response = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(b"\x00\x00"))
            )

        self.assertEqual(response.status_code, 500)
        self.assertEqual(response.content["error"], "turn pipeline failed; retry the utterance")
        self.assertNotIn("internal decoder detail", response.content["error"])

    def test_stt_failure_fails_turn_without_committing_affect(self):
        pcm = (np.sin(np.arange(16000 * 2) * 0.1) * 8000).astype("<i2").tobytes()
        output = io.StringIO()
        with redirect_stdout(output), patch.object(
            self.app.stt, "transcribe", side_effect=RuntimeError("forced STT failure")
        ):
            response = asyncio.run(self.app.turn("session-a", 16000, 0, _FakeUpload(pcm)))

        self.assertEqual(response.status_code, 500)
        self.assertEqual(response.content["error"], "turn pipeline failed; retry the utterance")
        self.assertEqual(self.app._prosody_registry.get("session-a").reference_count, 0)
        self.assertEqual(self.app._session_store.history("session-a"), [])
        self.assertIn("forced STT failure", output.getvalue())

    def test_classical_feature_failure_degrades_to_transcript_only_turn(self):
        pcm = (np.sin(np.arange(16000 * 2) * 0.1) * 8000).astype("<i2").tobytes()
        with patch.object(
            self.app.features_classical,
            "extract",
            side_effect=RuntimeError("forced feature failure"),
        ):
            result = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
            )

        self.assertTrue(result["ok"])
        self.assertFalse(result["prosody"]["available"])
        self.assertEqual(result["prosody"]["reliability_reason"], "feature_extraction_failed")
        self.assertIn("feature_extraction_failed", result["prosody"]["flags"])

    def test_history_eviction_resets_matching_prosody_tracker(self):
        original_tracker = self.app._prosody_registry.get("session-a")
        # The cap is fixed when the store is constructed, so express it by
        # building a store rather than by patching config after the fact.
        store = self.app.session_store.InMemorySessionStore(2)
        with patch.object(self.app, "_session_store", store):
            asyncio.run(self.app.turn("session-a", 16000, 0, None))
            asyncio.run(self.app.turn("session-b", 16000, 0, None))
            asyncio.run(self.app.turn("session-c", 16000, 0, None))

        self.assertEqual(store.history("session-a"), [])
        self.assertIsNot(self.app._prosody_registry.get("session-a"), original_tracker)

    def test_opening_history_uses_shared_scene_kind(self):
        result = asyncio.run(self.app.turn("session-a", 16000, 0, None))

        self.assertTrue(result["ok"])
        self.assertEqual(
            self.app._session_store.history("session-a")[0]["kind"],
            self.app.llm.HISTORY_KIND_SCENE,
        )

    def test_tts_failure_does_not_commit_prosody_reference_state(self):
        sample_count = 16000 * 2
        pcm = (np.sin(np.arange(sample_count) * 0.1) * 8000).astype("<i2").tobytes()
        observation = SimpleNamespace(
            label="neutral",
            confidence=0.82,
            probabilities={"neutral": 0.82, "happy": 0.06, "angry": 0.07, "sad": 0.05},
            normalized_entropy=0.40,
            top_two_margin=0.75,
            embedding=np.asarray([1.0, 0.0, 0.0, 0.0], dtype=np.float32),
            frame_instability=0.10,
            elapsed_ms=12,
            model_id="test/hubert",
        )

        with patch.object(self.app.ser, "analyze", return_value=observation), patch.object(
            self.app.tts, "synthesize", side_effect=RuntimeError("forced TTS failure")
        ):
            response = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
            )

        self.assertEqual(response.status_code, 500)
        self.assertEqual(self.app._prosody_registry.get("session-a").reference_count, 0)
        self.assertEqual(self.app._session_store.history("session-a"), [])

    def test_failed_new_session_does_not_evict_live_session_state(self):
        # A single-slot store, already full: a failing turn must not evict the
        # live session to make room for one that never completes.
        store = self.app.session_store.InMemorySessionStore(1)
        store.commit("live-session", [{"role": "user", "content": "hello"}])
        self.app._prosody_registry = self.app.prosody.ProsodyRegistry(1, 3, 0.4)
        live_tracker = self.app._prosody_registry.get("live-session")
        pcm = (np.sin(np.arange(16000 * 2) * 0.1) * 8000).astype("<i2").tobytes()

        with patch.object(self.app, "_session_store", store), patch.object(
            self.app.tts, "synthesize", side_effect=RuntimeError("forced TTS failure")
        ):
            response = asyncio.run(
                self.app.turn("failed-session", 16000, 0, _FakeUpload(pcm))
            )

        self.assertEqual(response.status_code, 500)
        self.assertNotEqual(store.history("live-session"), [])
        self.assertEqual(store.history("failed-session"), [])
        self.assertIs(self.app._prosody_registry.get("live-session"), live_tracker)

    def test_successful_turn_commits_prosody_reference_once(self):
        sample_count = 16000 * 2
        pcm = (np.sin(np.arange(sample_count) * 0.1) * 8000).astype("<i2").tobytes()
        observation = SimpleNamespace(
            label="neutral",
            confidence=0.82,
            probabilities={"neutral": 0.82, "happy": 0.06, "angry": 0.07, "sad": 0.05},
            normalized_entropy=0.40,
            top_two_margin=0.75,
            embedding=np.asarray([1.0, 0.0, 0.0, 0.0], dtype=np.float32),
            frame_instability=0.10,
            elapsed_ms=12,
            model_id="test/hubert",
        )

        with patch.object(self.app.ser, "analyze", return_value=observation):
            result = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
            )

        self.assertTrue(result["ok"])
        self.assertEqual(self.app._prosody_registry.get("session-a").reference_count, 1)
        self.assertEqual(result["prosody"]["reference_turns"], 1)
        self.assertEqual(len(self.app._session_store.history("session-a")), 2)
        source = DTO_PATH.read_text(encoding="utf-8")
        self.assertEqual(set(result), set(_class_fields(source, "SidecarTurnResponse")))

    def test_failed_second_turn_does_not_mutate_registered_tracker_or_history(self):
        sample_count = 16000 * 2
        pcm = (np.sin(np.arange(sample_count) * 0.1) * 8000).astype("<i2").tobytes()
        observation = SimpleNamespace(
            label="neutral",
            confidence=0.82,
            probabilities={"neutral": 0.82, "happy": 0.06, "angry": 0.07, "sad": 0.05},
            normalized_entropy=0.40,
            top_two_margin=0.75,
            embedding=np.asarray([1.0, 0.0, 0.0, 0.0], dtype=np.float32),
            frame_instability=0.10,
            elapsed_ms=12,
            model_id="test/hubert",
        )
        with patch.object(self.app.ser, "analyze", return_value=observation):
            first = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
            )
            with patch.object(
                self.app.tts, "synthesize", side_effect=RuntimeError("forced TTS failure")
            ):
                second = asyncio.run(
                    self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
                )

        self.assertTrue(first["ok"])
        self.assertEqual(second.status_code, 500)
        self.assertEqual(self.app._prosody_registry.get("session-a").reference_count, 1)
        self.assertEqual(len(self.app._session_store.history("session-a")), 2)

    def test_affect_and_classical_features_use_same_bounded_audio_window(self):
        sample_count = 16000 * 2
        pcm = (np.sin(np.arange(sample_count) * 0.1) * 8000).astype("<i2").tobytes()
        observed_lengths = {}
        real_extract = self.app.features_classical.extract

        def capture_features(audio, sample_rate):
            observed_lengths["features"] = len(audio)
            return real_extract(audio, sample_rate)

        def capture_hubert(audio):
            observed_lengths["hubert"] = len(audio)
            raise RuntimeError("forced HuBERT failure after length capture")

        with patch.object(self.app.config, "HUBERT_MAX_SECONDS", 1.0), patch.object(
            self.app.features_classical, "extract", side_effect=capture_features
        ), patch.object(self.app.ser, "analyze", side_effect=capture_hubert):
            result = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
            )

        self.assertTrue(result["ok"])
        self.assertEqual(observed_lengths, {"features": 16000, "hubert": 16000})
        self.assertEqual(result["prosody"]["duration_seconds"], 2.0)
        self.assertIn("affect_window_truncated", result["prosody"]["flags"])

    def test_reset_during_in_flight_turn_prevents_stale_state_reinsert(self):
        sample_count = 16000 * 2
        pcm = (np.sin(np.arange(sample_count) * 0.1) * 8000).astype("<i2").tobytes()
        observation = SimpleNamespace(
            label="neutral",
            confidence=0.82,
            probabilities={"neutral": 0.82, "happy": 0.06, "angry": 0.07, "sad": 0.05},
            normalized_entropy=0.40,
            top_two_margin=0.75,
            embedding=np.asarray([1.0, 0.0, 0.0, 0.0], dtype=np.float32),
            frame_instability=0.10,
            elapsed_ms=12,
            model_id="test/hubert",
        )
        with patch.object(self.app.ser, "analyze", return_value=observation):
            first = asyncio.run(
                self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
            )
            stt_started = asyncio.Event()
            release_stt = asyncio.Event()

            async def blocking_stt(_pcm_bytes):
                # Must yield the loop rather than block it: transcribe is now
                # awaited directly instead of running in a thread pool, so a
                # synchronous wait here would freeze the reset it races with
                # and the test would pass without exercising the race at all.
                stt_started.set()
                await asyncio.wait_for(release_stt.wait(), timeout=2.0)
                return "fixture transcript", 5

            async def reset_while_turn_waits():
                turn_task = asyncio.create_task(
                    self.app.turn("session-a", 16000, 0, _FakeUpload(pcm))
                )
                await stt_started.wait()
                await self.app.session_reset("session-a")
                release_stt.set()
                return await turn_task

            with patch.object(self.app.stt, "transcribe", side_effect=blocking_stt):
                second = asyncio.run(reset_while_turn_waits())

        self.assertTrue(first["ok"])
        self.assertTrue(second["ok"])
        self.assertEqual(self.app._session_store.history("session-a"), [])
        self.assertEqual(self.app._prosody_registry.get("session-a").reference_count, 0)


if __name__ == "__main__":
    unittest.main()
