"""Shared Windows WAV playback for the clearly named audition entry points."""

from pathlib import Path
import sys
import time
import winsound


def play_candidates(
    script_path: str,
    line_folder: str,
    line_text: str,
    candidates: tuple[tuple[int, str, str], ...],
) -> None:
    """Print and play every numbered candidate without opening a media player."""
    expected_numbers = list(range(1, len(candidates) + 1))
    actual_numbers = [number for number, _, _ in candidates]
    if actual_numbers != expected_numbers:
        raise ValueError(f"Candidate numbers must be sequential: {actual_numbers}")

    audio_directory = Path(script_path).resolve().parent / line_folder
    dry_run = "--dry-run" in sys.argv

    print(f"Line: {line_text}")
    for number, voice_name, filename in candidates:
        audio_path = audio_directory / filename
        if not audio_path.is_file():
            raise FileNotFoundError(audio_path)

        print(f"Candidate {number}: {voice_name}", flush=True)
        if not dry_run:
            winsound.PlaySound(
                str(audio_path),
                winsound.SND_FILENAME | winsound.SND_NODEFAULT,
            )
            time.sleep(0.8)


