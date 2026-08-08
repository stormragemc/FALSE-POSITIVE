"""Play Priya line 1 voice auditions."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Anika. ElevenLabs voice ID: 90ipbRoKi4CpHXvKVtl0
# Candidate 2: Monika Sogam. ElevenLabs voice ID: 2zRM7PkgwBPiau2jvVXc
# Candidate 3: Mahi. ElevenLabs voice ID: yD0Zg2jxgfQLY8I2MEHO
# Candidate 4: Aisha. ElevenLabs voice ID: MjJrIRgwH0lZCuxcakAW
# Candidate 5: Aaira. ElevenLabs voice ID: 1XNFRxE3WBB7iI0jnm7p
# Candidate 6: Aaliyah. ElevenLabs voice ID: aUTn6mevnrM9pqtesisb
# Candidate 7: Aasha. ElevenLabs voice ID: rxvktZTNrsQlsGIpOQGz
# Candidate 8: Saavi. ElevenLabs voice ID: a4BpQNxKFbuzzTj2JRQc
CANDIDATES = (
    (1, "Anika", "01-anika.wav"),
    (2, "Monika Sogam", "02-monika-sogam.wav"),
    (3, "Mahi", "03-mahi.wav"),
    (4, "Aisha", "04-aisha.wav"),
    (5, "Aaira", "05-aaira.wav"),
    (6, "Aaliyah", "06-aaliyah.wav"),
    (7, "Aasha", "07-aasha.wav"),
    (8, "Saavi", "08-saavi.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-01-panic",
        "Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here!",
        CANDIDATES,
    )

