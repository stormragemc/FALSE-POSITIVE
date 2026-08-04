"""Conversation-history storage for the interrogation backend.

The backend runs pinned to a single Cloud Run instance, so in-memory state is
correct. This interface exists so that assumption is replaceable: a Firestore
implementation with the same four methods drops in without touching app.py.
"""

from collections import OrderedDict


class InMemorySessionStore:
    """History keyed by the GUID Unity mints at scene start, LRU-evicted."""

    def __init__(self, max_sessions: int):
        self._max_sessions = max(1, int(max_sessions))
        self._sessions: OrderedDict[str, list[dict]] = OrderedDict()

    def history(self, session_id: str) -> list[dict]:
        """Never creates the session — a read of an unknown id is empty."""
        return self._sessions.get(session_id, [])

    def commit(self, session_id: str, history: list[dict]) -> list[str]:
        """Store history and mark the session most-recently-used.

        Returns the ids evicted to stay under the cap, so the caller can drop
        the matching prosody state in the same breath.
        """
        self._sessions.pop(session_id, None)
        self._sessions[session_id] = history
        evicted: list[str] = []
        while len(self._sessions) > self._max_sessions:
            evicted_id, _history = self._sessions.popitem(last=False)
            evicted.append(evicted_id)
        return evicted

    def reset(self, session_id: str) -> None:
        self._sessions.pop(session_id, None)

    def clear(self) -> None:
        self._sessions.clear()
