"""Play David line 3 voice auditions. Reference only: David is normally the player."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Brian. ElevenLabs voice ID: nPczCjzI2devNBz1zQrb
# Candidate 2: Daniel. ElevenLabs voice ID: onwK4e9ZLuTAKqWW03F9
# Candidate 3: George. ElevenLabs voice ID: JBFqnCBsd6RMkjVDRZzb
# Candidate 4: Eric. ElevenLabs voice ID: cjVigY5qzO86Huf0OWal
# Candidate 5: Liam. ElevenLabs voice ID: TX3LPaxmHKxFdv7VOQHJ
# Candidate 6: Will. ElevenLabs voice ID: bIHbv24MWmeRgasZH58o
# Candidate 7: Callum. ElevenLabs voice ID: N2lVS1w4EtoT3dr4eOWO
# Candidate 8: Roger. ElevenLabs voice ID: CwhRBWXzGAHq8TQ4Fs17
CANDIDATES = (
    (1, "Brian", "01-brian.wav"),
    (2, "Daniel", "02-daniel.wav"),
    (3, "George", "03-george.wav"),
    (4, "Eric", "04-eric.wav"),
    (5, "Liam", "05-liam.wav"),
    (6, "Will", "06-will.wav"),
    (7, "Callum", "07-callum.wav"),
    (8, "Roger", "08-roger.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-03-defense",
        "Because I didn't kill Nick. I argued with him, let him walk out into that storm, and passed out. I did not lock that door.",
        CANDIDATES,
    )

