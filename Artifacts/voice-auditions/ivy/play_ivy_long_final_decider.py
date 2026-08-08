"""Play Ivy's longer Jessica-versus-Laura voice decider."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Audition-only passage; it is not part of HUMAN_SCRIPT.md.
# Both finalists use ElevenLabs model eleven_v3 with Natural stability.
# Finalist 1: Jessica. ElevenLabs voice ID: cgSgspJ2msm6clMCkdW9
# Finalist 2: Laura. ElevenLabs voice ID: FGY2WhTYpPnrIDTdsKH5
FINALISTS = (
    (1, "Jessica", "01-jessica.wav"),
    (2, "Laura", "02-laura.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "final-decider/line-05-long-decider",
        (
            "I don't know what happened. I was upstairs with Aaron, and by the "
            "time we came down, Nick was already outside. I know how that "
            "sounds, but it's the truth. Please... can we just slow down and think?"
        ),
        FINALISTS,
    )
