"""G6 — no lie/deception/truth language anywhere the player can reach.

GAME_COMPLETION_PLAN.md §8 guardrail 7 calls this non-negotiable and graded,
and assigns the grep gate to A11. It is graded because it is the whole thesis:
the officer notes an *unsupported detail*, and the game never claims to have
detected dishonesty. One stray "you're lying" in a prompt undoes that.

Scope, and why it is drawn here:

* **Prompt files** are checked whole. They are plain text with no comments, and
  every word in them is either spoken by the officer or shapes what he says.
* **C# is checked at its string literals only.** Comments must be free to
  discuss the rule — OutputGuard and this file's own subject matter cannot be
  written without the words — so a gate over whole C# files would either fail
  on its own documentation or force the documentation out.
* **The two filters are exempt.** `OutputGuard.cs` and `output_safety.py` carry
  the banned words *as patterns to block*. Excluding them is not a hole: they
  are the code this gate exists to keep honest, and they are covered directly
  by test_output_safety.py and JudgementLayerTests.
"""

from pathlib import Path
import re
import unittest

REPO = Path(__file__).resolve().parents[2]

# Word-boundary matched so "believe" does not trip on "lie".
BANNED = re.compile(
    r"\b(?:lie|lies|lied|lying|liar|truth|truthful|truthfulness|"
    r"deceit|deception|deceptive|dishonest|dishonesty)\b",
    re.IGNORECASE,
)

# Files whose job is to carry the banned words as blocklist patterns.
EXEMPT_FILES = {
    "OutputGuard.cs",
    "output_safety.py",
}

# Narrow, deliberate exceptions. Matched as exact substrings of the offending
# line rather than by filename, so anything *new* in these same files still
# fails. Each one is a decision, not an oversight:
ALLOWED_PHRASES = (
    # The officer's own instruction sheet, enumerating the vocabulary he must
    # not use. A rule cannot forbid a word without naming it, and naming them
    # explicitly enforces G6 far more reliably on the model than a paraphrase
    # would. Nothing here is spoken; it is the instruction that stops it being
    # spoken, and output_safety.py backstops the model if it slips anyway.
    "Never say the words lie, lying, liar, deception, dishonest, false, untrue,",

    # The "What It Cannot Do" transparency panel. This is the one place the
    # game is *required* to use the word: it exists to tell the player, in
    # plain terms, that nothing here detects dishonesty and that affect is not
    # evidence. G6 prohibits the officer implying the witness lied; it does not
    # prohibit the game disclaiming that lie detection is possible. Deleting
    # the word would make the disclaimer weaker and less honest, which is the
    # opposite of what the guardrail is for.
    "It cannot detect lies. Neither can anything else.",
)


def _allowed(text: str) -> bool:
    return any(phrase in text for phrase in ALLOWED_PHRASES)

PROMPT_DIRS = [
    REPO / "Sidecar" / "prompts",
    REPO / "Assets" / "_Project" / "Prompts",
]

CSHARP_ROOT = REPO / "Assets" / "_Project" / "Scripts"

# Ordinary double-quoted C# literals, including @"..." verbatim strings.
# Interpolated $"..." literals match too; the braces inside are harmless here
# because a banned word in an interpolated string is still a banned word.
STRING_LITERAL = re.compile(r'@?"(?:[^"\\\n]|\\.)*"')

LINE_COMMENT = re.compile(r"^\s*(?://|///|\*|/\*)")


def _literals(source: str):
    """Yield (line_number, literal) for every string literal outside a comment."""
    for number, line in enumerate(source.splitlines(), start=1):
        if LINE_COMMENT.match(line):
            continue
        for match in STRING_LITERAL.finditer(line):
            yield number, match.group(0)


class G6LanguageTests(unittest.TestCase):
    def test_prompts_avoid_the_banned_language(self):
        checked = 0
        offences = []

        for directory in PROMPT_DIRS:
            if not directory.is_dir():
                continue
            for path in sorted(directory.glob("*.txt")):
                checked += 1
                text = path.read_text(encoding="utf-8", errors="replace")
                for number, line in enumerate(text.splitlines(), start=1):
                    if _allowed(line):
                        continue
                    found = BANNED.search(line)
                    if found:
                        offences.append(
                            f"{path.relative_to(REPO)}:{number} — {found.group(0)!r} in {line.strip()!r}"
                        )

        self.assertGreater(checked, 0, "found no prompt files to check — has the layout moved?")
        self.assertEqual([], offences, "G6: banned language in a prompt:\n" + "\n".join(offences))

    def test_visible_csharp_strings_avoid_the_banned_language(self):
        checked = 0
        offences = []

        for path in sorted(CSHARP_ROOT.rglob("*.cs")):
            if path.name in EXEMPT_FILES:
                continue
            checked += 1
            source = path.read_text(encoding="utf-8", errors="replace")
            for number, literal in _literals(source):
                if _allowed(literal):
                    continue
                found = BANNED.search(literal)
                if found:
                    offences.append(
                        f"{path.relative_to(REPO)}:{number} — {found.group(0)!r} in {literal}"
                    )

        self.assertGreater(checked, 0, "found no C# to check — has the layout moved?")
        self.assertEqual(
            [], offences, "G6: banned language in a visible string:\n" + "\n".join(offences)
        )

    def test_the_gate_would_actually_catch_something(self):
        """A gate that cannot fail is not a gate.

        Both checks above pass by finding nothing, which is indistinguishable
        from a broken matcher or a wrong path. This pins the matcher itself.
        """
        self.assertTrue(BANNED.search('"You are lying."'))
        self.assertTrue(BANNED.search("Your truthfulness score is low."))
        self.assertFalse(BANNED.search("I believe the door was locked."))
        self.assertFalse(BANNED.search("an unsupported detail"))

        found = list(_literals('string s = "hello";\n// "lying" in a comment\n'))
        self.assertEqual(1, len(found), "comment lines must be skipped, code lines must not")
        self.assertIn("hello", found[0][1])


if __name__ == "__main__":
    unittest.main()
