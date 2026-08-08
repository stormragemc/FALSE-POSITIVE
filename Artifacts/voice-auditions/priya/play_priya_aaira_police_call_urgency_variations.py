"""Play Aaira's urgency variations for Priya's police call."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Provisional Priya voice: Aaira.
# ElevenLabs voice ID: 1XNFRxE3WBB7iI0jnm7p
# Model: eleven_v3 with Natural stability (0.50).
# Take 1 uses a panicked-but-clear direction and emphatic punctuation.
# Take 2 uses a quicker alarmed direction and tighter sentence flow.
# Neither take uses [breathless], avoiding the excessive gasps from earlier tests.
CANDIDATES = (
    (1, "Aaira — panicked but clear", "01-panicked-but-clear.wav"),
    (2, "Aaira — alarmed and faster", "02-alarmed-faster.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "aaira-v3-police-call-urgency-variations",
        "Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry.",
        CANDIDATES,
    )
