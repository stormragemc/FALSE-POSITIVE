"""Cost ceiling. Counts attempts, not successes — a retry loop still spends
money at the vendors, so admission is where the cap belongs."""

import unittest

from limits import TurnLimiter


class TurnLimiterTests(unittest.TestCase):
    def test_turns_under_both_caps_are_admitted(self):
        limiter = TurnLimiter(max_per_session=3, max_per_day=10)

        self.assertEqual(limiter.admit("a", 0.0), "")
        self.assertEqual(limiter.admit("a", 1.0), "")

    def test_session_cap_blocks_that_session_only(self):
        limiter = TurnLimiter(max_per_session=2, max_per_day=100)
        limiter.admit("a", 0.0)
        limiter.admit("a", 0.0)

        self.assertEqual(limiter.admit("a", 0.0), "session_turn_limit_reached")
        self.assertEqual(limiter.admit("b", 0.0), "")

    def test_daily_cap_blocks_everyone(self):
        limiter = TurnLimiter(max_per_session=100, max_per_day=2)
        limiter.admit("a", 0.0)
        limiter.admit("b", 0.0)

        self.assertEqual(limiter.admit("c", 0.0), "daily_turn_budget_exhausted")

    def test_daily_counter_rolls_over_but_session_counter_does_not(self):
        limiter = TurnLimiter(max_per_session=100, max_per_day=1)
        limiter.admit("a", 0.0)
        self.assertEqual(limiter.admit("b", 0.0), "daily_turn_budget_exhausted")

        tomorrow = 86400.0 + 10.0
        self.assertEqual(limiter.admit("b", tomorrow), "")

    def test_forget_clears_a_session_count_for_session_reset(self):
        limiter = TurnLimiter(max_per_session=1, max_per_day=100)
        limiter.admit("a", 0.0)
        self.assertEqual(limiter.admit("a", 0.0), "session_turn_limit_reached")

        limiter.forget("a")

        self.assertEqual(limiter.admit("a", 0.0), "")

    def test_a_blocked_turn_does_not_consume_daily_budget(self):
        limiter = TurnLimiter(max_per_session=1, max_per_day=5)
        limiter.admit("a", 0.0)
        limiter.admit("a", 0.0)  # blocked by the session cap

        # Four of the five daily turns must remain for other players.
        for session_id in ("b", "c", "d", "e"):
            self.assertEqual(limiter.admit(session_id, 0.0), "")


if __name__ == "__main__":
    unittest.main()
