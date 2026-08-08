"""Play Roger's clean V3 Natural RADIO-003 audition."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Roger. ElevenLabs voice ID: CwhRBWXzGAHq8TQ4Fs17
# Uses ElevenLabs model eleven_v3 with Natural stability.
# Static and radio filtering are intentionally deferred to later sound design.
CANDIDATES = (
    (1, "Roger", "01-roger.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "v3-natural-round/line-03-stay-indoors",
        "…please stay indoors…",
        CANDIDATES,
    )
