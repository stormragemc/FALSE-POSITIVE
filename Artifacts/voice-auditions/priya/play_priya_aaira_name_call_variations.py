"""Play Aaira's Priya line 1 name-calling delivery variations."""

from pathlib import Path
import sys


AUDITION_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AUDITION_ROOT))

from _playback import play_candidates


# All takes use Aaira through ElevenLabs model eleven_v3, Natural stability.
# Aaira ElevenLabs voice ID: 1XNFRxE3WBB7iI0jnm7p
# Take 1: restrained [calling out] direction with pauses between names.
# Take 2: breathless opening and a pleading final request.
# Take 3: stronger [shouts] direction as though calling across the cabin.
# Take 4: no audio tags; delivery is controlled only by punctuation and casing.
# Take 5: take 2's breathless opening with take 4's punctuation-only name calls.
# Take 6: take 5 with the ellipses removed for quicker consecutive name calls.
# Take 7: quick name calls with [worried] replacing [breathless] to avoid gasps.
CANDIDATES = (
    (1, "Aaira — balanced calls", "01-balanced-calls.wav"),
    (2, "Aaira — breathless calls", "02-breathless-calls.wav"),
    (3, "Aaira — across-cabin calls", "03-across-cabin-calls.wav"),
    (4, "Aaira — punctuation only", "04-punctuation-only.wav"),
    (5, "Aaira — breathless/punctuation hybrid", "05-hybrid-breathless-natural-calls.wav"),
    (6, "Aaira — hybrid with quick name calls", "06-hybrid-quick-name-calls.wav"),
    (7, "Aaira — worried quick calls, no breathless tag", "07-worried-quick-calls-no-breathless.wav"),
)


if __name__ == "__main__":
    play_candidates(
        __file__,
        "aaira-line-01-name-call-variations",
        "Guys—help! Something's happened to Nick. Ivy! Aaron! David! Please—come here!",
        CANDIDATES,
    )
