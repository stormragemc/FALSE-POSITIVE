"""Play one or more finalized Priya lines without opening a media player."""

from argparse import ArgumentParser
from pathlib import Path
import time
import winsound


VOICE_DIRECTORY = Path(__file__).resolve().parent
LINE_IDS = tuple(f"PRIYA-{number:03d}" for number in range(1, 9))


def main() -> None:
    """Play requested IDs, or print the available IDs with --dry-run."""
    parser = ArgumentParser()
    parser.add_argument("line_ids", nargs="*", choices=LINE_IDS)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    selected_ids = tuple(args.line_ids) if args.line_ids else LINE_IDS
    for line_id in selected_ids:
        audio_path = VOICE_DIRECTORY / f"{line_id}.wav"
        if not audio_path.is_file():
            raise FileNotFoundError(audio_path)

        print(f"Playing {line_id}: {audio_path.name}", flush=True)
        if not args.dry_run:
            winsound.PlaySound(
                str(audio_path),
                winsound.SND_FILENAME | winsound.SND_NODEFAULT,
            )
            time.sleep(0.8)


if __name__ == "__main__":
    main()
