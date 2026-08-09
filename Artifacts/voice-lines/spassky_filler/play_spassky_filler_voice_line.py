"""Play one or more Spassky filler lines without opening a media player."""

from argparse import ArgumentParser
from pathlib import Path
import winsound


VOICE_DIRECTORY = Path(__file__).resolve().parent
LINE_IDS = (
    "SPASSKY-FILLER-001",
    "SPASSKY-FILLER-003",
    "SPASSKY-FILLER-004",
    "SPASSKY-FILLER-005",
    "SPASSKY-FILLER-006",
    "SPASSKY-FILLER-007",
    "SPASSKY-FILLER-009",
    "SPASSKY-FILLER-010",
    "SPASSKY-FILLER-013",
    "SPASSKY-FILLER-023",
)


def main() -> None:
    """Play the requested filler IDs through the Windows audio backend."""
    parser = ArgumentParser()
    parser.add_argument("line_ids", nargs="+", choices=LINE_IDS, metavar="ID")
    args = parser.parse_args()

    for line_id in args.line_ids:
        audio_path = VOICE_DIRECTORY / f"{line_id}.wav"
        if not audio_path.is_file():
            raise FileNotFoundError(audio_path)
        print(f"Playing {line_id}: {audio_path.name}", flush=True)
        winsound.PlaySound(
            str(audio_path),
            winsound.SND_FILENAME | winsound.SND_NODEFAULT,
        )


if __name__ == "__main__":
    main()
