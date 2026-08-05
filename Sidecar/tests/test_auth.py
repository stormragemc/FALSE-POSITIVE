"""Client-key authentication policy tests."""

import importlib
import importlib.util
import unittest


class ClientKeyAuthTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.find_spec("auth")
        if spec is None:
            raise unittest.SkipTest("auth module does not exist yet")
        cls.auth = importlib.import_module("auth")

    def test_matching_client_key_is_authorized(self):
        self.assertTrue(self.auth.is_authorized("expected", "expected"))

    def test_missing_or_wrong_client_key_is_rejected(self):
        self.assertFalse(self.auth.is_authorized(None, "expected"))
        self.assertFalse(self.auth.is_authorized("wrong", "expected"))

    def test_blank_server_key_fails_closed(self):
        self.assertFalse(self.auth.is_authorized("anything", ""))

    def test_whitespace_server_key_fails_closed(self):
        self.assertFalse(self.auth.is_authorized("   ", "   "))

    def test_non_ascii_client_key_is_rejected_without_raising(self):
        try:
            authorized = self.auth.is_authorized("é", "expected")
        except TypeError as exc:
            self.fail(f"malformed client key raised instead of being rejected: {exc}")
        self.assertFalse(authorized)


class AuthModuleAvailabilityTests(unittest.TestCase):
    def test_auth_module_exists(self):
        self.assertIsNotNone(
            importlib.util.find_spec("auth"),
            "the backend needs an auth module before protected routes can ship",
        )


if __name__ == "__main__":
    unittest.main()
