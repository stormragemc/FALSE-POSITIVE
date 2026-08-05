"""Shared-secret gate for the public backend.

The key ships inside the Unity build and is therefore extractable by anyone
who cares to look. It stops drive-by traffic against a public URL; it is not
a security boundary. The real cost protection is limits.py.
"""

import hmac


CLIENT_KEY_HEADER = "x-fp-client-key"


def is_authorized(supplied: str | None, expected: str) -> bool:
    """Fails closed: an unconfigured server rejects every request."""
    if not expected:
        return False
    return hmac.compare_digest(supplied or "", expected)
