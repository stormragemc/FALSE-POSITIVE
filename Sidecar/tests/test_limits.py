"""Per-session and per-day turn admission tests."""

import importlib
import importlib.util
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import unittest


class TurnLimiterAvailabilityTests(unittest.TestCase):
    def test_limits_module_exists(self):
        self.assertIsNotNone(
            importlib.util.find_spec("limits"),
            "the backend needs turn caps before paid APIs can be exposed",
        )


class TurnLimiterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.find_spec("limits")
        if spec is None:
            raise unittest.SkipTest("limits module does not exist yet")
        cls.TurnLimiter = importlib.import_module("limits").TurnLimiter

    def test_turns_are_admitted_below_both_caps(self):
        limiter = self.TurnLimiter(max_per_session=2, max_per_day=3)

        self.assertEqual(limiter.admit("a", now=100.0), (True, None))
        self.assertEqual(limiter.admit("a", now=101.0), (True, None))
        self.assertEqual(limiter.admit("b", now=102.0), (True, None))

    def test_session_cap_only_blocks_that_session(self):
        limiter = self.TurnLimiter(max_per_session=1, max_per_day=3)

        self.assertEqual(limiter.admit("a", now=100.0), (True, None))
        self.assertEqual(
            limiter.admit("a", now=101.0),
            (False, "session_turn_limit_reached"),
        )
        self.assertEqual(limiter.admit("b", now=102.0), (True, None))

    def test_daily_cap_blocks_all_sessions_until_utc_day_rollover(self):
        limiter = self.TurnLimiter(max_per_session=3, max_per_day=2)
        last_second = 86_399.0

        self.assertEqual(limiter.admit("a", now=1.0), (True, None))
        self.assertEqual(limiter.admit("b", now=last_second), (True, None))
        self.assertEqual(
            limiter.admit("c", now=last_second),
            (False, "daily_turn_budget_exhausted"),
        )
        self.assertEqual(limiter.admit("c", now=86_400.0), (True, None))

    def test_forget_resets_only_the_named_session_counter(self):
        limiter = self.TurnLimiter(max_per_session=1, max_per_day=4)
        limiter.admit("a", now=100.0)
        limiter.admit("b", now=101.0)

        limiter.forget("a")

        self.assertEqual(limiter.admit("a", now=102.0), (True, None))
        self.assertEqual(
            limiter.admit("b", now=103.0),
            (False, "session_turn_limit_reached"),
        )

    def test_zero_or_negative_limits_are_rejected(self):
        for session_cap, day_cap in ((0, 1), (1, 0), (-1, 1), (1, -1)):
            with self.subTest(session_cap=session_cap, day_cap=day_cap):
                with self.assertRaises(ValueError):
                    self.TurnLimiter(session_cap, day_cap)

    def test_concurrent_admission_never_exceeds_session_cap(self):
        limiter = self.TurnLimiter(max_per_session=7, max_per_day=100)

        with ThreadPoolExecutor(max_workers=20) as pool:
            results = list(pool.map(lambda _index: limiter.admit("shared"), range(50)))

        self.assertEqual(sum(allowed for allowed, _reason in results), 7)
        self.assertTrue(
            all(
                reason == "session_turn_limit_reached"
                for allowed, reason in results
                if not allowed
            )
        )

    def test_documented_limit_reasons_match_the_runtime_contract(self):
        sidecar_dir = Path(__file__).resolve().parents[1]
        project_dir = sidecar_dir.parent
        documents = (
            sidecar_dir / "README.md",
            project_dir / "docs/IMPLEMENTATION_PLAN.md",
        )

        for path in documents:
            with self.subTest(path=path.name):
                text = path.read_text(encoding="utf-8")
                self.assertIn("session_turn_limit_reached", text)
                self.assertIn("daily_turn_budget_exhausted", text)
                self.assertNotIn("`session_turn_limit`", text)
                self.assertNotIn("`daily_turn_limit`", text)


if __name__ == "__main__":
    unittest.main()
