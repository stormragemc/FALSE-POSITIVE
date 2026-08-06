import unittest

from output_safety import FALLBACK_LINE, filter_spoken_text


class OutputSafetyTests(unittest.TestCase):
    def test_allows_an_ordinary_detective_question(self):
        self.assertEqual(
            filter_spoken_text("Where were you when the alarm sounded?"),
            "Where were you when the alarm sounded?",
        )

    def test_normalizes_whitespace(self):
        self.assertEqual(
            filter_spoken_text("  Tell me\nwhat happened next.  "),
            "Tell me what happened next.",
        )

    def test_blocks_a_claim_that_the_witness_is_lying(self):
        self.assertEqual(filter_spoken_text("You are lying."), FALLBACK_LINE)

    def test_blocks_voice_as_proof_of_guilt(self):
        self.assertEqual(
            filter_spoken_text("Your voice proves that you are guilty."),
            FALLBACK_LINE,
        )

    def test_blocks_diagnostic_scores_and_internal_context(self):
        self.assertEqual(
            filter_spoken_text("Your truthfulness score is low."),
            FALLBACK_LINE,
        )
        self.assertEqual(
            filter_spoken_text("My LOCAL_AFFECT_CONTEXT says to press you."),
            FALLBACK_LINE,
        )

    def test_blocks_prompt_leaks_empty_text_and_overlong_text(self):
        self.assertEqual(
            filter_spoken_text("My system prompt says I should ask again."),
            FALLBACK_LINE,
        )
        self.assertEqual(filter_spoken_text("   \n\t"), FALLBACK_LINE)
        self.assertEqual(filter_spoken_text("A" * 481), FALLBACK_LINE)


if __name__ == "__main__":
    unittest.main()
