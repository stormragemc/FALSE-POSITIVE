"""Play Priya line 1 refined female-only voice auditions."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# All candidates use ElevenLabs model eleven_multilingual_v2.
# Candidate 1: Anika. ElevenLabs voice ID: 90ipbRoKi4CpHXvKVtl0
# Candidate 2: Monika Sogam. ElevenLabs voice ID: 2zRM7PkgwBPiau2jvVXc
# Candidate 3: Aaira. ElevenLabs voice ID: 1XNFRxE3WBB7iI0jnm7p
# Candidate 4: Aaliyah. ElevenLabs voice ID: aUTn6mevnrM9pqtesisb
# Candidate 5: Aasha. ElevenLabs voice ID: rxvktZTNrsQlsGIpOQGz
# Candidate 6: Saavi. ElevenLabs voice ID: a4BpQNxKFbuzzTj2JRQc
CANDIDATES = (
    (1, "Anika", "01-anika.wav"),
    (2, "Monika Sogam", "02-monika-sogam.wav"),
    (3, "Aaira", "03-aaira.wav"),
    (4, "Aaliyah", "04-aaliyah.wav"),
    (5, "Aasha", "05-aasha.wav"),
    (6, "Saavi", "06-saavi.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-01-panic-refined",
        "Guys—help! Something's happened to Nick. Ivy? Aaron? David? Please, come here!",
        CANDIDATES,
    )
