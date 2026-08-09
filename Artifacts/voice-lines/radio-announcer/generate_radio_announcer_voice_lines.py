"""Generate the radio announcer's production lines with Roger."""

from argparse import ArgumentParser
from array import array
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
VOICE_NAME = "Roger"
VOICE_ID = "CwhRBWXzGAHq8TQ4Fs17"
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
SAMPLE_WIDTH = 2
CHANNELS = 1
FADE_DURATION_SECONDS = 0.020
SILENCE_DURATION_SECONDS = 0.150
SILENCE_FRAMES = round(SAMPLE_RATE * SILENCE_DURATION_SECONDS)
CLEANUP_FILTER = (
    "afftdn=nr=12:nf=-55:tn=1:gs=5,"
    f"areverse,afade=t=in:st=0:d={FADE_DURATION_SECONDS},areverse,"
    f"apad=pad_dur={SILENCE_DURATION_SECONDS}"
)

VOICE_SETTINGS = VoiceSettings(
    stability=0.50,
    similarity_boost=0.75,
    style=0.00,
    use_speaker_boost=True,
    speed=1.00,
)

# Tags and punctuation direct delivery without adding spoken content.
LINES = (
    (
        "RADIO-001",
        "A snowstorm is moving through the area. Please stay indoors until "
        "conditions improve.",
        "[neutral, impersonal, calm public safety announcement] A snowstorm is "
        "moving through the area. Please stay indoors until conditions improve.",
    ),
    (
        "RADIO-002",
        "…snow storm…",
        "[neutral, impersonal, calm public safety announcement] ...snow storm...",
    ),
    (
        "RADIO-003",
        "…please stay indoors…",
        "[neutral, impersonal, calm public safety announcement] "
        "...please stay indoors...",
    ),
    (
        "RADIO-004",
        "…during these times.",
        "[neutral, impersonal, calm public safety announcement] "
        "...during these times.",
    ),
)
LINE_IDS = tuple(line_id for line_id, _, _ in LINES)


def validate_wav(path: Path) -> None:
    """Raise when a production WAV fails the format, peak, or tail contract."""
    with wave.open(str(path), "rb") as audio:
        if audio.getnchannels() != CHANNELS:
            raise ValueError(f"Unexpected channel count in {path.name}")
        if audio.getsampwidth() != SAMPLE_WIDTH:
            raise ValueError(f"Unexpected sample width in {path.name}")
        if audio.getframerate() != SAMPLE_RATE:
            raise ValueError(f"Unexpected sample rate in {path.name}")
        if audio.getcomptype() != "NONE":
            raise ValueError(f"Unexpected compression in {path.name}")
        if audio.getnframes() <= 0:
            raise ValueError(f"No audio frames in {path.name}")
        samples = array("h", audio.readframes(audio.getnframes()))

    if sys.byteorder != "little":
        samples.byteswap()
    if not samples or not any(samples[:-SILENCE_FRAMES]):
        raise ValueError(f"No non-silent speech content in {path.name}")
    if any(sample in (-32_768, 32_767) for sample in samples):
        raise ValueError(f"Sample clipping detected in {path.name}")
    if len(samples) < SILENCE_FRAMES or any(samples[-SILENCE_FRAMES:]):
        raise ValueError(
            f"Less than {SILENCE_DURATION_SECONDS:.3f} s exact zero tail in "
            f"{path.name}"
        )


def clean_wav_safely(source_path: Path, output_path: Path) -> None:
    """Denoise, fade, pad, validate, and atomically publish a production WAV."""
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            dir=OUTPUT_DIRECTORY,
            prefix=f".{output_path.stem}-clean-",
            suffix=".wav",
            delete=False,
        ) as temporary:
            temporary_path = Path(temporary.name)

        command = (
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(source_path),
            "-af",
            CLEANUP_FILTER,
            "-ar",
            str(SAMPLE_RATE),
            "-ac",
            str(CHANNELS),
            "-c:a",
            "pcm_s16le",
            "-map_metadata",
            "-1",
            str(temporary_path),
        )
        try:
            subprocess.run(command, check=True)
        except FileNotFoundError as error:
            raise RuntimeError("ffmpeg is required for production cleanup") from error
        except subprocess.CalledProcessError as error:
            raise RuntimeError(
                f"ffmpeg cleanup failed for {source_path.name}"
            ) from error

        validate_wav(temporary_path)
        os.replace(temporary_path, output_path)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def write_wav_safely(path: Path, pcm: bytes) -> None:
    """Wrap raw PCM, apply production cleanup, and publish it atomically."""
    if not pcm:
        raise ValueError(f"Empty audio response for {path.stem}")
    if len(pcm) % SAMPLE_WIDTH:
        raise ValueError(f"Incomplete PCM sample for {path.stem}")

    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            dir=OUTPUT_DIRECTORY,
            prefix=f".{path.stem}-",
            suffix=".tmp",
            delete=False,
        ) as temporary:
            temporary_path = Path(temporary.name)

        with wave.open(str(temporary_path), "wb") as output:
            output.setnchannels(CHANNELS)
            output.setsampwidth(SAMPLE_WIDTH)
            output.setframerate(SAMPLE_RATE)
            output.writeframes(pcm)

        clean_wav_safely(temporary_path, path)
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def main() -> int:
    """Generate selected missing lines unless overwrite is explicitly enabled."""
    parser = ArgumentParser()
    parser.add_argument(
        "--line",
        action="append",
        choices=LINE_IDS,
        help="Generate only this stable ID; may be repeated. Defaults to all.",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Explicitly regenerate and replace selected existing WAVs.",
    )
    args = parser.parse_args()

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    selected_ids = set(args.line or LINE_IDS)

    for line_id, _canonical_words, prompt in LINES:
        if line_id not in selected_ids:
            continue

        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.overwrite:
            print(f"SKIP {output_path.name}", flush=True)
            continue

        chunks = client.text_to_speech.convert(
            voice_id=VOICE_ID,
            text=prompt,
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=VOICE_SETTINGS,
        )
        pcm = b"".join(chunks)
        write_wav_safely(output_path, pcm)
        print(f"GENERATED {output_path.name} with {VOICE_NAME}", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
