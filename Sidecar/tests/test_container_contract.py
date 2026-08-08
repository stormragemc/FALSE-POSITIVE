"""Static deployment contract for the Cloud Run container."""

from pathlib import Path
import unittest


SIDECAR_DIR = Path(__file__).resolve().parents[1]


class ContainerContractTests(unittest.TestCase):
    def test_dockerfile_has_cloud_run_runtime_and_cpu_hubert_dependencies(self):
        path = SIDECAR_DIR / "Dockerfile"
        self.assertTrue(path.exists(), "the Cloud Run Dockerfile must exist")
        dockerfile = path.read_text(encoding="utf-8")

        self.assertIn("FROM python:3.12-slim", dockerfile)
        self.assertIn("ffmpeg", dockerfile)
        self.assertIn("https://download.pytorch.org/whl/cpu", dockerfile)
        self.assertIn("ARG HUBERT_MODEL_ID=superb/hubert-base-superb-er", dockerfile)
        self.assertIn("ARG HUBERT_MODEL_REVISION=9a456581e0147a2b7fdaf56d77a9e8fce3865eaa", dockerfile)
        self.assertIn("ENV HUBERT_MODEL_ID=${HUBERT_MODEL_ID}", dockerfile)
        self.assertIn("HUBERT_LOCAL_FILES_ONLY=true", dockerfile)
        self.assertIn("os.environ['HUBERT_MODEL_ID']", dockerfile)
        self.assertIn("os.environ['HUBERT_MODEL_REVISION']", dockerfile)
        self.assertIn("use_safetensors=True", dockerfile)
        self.assertLess(
            dockerfile.index("AutoFeatureExtractor.from_pretrained"),
            dockerfile.index("COPY . ."),
        )
        self.assertIn("uvicorn app:app --host 0.0.0.0 --port ${PORT}", dockerfile)
        self.assertIn(
            'CMD ["sh", "-c", "exec uvicorn app:app --host 0.0.0.0 --port ${PORT}"]',
            dockerfile,
        )

    def test_docker_context_excludes_secrets_tests_and_local_artifacts(self):
        path = SIDECAR_DIR / ".dockerignore"
        self.assertTrue(path.exists(), "the Docker context needs an explicit ignore file")
        ignored = path.read_text(encoding="utf-8").splitlines()

        self.assertIn(".env", ignored)
        self.assertIn("tests/", ignored)
        self.assertIn("tools/", ignored)
        self.assertIn("**/__pycache__/", ignored)

    def test_the_unsupported_detail_prompt_is_shipped_in_the_image(self):
        """G3 keeps this prompt in a file rather than a Python literal, which
        means the image has to carry it. A future 'trim the context' commit
        adding prompts/ to .dockerignore would disable §7 detection in
        production only — every offline test would still pass."""
        prompt = SIDECAR_DIR / "prompts" / "unsupported_detail_judge.txt"
        self.assertTrue(prompt.exists(), "the §7 judge prompt must exist")

        ignored = (SIDECAR_DIR / ".dockerignore").read_text(encoding="utf-8").splitlines()
        for line in ignored:
            self.assertNotIn(
                "prompts",
                line,
                "prompts/ must stay in the Docker context — see this test's docstring",
            )

    def test_windows_launcher_warning_names_current_required_configuration(self):
        launcher = (SIDECAR_DIR / "run_sidecar.bat").read_text(encoding="utf-8")

        self.assertIn("GCP_PROJECT", launcher)
        self.assertIn("FP_CLIENT_KEY", launcher)
        self.assertNotIn("GEMINI_API_KEY", launcher)

    def test_diagnostic_probes_follow_the_cloud_adapters(self):
        stt_probe = (SIDECAR_DIR / "tools/probe_stt_ser.py").read_text(encoding="utf-8")
        llm_probe = (SIDECAR_DIR / "tools/probe_llm.py").read_text(encoding="utf-8")

        self.assertIn("asyncio.run(stt.transcribe(raw_bytes))", stt_probe)
        self.assertIn("Google Cloud Speech-to-Text", stt_probe)
        self.assertNotIn("faster-whisper small.en", stt_probe)
        self.assertIn("vertexai=True", llm_probe)
        self.assertIn("project=config.GCP_PROJECT", llm_probe)
        self.assertIn("location=config.GCP_LOCATION", llm_probe)


if __name__ == "__main__":
    unittest.main()
