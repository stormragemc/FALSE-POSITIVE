"""Play Aaira's Eleven v3 validation lines for Priya."""

from pathlib import Path
import sys
import time
import winsound


# Provisional Priya voice: Aaira.
# ElevenLabs voice ID: 1XNFRxE3WBB7iI0jnm7p
# Model: eleven_v3 with Natural stability (0.50).
# Similarity boost: 0.75. Style: 0.00. Speaker boost: enabled. Speed: 1.00.
LINES = (
    (1, "Locked-door suspicion", "The door was locked. Who locked it?", "01-locked-door-suspicion.wav"),
    (2, "Concern for Nick", "Nick? Nick, can you hear me?", "02-nick-concern.wav"),
    (
        3,
        "Urgent police call",
        "Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry.",
        "03-police-call.wav",
    ),
    (
        4,
        "Ending confusion",
        "What happened? Why won't anyone tell me what happened?",
        "04-ending-confusion.wav",
    ),
)


def main() -> None:
    """Print and play every validation line, or a requested line number."""
    dry_run = "--dry-run" in sys.argv
    requested = [int(arg) for arg in sys.argv[1:] if arg.isdigit()]
    selected_lines = LINES
    if requested:
        selected_lines = tuple(line for line in LINES if line[0] in requested)
        if not selected_lines:
            raise ValueError(f"Unknown line number: {requested}")

    audio_directory = Path(__file__).resolve().parent / "aaira-v3-final-validation"
    for number, label, text, filename in selected_lines:
        audio_path = audio_directory / filename
        if not audio_path.is_file():
            raise FileNotFoundError(audio_path)

        print(f"Line {number}: {label}", flush=True)
        print(text, flush=True)
        if not dry_run:
            winsound.PlaySound(
                str(audio_path),
                winsound.SND_FILENAME | winsound.SND_NODEFAULT,
            )
            time.sleep(0.8)


if __name__ == "__main__":
    main()
