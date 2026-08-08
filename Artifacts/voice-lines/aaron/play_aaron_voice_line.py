"""Play one finalized Aaron line without opening a media player."""

from argparse import ArgumentParser
from pathlib import Path
import winsound


VOICE_DIRECTORY = Path(__file__).resolve().parent
LINE_IDS = tuple(f"AARON-{number:03d}" for number in range(1, 6))


def main() -> None:
    """Resolve and optionally play exactly one requested stable line ID."""
    parser = ArgumentParser()
    parser.add_argument("line_id", choices=LINE_IDS)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Resolve the requested WAV without playing it.",
    )
    args = parser.parse_args()

    audio_path = VOICE_DIRECTORY / f"{args.line_id}.wav"
    if not audio_path.is_file():
        raise FileNotFoundError(audio_path)

    if args.dry_run:
        print(f"Resolved {args.line_id}: {audio_path.name}", flush=True)
        return

    print(f"Playing {args.line_id}: {audio_path.name}", flush=True)
    winsound.PlaySound(
        str(audio_path),
        winsound.SND_FILENAME | winsound.SND_NODEFAULT,
    )


if __name__ == "__main__":
    main()
