"""Play Nick's Ivan-Energetic-versus-Alexei final decider."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Both finalists use ElevenLabs model eleven_v3 with Natural stability.
# Finalist 1: Ivan Energetic, original candidate 3.
# ElevenLabs voice ID: JKtNvDNrWu33P1xzttP2
# Finalist 2: Alexei, original candidate 4.
# ElevenLabs voice ID: NQJnREzQtnAHHZnia0tY
FINALISTS = (
    (1, "Ivan Energetic", "01-ivan-energetic.wav"),
    (2, "Alexei", "02-alexei.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "final-decider/line-03-freezing",
        "Here. You look fucking freezing.",
        FINALISTS,
    )
