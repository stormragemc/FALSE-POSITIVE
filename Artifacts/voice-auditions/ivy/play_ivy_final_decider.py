"""Play the Jessica-versus-Laura final voice decider for Ivy."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


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
        "final-decider/line-03-confirmation",
        "Yes. All night.",
        FINALISTS,
    )
    play_candidates(
        __file__,
        "final-decider/line-04-careful",
        "Careful. Careful. Easy.",
        FINALISTS,
    )
