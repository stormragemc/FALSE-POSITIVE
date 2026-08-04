"""The session store is the seam a networked store drops into later."""

import unittest

from session_store import InMemorySessionStore


class InMemorySessionStoreTests(unittest.TestCase):
    def test_unknown_session_reads_as_empty_without_creating_it(self):
        store = InMemorySessionStore(max_sessions=4)

        self.assertEqual(store.history("nobody"), [])
        self.assertEqual(store.commit("real", [{"role": "user", "content": "hi"}]), [])
        self.assertEqual(len(store.history("real")), 1)

    def test_commit_replaces_history_and_refreshes_recency(self):
        store = InMemorySessionStore(max_sessions=2)
        store.commit("a", [{"role": "user", "content": "1"}])
        store.commit("b", [{"role": "user", "content": "2"}])
        # Touching "a" must make "b" the least-recently-used, not "a".
        store.commit("a", [{"role": "user", "content": "1"}, {"role": "user", "content": "3"}])

        evicted = store.commit("c", [{"role": "user", "content": "4"}])

        self.assertEqual(evicted, ["b"])
        self.assertEqual(len(store.history("a")), 2)
        self.assertEqual(store.history("b"), [])

    def test_reset_forgets_one_session_and_clear_forgets_all(self):
        store = InMemorySessionStore(max_sessions=4)
        store.commit("a", [{"role": "user", "content": "1"}])
        store.commit("b", [{"role": "user", "content": "2"}])

        store.reset("a")
        self.assertEqual(store.history("a"), [])
        self.assertEqual(len(store.history("b")), 1)

        store.clear()
        self.assertEqual(store.history("b"), [])

    def test_reset_of_unknown_session_is_not_an_error(self):
        store = InMemorySessionStore(max_sessions=4)
        store.reset("never-existed")  # must not raise


if __name__ == "__main__":
    unittest.main()
