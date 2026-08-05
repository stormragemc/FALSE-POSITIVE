from pathlib import Path
import re
import unittest
from typing import get_origin, get_type_hints

from prosody import ProsodySignal
import ser


DTO_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Scripts/Net/SidecarDtos.cs"
)
UNITY_CONFIG_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Scripts/Core/InterrogationConfig.cs"
)
UNITY_CONFIG_ASSET_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Config/InterrogationConfig.asset"
)
UNITY_CLIENT_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Scripts/Net/InterrogationSidecarClient.cs"
)
PCM_UTILITY_PATH = (
    Path(__file__).resolve().parents[2]
    / "Assets/_Project/Scripts/Audio/PcmUtility.cs"
)


def _class_fields(source: str, class_name: str) -> dict[str, str]:
    match = re.search(rf"public\s+sealed\s+class\s+{re.escape(class_name)}\b", source)
    if match is None:
        raise AssertionError(f"missing C# DTO class {class_name}")
    opening = source.find("{", match.end())
    depth = 0
    closing = -1
    for index in range(opening, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                closing = index
                break
    if opening < 0 or closing < 0:
        raise AssertionError(f"unbalanced C# DTO class {class_name}")
    body = source[opening + 1 : closing]
    return {
        field_name: field_type
        for field_type, field_name in re.findall(
            r"public\s+([A-Za-z0-9_\[\]]+)\s+([A-Za-z0-9_]+)\s*;", body
        )
    }


def _prosody_csharp_types() -> dict[str, str]:
    result = {}
    for name, annotation in get_type_hints(ProsodySignal).items():
        origin = get_origin(annotation)
        if annotation is str:
            result[name] = "string"
        elif annotation is bool:
            result[name] = "bool"
        elif annotation is int:
            result[name] = "int"
        elif annotation is float:
            result[name] = "float"
        elif origin is dict:
            result[name] = "SidecarClassProbabilities"
        elif origin is list:
            result[name] = "string[]"
        else:
            raise AssertionError(f"unmapped Python prosody type: {name}={annotation}")
    return result


class UnityContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = DTO_PATH.read_text(encoding="utf-8")

    def test_unity_prosody_dto_matches_sidecar_json_types(self):
        self.assertEqual(
            _class_fields(self.source, "SidecarProsodySignal"),
            _prosody_csharp_types(),
        )

    def test_turn_response_explicitly_nests_prosody_dto(self):
        response_fields = _class_fields(self.source, "SidecarTurnResponse")
        self.assertEqual(response_fields.get("prosody"), "SidecarProsodySignal")

    def test_unity_probability_dto_covers_checkpoint_labels_with_float_types(self):
        self.assertEqual(
            _class_fields(self.source, "SidecarClassProbabilities"),
            {label: "float" for label in ser.SUPPORTED_EMOTION_LABELS},
        )

    def test_committed_unity_asset_serializes_hosted_fields_without_live_values(self):
        asset = UNITY_CONFIG_ASSET_PATH.read_text(encoding="utf-8")

        self.assertRegex(asset, r"(?m)^  backendBaseUrl:\s*$")
        self.assertRegex(asset, r"(?m)^  backendClientKey:\s*$")
        self.assertRegex(asset, r"(?m)^  autoLaunchSidecar: 0$")

    def test_local_fallback_defaults_to_the_docker_port(self):
        source = UNITY_CONFIG_PATH.read_text(encoding="utf-8")
        asset = UNITY_CONFIG_ASSET_PATH.read_text(encoding="utf-8")

        self.assertIn("public int sidecarPort = 8080;", source)
        self.assertRegex(asset, r"(?m)^  sidecarPort: 8080$")

    def test_unity_client_attaches_the_configured_key(self):
        source = UNITY_CLIENT_PATH.read_text(encoding="utf-8")

        self.assertIn('request.SetRequestHeader("X-FP-Client-Key", config.backendClientKey);', source)
        self.assertIn("if (!string.IsNullOrEmpty(config.backendClientKey))", source)

    def test_remote_backend_requires_https_but_loopback_http_is_allowed(self):
        config_source = UNITY_CONFIG_PATH.read_text(encoding="utf-8")
        client_source = UNITY_CLIENT_PATH.read_text(encoding="utf-8")

        self.assertIn("IsBackendUrlAllowed", config_source)
        self.assertIn("Uri.UriSchemeHttps", config_source)
        self.assertIn("uri.IsLoopback", config_source)
        self.assertIn("if (!config.IsBackendUrlAllowed)", client_source)

    def test_unity_resamples_microphone_audio_to_the_canonical_upload_rate(self):
        client_source = UNITY_CLIENT_PATH.read_text(encoding="utf-8")
        pcm_source = PCM_UTILITY_PATH.read_text(encoding="utf-8")

        self.assertIn("private const int BackendSampleRate = 16000;", client_source)
        self.assertIn("int uploadSampleRate = BackendSampleRate;", client_source)
        self.assertIn(
            "PcmUtility.ResampleMono(pcmSamples, sampleRate, uploadSampleRate)",
            client_source,
        )
        self.assertIn(
            'new MultipartFormDataSection("sample_rate", uploadSampleRate.ToString())',
            client_source,
        )
        self.assertIn("public static float[] ResampleMono(", pcm_source)


if __name__ == "__main__":
    unittest.main()
