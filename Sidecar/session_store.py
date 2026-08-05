"""Conversation-history storage for the interrogation backend.

The backend runs pinned to a single Cloud Run instance, so in-memory state is
correct. This interface exists so that assumption is replaceable: a Firestore
implementation with the same four methods drops in without touching app.py.
"""

from collections import OrderedDict
import time
from typing import Callable


class InMemorySessionStore:
    """History keyed by the GUID Unity mints at scene start, LRU-evicted."""

    def __init__(
        self,
        max_sessions: int,
        idle_ttl_seconds: float = 3600.0,
        clock: Callable[[], float] = time.monotonic,
    ):
        self._max_sessions = max(1, int(max_sessions))
        self._idle_ttl_seconds = max(1.0, float(idle_ttl_seconds))
        self._clock = clock
        self._sessions: OrderedDict[str, list[dict]] = OrderedDict()
        self._last_access: dict[str, float] = {}

    def expire_idle(self) -> list[str]:
        """Delete and return sessions idle beyond the configured retention limit."""
        cutoff = self._clock() - self._idle_ttl_seconds
        expired = [
            session_id
            for session_id, last_access in self._last_access.items()
            if last_access <= cutoff
        ]
        for session_id in expired:
            self._sessions.pop(session_id, None)
            self._last_access.pop(session_id, None)
        return expired

    def history(self, session_id: str) -> list[dict]:
        """Never creates the session — a read of an unknown id is empty."""
        self.expire_idle()
        if session_id not in self._sessions:
            return []
        self._last_access[session_id] = self._clock()
        return self._sessions[session_id]

    def commit(self, session_id: str, history: list[dict]) -> list[str]:
        """Store history and mark the session most-recently-used.

        Returns the ids evicted to stay under the cap, so the caller can drop
        the matching prosody state in the same breath.
        """
        evicted = self.expire_idle()
        self._sessions.pop(session_id, None)
        self._sessions[session_id] = history
        self._last_access[session_id] = self._clock()
        while len(self._sessions) > self._max_sessions:
            evicted_id, _history = self._sessions.popitem(last=False)
            self._last_access.pop(evicted_id, None)
            evicted.append(evicted_id)
        return evicted

    def reset(self, session_id: str) -> None:
        self._sessions.pop(session_id, None)
        self._last_access.pop(session_id, None)

    def clear(self) -> None:
        self._sessions.clear()
        self._last_access.clear()
