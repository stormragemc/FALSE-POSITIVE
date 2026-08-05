"""Small, auditable client-key authentication policy."""

import hmac

CLIENT_KEY_HEADER = "x-fp-client-key"


def is_authorized(provided_key: str | None, expected_key: str) -> bool:
    """Return whether a non-empty supplied key matches the configured key."""
    if not provided_key or not expected_key or not expected_key.strip():
        return False
    return hmac.compare_digest(
        provided_key.encode("utf-8"),
        expected_key.encode("utf-8"),
    )
