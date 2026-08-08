"""Play one finalized radio-announcer line without opening a media player."""

from argparse import ArgumentParser
from pathlib import Path
import winsound


VOICE_DIRECTORY = Path(__file__).resolve().parent
LINE_IDS = tuple(f"RADIO-{number:03d}" for number in range(1, 5))


def main() -> None:
    """Resolve and optionally play exactly one stable ID."""
    parser = ArgumentParser()
    parser.add_argument("line_id", choices=LINE_IDS)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    audio_path = VOICE_DIRECTORY / f"{args.line_id}.wav"
    if not audio_path.is_file():
        raise FileNotFoundError(audio_path)

    if args.dry_run:
        print(f"DRY RUN {args.line_id}: {audio_path.name}", flush=True)
        return

    print(f"Playing {args.line_id}: {audio_path.name}", flush=True)
    winsound.PlaySound(
        str(audio_path),
        winsound.SND_FILENAME | winsound.SND_NODEFAULT,
    )


if __name__ == "__main__":
    main()
