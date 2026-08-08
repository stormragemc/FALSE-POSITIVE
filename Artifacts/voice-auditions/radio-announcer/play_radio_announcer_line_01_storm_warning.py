"""Play radio announcer line 1 voice auditions."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Roger. ElevenLabs voice ID: CwhRBWXzGAHq8TQ4Fs17
# Candidate 2: Sarah. ElevenLabs voice ID: EXAVITQu4vr4xnSDxMaL
# Candidate 3: Daniel. ElevenLabs voice ID: onwK4e9ZLuTAKqWW03F9
# Candidate 4: Matilda. ElevenLabs voice ID: XrExE9yKIg1WjnnlVkGX
# Candidate 5: George. ElevenLabs voice ID: JBFqnCBsd6RMkjVDRZzb
# Candidate 6: Jessica. ElevenLabs voice ID: cgSgspJ2msm6clMCkdW9
# Candidate 7: Chris. ElevenLabs voice ID: iP95p4xoKVk53GoZ742B
# Candidate 8: Alice. ElevenLabs voice ID: Xb7hH8MSUJpSbSDYk0k2
CANDIDATES = (
    (1, "Roger", "01-roger.wav"),
    (2, "Sarah", "02-sarah.wav"),
    (3, "Daniel", "03-daniel.wav"),
    (4, "Matilda", "04-matilda.wav"),
    (5, "George", "05-george.wav"),
    (6, "Jessica", "06-jessica.wav"),
    (7, "Chris", "07-chris.wav"),
    (8, "Alice", "08-alice.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-01-storm-warning",
        "A snowstorm is moving through the area. Please stay indoors until conditions improve.",
        CANDIDATES,
    )
