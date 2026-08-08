"""Play Ivy line 2 voice auditions."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Jessica. ElevenLabs voice ID: cgSgspJ2msm6clMCkdW9
# Candidate 2: Matilda. ElevenLabs voice ID: XrExE9yKIg1WjnnlVkGX
# Candidate 3: Laura. ElevenLabs voice ID: FGY2WhTYpPnrIDTdsKH5
# Candidate 4: Lily. ElevenLabs voice ID: pFZP5JQG7iQjIQuC4Bku
# Candidate 5: Sarah. ElevenLabs voice ID: EXAVITQu4vr4xnSDxMaL
# Candidate 6: Alice. ElevenLabs voice ID: Xb7hH8MSUJpSbSDYk0k2
# Candidate 7: Aria. ElevenLabs voice ID: 9BWtsMINqrJLrRacOk9x
# Candidate 8: Charlotte. ElevenLabs voice ID: XB0fDUnXU5powFXDhCwa
CANDIDATES = (
    (1, "Jessica", "01-jessica.wav"),
    (2, "Matilda", "02-matilda.wav"),
    (3, "Laura", "03-laura.wav"),
    (4, "Lily", "04-lily.wav"),
    (5, "Sarah", "05-sarah.wav"),
    (6, "Alice", "06-alice.wav"),
    (7, "Aria", "07-aria.wav"),
    (8, "Charlotte", "08-charlotte.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-02-alibi",
        "I don't know. I was upstairs with Aaron.",
        CANDIDATES,
    )

