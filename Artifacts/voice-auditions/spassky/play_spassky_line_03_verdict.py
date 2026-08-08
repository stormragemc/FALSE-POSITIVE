"""Play Officer Spassky line 3 voice auditions."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Stanislav. ElevenLabs voice ID: ogi2DyUAKJb7CEdqqvlU
# Candidate 2: Alexei. ElevenLabs voice ID: NQJnREzQtnAHHZnia0tY
# Candidate 3: Ivan. ElevenLabs voice ID: 1qd9R09Ljlx9V1Ok0t5S
# Candidate 4: Denis. ElevenLabs voice ID: 1EVds7FNGSXoKeOiMXuf
# Candidate 5: Alex Bell. ElevenLabs voice ID: TUQNWEvVPBLzMBSVDPUA
# Candidate 6: Artem Lebedev. ElevenLabs voice ID: rQOBu7YxCDxGiFdTm28w
# Candidate 7: Dmitry. ElevenLabs voice ID: vnUSJFFoxRr5JFjw51pu
# Candidate 8: Valery. ElevenLabs voice ID: gXMhWmiqsFkrcssqVb5k
CANDIDATES = (
    (1, "Stanislav", "01-stanislav.wav"),
    (2, "Alexei", "02-alexei.wav"),
    (3, "Ivan", "03-ivan.wav"),
    (4, "Denis", "04-denis.wav"),
    (5, "Alex Bell", "05-alex-bell.wav"),
    (6, "Artem Lebedev", "06-artem-lebedev.wav"),
    (7, "Dmitry", "07-dmitry.wav"),
    (8, "Valery", "08-valery.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-03-verdict",
        "Tell me why I should spare your life.",
        CANDIDATES,
    )

