"""Last safety check for text produced by the detective model.

The model is asked to stay in character, but its output is still untrusted.
Anything rejected here is replaced instead of being edited or sent to TTS.
"""

import re


FALLBACK_LINE = "Let's come back to that."
MAX_SPOKEN_CHARACTERS = 480

_BLOCKED_PATTERNS = (
    re.compile(
        r"\b(?:you|the witness)\s+(?:are|were|must be|have been)\s+"
        r"(?:lying|a liar|truthful|deceptive)\b",
        re.IGNORECASE,
    ),
    re.compile(
        r"\b(?:your|the witness'?s)\s+(?:voice|tone|speech|pauses?|affect)\s+"
        r"(?:proves|shows|confirms|means|tells me)\b",
        re.IGNORECASE,
    ),
    re.compile(
        r"\b(?:lie|truthfulness|deception|guilt|affect|emotion)\s+"
        r"(?:score|probability|detector|model|analysis|reading|label)\b",
        re.IGNORECASE,
    ),
    re.compile(
        r"\b(?:system prompt|system instruction|developer message|hidden "
        r"instructions|cop_persona|local_affect_context|witness_transcript)\b",
        re.IGNORECASE,
    ),
)


def filter_spoken_text(text: str) -> str:
    """Return safe dialogue, or the trusted fallback when in doubt."""
    cleaned = " ".join((text or "").split())
    if not cleaned or len(cleaned) > MAX_SPOKEN_CHARACTERS:
        return FALLBACK_LINE
    if any(pattern.search(cleaned) for pattern in _BLOCKED_PATTERNS):
        return FALLBACK_LINE
    return cleaned
