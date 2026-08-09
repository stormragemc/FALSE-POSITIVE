"""Static contract checks for the browser-playable fallback."""

from pathlib import Path
from html.parser import HTMLParser
import re
import unittest


WEB_DIR = Path(__file__).resolve().parents[1] / "web"


class ElementAttributeParser(HTMLParser):
    """Collect attributes for the handful of stable UI ids in the static shell."""

    def __init__(self):
        super().__init__()
        self.by_id = {}

    def handle_starttag(self, _tag, attrs):
        attributes = dict(attrs)
        if element_id := attributes.get("id"):
            self.by_id[element_id] = attributes


class FallbackWebContractTests(unittest.TestCase):
    def setUp(self):
        self.index = (WEB_DIR / "index.html").read_text(encoding="utf-8")
        self.styles = (WEB_DIR / "styles.css").read_text(encoding="utf-8")
        self.app = (WEB_DIR / "app.js").read_text(encoding="utf-8")

    def test_site_has_no_package_or_remote_asset_dependency(self):
        self.assertIn('<link rel="stylesheet" href="./styles.css">', self.index)
        self.assertIn('<script src="./app.js" defer></script>', self.index)
        self.assertNotRegex(self.index, r'<(?:script|link)[^>]+(?:src|href)="https?://')
        self.assertTrue((WEB_DIR / "README.md").exists())

    def test_every_canonical_cutscene_variant_has_one_blank_iframe_definition(self):
        expected = {
            *(f"CS-{number:02d}" for number in range(1, 16)),
            "CS-16A", "CS-16B",
            "CS-17A", "CS-17B", "CS-17C",
            "CS-18A", "CS-18B", "CS-18C", "CS-18D",
        }
        declared = set(re.findall(r'\{ id: "(CS-[0-9A-Z]+)", scene:', self.app))
        self.assertEqual(declared, expected)
        for cutscene_id in expected:
            self.assertEqual(self.app.count(f'{{ id: "{cutscene_id}", scene:'), 1)
        self.assertIn('iframe.src = source || "about:blank"', self.app)
        self.assertIn("iframe.title =", self.app)

    def test_voice_path_matches_the_sidecar_pcm_contract(self):
        self.assertIn("navigator.mediaDevices.getUserMedia", self.app)
        self.assertIn("new MediaRecorder", self.app)
        self.assertIn("const TARGET_SAMPLE_RATE = 16000", self.app)
        self.assertIn('view.setInt16(index * 2', self.app)
        self.assertIn('form.append("sample_rate", String(TARGET_SAMPLE_RATE))', self.app)
        self.assertIn('form.append("audio"', self.app)
        self.assertIn('"X-FP-Client-Key": key', self.app)
        self.assertIn("const MAX_RECORDING_MS = 19000", self.app)
        self.assertIn("if (payload.session_ended) return payload", self.app)

    def test_client_key_is_tab_scoped_and_no_secret_is_bundled(self):
        self.assertIn("sessionStorage.setItem(SESSION_KEY", self.app)
        self.assertNotIn("false-positive-backend-", self.app)
        self.assertNotRegex(self.app, r'FP_CLIENT_KEY\s*=')

    def test_transcripts_are_session_scoped_and_cutscenes_are_sandboxed(self):
        self.assertIn("delete safeState.transcripts", self.app)
        self.assertIn("sessionStorage.setItem(TRANSCRIPT_SESSION_KEY", self.app)
        self.assertIn('iframe.setAttribute("sandbox"', self.app)
        self.assertIn('url.protocol !== "https:" && !localHttp', self.app)

    def test_responsive_and_reduced_motion_rules_exist(self):
        self.assertIn("@media (max-width: 760px)", self.styles)
        self.assertIn("@media (prefers-reduced-motion: reduce)", self.styles)

    def test_cinematic_shell_starts_with_case_notes_collapsed(self):
        parser = ElementAttributeParser()
        parser.feed(self.index)

        self.assertEqual(parser.by_id["notebook-toggle"]["aria-expanded"], "false")
        self.assertIn("hidden", parser.by_id["notebook-body"])

    def test_cinematic_scene_art_is_packaged_with_the_fallback(self):
        expected_art = {
            "interrogation-room.webp",
            "detective-silhouette.webp",
            "evidence-door.webp",
            "cabin-group.webp",
            "full-cast.webp",
        }
        image_dir = WEB_DIR / "images"
        self.assertEqual({path.name for path in image_dir.glob("*.webp")}, expected_art)
        for filename in expected_art:
            payload = (image_dir / filename).read_bytes()
            self.assertGreater(len(payload), 20_000)
            self.assertEqual(payload[:4], b"RIFF")
            self.assertEqual(payload[8:12], b"WEBP")


if __name__ == "__main__":
    unittest.main()
