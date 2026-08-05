"""In-process paid-turn caps for the single-instance backend checkpoint."""

from threading import Lock
import time

SESSION_LIMIT_REASON = "session_turn_limit_reached"
DAILY_LIMIT_REASON = "daily_turn_budget_exhausted"


class TurnLimiter:
    """Atomically admit turns under per-session and UTC-day caps."""

    def __init__(self, max_per_session: int, max_per_day: int):
        if max_per_session < 1 or max_per_day < 1:
            raise ValueError("turn limits must be positive")
        self._max_per_session = max_per_session
        self._max_per_day = max_per_day
        self._session_counts: dict[str, int] = {}
        self._day: int | None = None
        self._day_count = 0
        self._lock = Lock()

    def admit(self, session_id: str, now: float | None = None) -> tuple[bool, str | None]:
        day = int((time.time() if now is None else now) // 86_400)
        with self._lock:
            if day != self._day:
                self._day = day
                self._day_count = 0

            if self._day_count >= self._max_per_day:
                return False, DAILY_LIMIT_REASON

            session_count = self._session_counts.get(session_id, 0)
            if session_count >= self._max_per_session:
                return False, SESSION_LIMIT_REASON

            self._session_counts[session_id] = session_count + 1
            self._day_count += 1
            return True, None

    def forget(self, session_id: str) -> None:
        with self._lock:
            self._session_counts.pop(session_id, None)
