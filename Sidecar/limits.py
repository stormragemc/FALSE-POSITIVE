"""Turn admission caps, so a runaway client cannot drain the project budget.

Counts on admission rather than on success: a client retrying a failing turn
still pays Google, Vertex, and ElevenLabs on every attempt.

In-memory counters are correct only because the service is pinned to one
Cloud Run instance. If that ever changes, this moves to a shared counter at
the same time the session store does.
"""

_SECONDS_PER_DAY = 86400.0


class TurnLimiter:
    def __init__(self, max_per_session: int, max_per_day: int):
        self._max_per_session = max(1, int(max_per_session))
        self._max_per_day = max(1, int(max_per_day))
        self._session_counts: dict[str, int] = {}
        self._day_index: int | None = None
        self._day_count = 0

    def admit(self, session_id: str, now: float) -> str:
        """Returns "" if the turn may proceed, else a machine-readable reason.

        Counts the turn when it is admitted; a rejected turn costs nothing and
        is not counted against anyone else's budget.
        """
        day_index = int(now // _SECONDS_PER_DAY)
        if day_index != self._day_index:
            self._day_index = day_index
            self._day_count = 0

        if self._session_counts.get(session_id, 0) >= self._max_per_session:
            return "session_turn_limit_reached"
        if self._day_count >= self._max_per_day:
            return "daily_turn_budget_exhausted"

        self._session_counts[session_id] = self._session_counts.get(session_id, 0) + 1
        self._day_count += 1
        return ""

    def forget(self, session_id: str) -> None:
        self._session_counts.pop(session_id, None)
