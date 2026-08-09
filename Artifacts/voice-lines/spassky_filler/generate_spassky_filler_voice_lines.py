"""Generate Officer Spassky's prerecorded filler acknowledgements."""

from argparse import ArgumentParser
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile
import wave

import numpy as np
from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent

VOICE_ID = "6sXsAlJKKBf265ucBSRt"  # Maksim — "Raw, unpolished, deep", Russian
MODEL_ID = "eleven_multilingual_v2"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
SAMPLE_WIDTH = 2
CHANNELS = 1
PEAK_CEILING = 0.97
TRAILING_SILENCE_MILLISECONDS = 150
ZERO_TAIL_FRAMES = SAMPLE_RATE * TRAILING_SILENCE_MILLISECONDS // 1_000
CLEANUP_FILTER = (
    "afftdn=nr=12:nf=-55:tn=1:gs=5,"
    "areverse,afade=t=in:st=0:d=0.020,areverse,"
    "apad=pad_dur=0.150"
)

# These acknowledgements use Spassky's FLAT register. They must remain neutral:
# heard, but neither believed nor disbelieved. The exact prompts are the spoken
# words because eleven_multilingual_v2 does not use Eleven V3 audio tags.
VOICE_SETTINGS = VoiceSettings(
    stability=0.28,
    similarity_boost=1.00,
    style=0.62,
    speed=0.92,
)

LINES = (
    ("SPASSKY-FILLER-001", "Hm."),
    ("SPASSKY-FILLER-003", "I see."),
    ("SPASSKY-FILLER-004", "Interesting."),
    ("SPASSKY-FILLER-005", "All right."),
    ("SPASSKY-FILLER-006", "Very well."),
    ("SPASSKY-FILLER-007", "Noted."),
    ("SPASSKY-FILLER-009", "That's noted."),
    ("SPASSKY-FILLER-010", "Hm. I see."),
    ("SPASSKY-FILLER-013", "Interesting. All right."),
    ("SPASSKY-FILLER-023", "Hm... interesting."),
)


def write_pcm_wav(path: Path, pcm: bytes) -> None:
    """Write raw 24 kHz mono 16-bit PCM to a WAV container."""
    with wave.open(str(path), "wb") as output:
        output.setnchannels(CHANNELS)
        output.setsampwidth(SAMPLE_WIDTH)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def read_pcm_wav(path: Path) -> bytes:
    """Read one canonical WAV and reject an unexpected format."""
    with wave.open(str(path), "rb") as source:
        actual = (
            source.getnchannels(),
            source.getframerate(),
            source.getsampwidth(),
            source.getcomptype(),
        )
        expected = (CHANNELS, SAMPLE_RATE, SAMPLE_WIDTH, "NONE")
        if actual != expected:
            raise RuntimeError(f"{path.name} has format {actual}; expected {expected}.")
        pcm = source.readframes(source.getnframes())
    if not pcm:
        raise RuntimeError(f"{path.name} contains no audio frames.")
    return pcm


def validate_wav(path: Path) -> dict[str, float | int]:
    """Validate format, headroom, and the exact digital-zero playback tail."""
    pcm = read_pcm_wav(path)
    samples = np.frombuffer(pcm, dtype=np.int16)
    if samples.size <= ZERO_TAIL_FRAMES:
        raise RuntimeError(f"{path.name} is shorter than its required zero tail.")

    tail = samples[-ZERO_TAIL_FRAMES:]
    if np.any(tail):
        raise RuntimeError(
            f"{path.name} does not end with {ZERO_TAIL_FRAMES} exact zero frames."
        )

    speech_samples = samples[:-ZERO_TAIL_FRAMES]
    peak_sample = int(np.max(np.abs(speech_samples.astype(np.int32))))
    peak = peak_sample / 32768.0
    if peak > PEAK_CEILING:
        raise RuntimeError(
            f"{path.name} peak {peak:.6f} exceeds ceiling {PEAK_CEILING:.2f}."
        )
    if np.any(np.abs(speech_samples.astype(np.int32)) >= 32767):
        raise RuntimeError(f"{path.name} contains full-scale samples.")

    return {
        "frames": int(samples.size),
        "duration_seconds": samples.size / SAMPLE_RATE,
        "peak": peak,
        "zero_tail_frames": ZERO_TAIL_FRAMES,
    }


def write_clean_wav(path: Path, pcm: bytes) -> dict[str, float | int]:
    """Denoise, fade the endpoint, pad it, validate it, and install atomically."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise RuntimeError("FFmpeg is required for production VO cleanup.")

    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        dir=path.parent,
        prefix=f".{path.stem}-",
    ) as temporary_directory:
        temporary_root = Path(temporary_directory)
        source_path = temporary_root / "source.wav"
        cleaned_path = temporary_root / path.name
        write_pcm_wav(source_path, pcm)

        subprocess.run(
            [
                ffmpeg,
                "-hide_banner",
                "-loglevel",
                "error",
                "-nostdin",
                "-y",
                "-i",
                str(source_path),
                "-af",
                CLEANUP_FILTER,
                "-ac",
                str(CHANNELS),
                "-ar",
                str(SAMPLE_RATE),
                "-c:a",
                "pcm_s16le",
                str(cleaned_path),
            ],
            check=True,
        )
        metrics = validate_wav(cleaned_path)
        os.replace(cleaned_path, path)
    return metrics


def main() -> int:
    """Generate missing fillers, or overwrite selected files with --force."""
    parser = ArgumentParser()
    parser.add_argument(
        "--force",
        action="store_true",
        help="Regenerate and overwrite existing production WAVs.",
    )
    parser.add_argument(
        "--only",
        metavar="ID",
        action="append",
        help="Render only this line ID. Repeatable.",
    )
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Validate existing WAVs without calling the API.",
    )
    args = parser.parse_args()

    lines = LINES
    if args.only:
        wanted = {value.upper() for value in args.only}
        lines = tuple(entry for entry in LINES if entry[0] in wanted)
        missing = wanted - {entry[0] for entry in lines}
        if missing:
            print(f"Unknown line IDs: {', '.join(sorted(missing))}", file=sys.stderr)
            return 2

    if args.validate_only:
        missing_files = []
        for line_id, _text in lines:
            output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
            if not output_path.is_file():
                missing_files.append(output_path.name)
                continue
            metrics = validate_wav(output_path)
            print(
                f"VALID {output_path.name}  "
                f"{metrics['duration_seconds']:.3f}s  peak={metrics['peak']:.4f}",
                flush=True,
            )
        if missing_files:
            print(f"Missing WAVs: {', '.join(missing_files)}", file=sys.stderr)
            return 2
        return 0

    client = None
    for line_id, text in lines:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.force:
            print(f"SKIP {output_path.name}", flush=True)
            continue

        if client is None:
            api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
            if not api_key:
                print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
                return 2
            client = ElevenLabs(api_key=api_key)

        chunks = client.text_to_speech.convert(
            voice_id=VOICE_ID,
            text=text,
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=VOICE_SETTINGS,
        )
        pcm = b"".join(chunks)
        if not pcm:
            raise RuntimeError(f"Empty audio response for {line_id}")
        metrics = write_clean_wav(output_path, pcm)
        print(
            f"GENERATED {output_path.name}  "
            f"{metrics['duration_seconds']:.3f}s  peak={metrics['peak']:.4f}",
            flush=True,
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
