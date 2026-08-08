"""Play one or more finalized Spassky lines without opening a media player.

Cross-platform, unlike the Priya player: this set is generated from macOS and
reviewed on Windows, so it dispatches to whichever backend the host provides.
"""

from argparse import ArgumentParser
from pathlib import Path
import shutil
import subprocess
import sys
import time


VOICE_DIRECTORY = Path(__file__).resolve().parent
LINE_IDS = tuple(f"SPASSKY-{number:03d}" for number in range(1, 63))


def play(audio_path: Path) -> None:
    """Play a WAV through the host's own audio backend."""
    if sys.platform == "win32":
        import winsound

        winsound.PlaySound(
            str(audio_path),
            winsound.SND_FILENAME | winsound.SND_NODEFAULT,
        )
        return

    players = ("afplay",) if sys.platform == "darwin" else ("paplay", "aplay", "ffplay")
    for player in players:
        executable = shutil.which(player)
        if not executable:
            continue
        command = [executable, str(audio_path)]
        if player == "ffplay":
            command[1:1] = ["-nodisp", "-autoexit", "-loglevel", "error"]
        subprocess.run(command, check=True)
        return

    raise RuntimeError(f"No audio player found; tried {', '.join(players)}.")


def main() -> None:
    """Play requested IDs, or print the available IDs with --dry-run."""
    parser = ArgumentParser()
    parser.add_argument("line_ids", nargs="*", choices=LINE_IDS, metavar="ID")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    selected_ids = tuple(args.line_ids) if args.line_ids else LINE_IDS
    for line_id in selected_ids:
        audio_path = VOICE_DIRECTORY / f"{line_id}.wav"
        if not audio_path.is_file():
            raise FileNotFoundError(audio_path)

        print(f"Playing {line_id}: {audio_path.name}", flush=True)
        if not args.dry_run:
            play(audio_path)
            time.sleep(0.8)


if __name__ == "__main__":
    main()
