"""Play Nick's fire-argument voice auditions."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# Candidate 1: Ivan. ElevenLabs voice ID: 1qd9R09Ljlx9V1Ok0t5S
# Candidate 2: Denis. ElevenLabs voice ID: 0BcDz9UPwL3MpsnTeUlO
# Candidate 3: Ivan Energetic. ElevenLabs voice ID: JKtNvDNrWu33P1xzttP2
# Candidate 4: Alexei. ElevenLabs voice ID: NQJnREzQtnAHHZnia0tY
# Candidate 5: Oleg. ElevenLabs voice ID: MWyJiWDobXN8FX3CJTdE
# Candidate 6: Guy. ElevenLabs voice ID: zvm1P65eFt40xSwMli2k
# Candidate 7: Alex Bell. ElevenLabs voice ID: TUQNWEvVPBLzMBSVDPUA
# Candidate 8: Escobar. ElevenLabs voice ID: XGyi3FDBCYWBQ6vRd0FV
CANDIDATES = (
    (1, "Ivan", "01-ivan.wav"),
    (2, "Denis", "02-denis.wav"),
    (3, "Ivan Energetic", "03-ivan-energetic.wav"),
    (4, "Alexei", "04-alexei.wav"),
    (5, "Oleg", "05-oleg.wav"),
    (6, "Guy", "06-guy.wav"),
    (7, "Alex Bell", "07-alex-bell.wav"),
    (8, "Escobar", "08-escobar.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "line-01-fire-argument",
        "Not tonight, David. I can't do this with you right now. I need some air.",
        CANDIDATES,
    )

