"""The client key is a speed bump, not a wall — but it must fail closed."""

import unittest

import auth


class AuthTests(unittest.TestCase):
    def test_matching_key_is_authorized(self):
        self.assertTrue(auth.is_authorized("s3cret", "s3cret"))

    def test_wrong_key_is_rejected(self):
        self.assertFalse(auth.is_authorized("wrong", "s3cret"))

    def test_missing_key_is_rejected(self):
        self.assertFalse(auth.is_authorized("", "s3cret"))
        self.assertFalse(auth.is_authorized(None, "s3cret"))

    def test_unconfigured_server_denies_everything_rather_than_falling_open(self):
        # A deploy that forgot the secret must reject traffic, not serve it.
        self.assertFalse(auth.is_authorized("anything", ""))
        self.assertFalse(auth.is_authorized("", ""))

    def test_header_name_is_lowercase_for_case_insensitive_lookup(self):
        self.assertEqual(auth.CLIENT_KEY_HEADER, auth.CLIENT_KEY_HEADER.lower())


if __name__ == "__main__":
    unittest.main()