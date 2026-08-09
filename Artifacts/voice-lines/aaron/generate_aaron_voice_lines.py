"""Generate Aaron's production dialogue with the selected Liam voice."""

from argparse import ArgumentParser
from array import array
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
VOICE_ID = "TX3LPaxmHKxFdv7VOQHJ"  # Liam
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
SAMPLE_WIDTH = 2
CHANNELS = 1
FFMPEG_BINARY = "ffmpeg"
CLEANUP_FILTER = "afftdn=nr=12:nf=-55:tn=1:gs=5"
FADE_DURATION_SECONDS = 0.020
SILENCE_DURATION_SECONDS = 0.150
MINIMUM_ZERO_TAIL_FRAMES = round(SAMPLE_RATE * SILENCE_DURATION_SECONDS)

VOICE_SETTINGS = VoiceSettings(
    stability=0.50,
    similarity_boost=0.75,
    style=0.00,
    use_speaker_boost=True,
    speed=1.00,
)

# Canonical words are retained beside each exact synthesis prompt. Tags and
# punctuation direct the performance and must not add spoken content.
LINES = (
    (
        "AARON-001",
        "He's freezing. Let's get him inside, onto the sofa by the fire.",
        "[calm on the surface, suppressing urgency, taking charge] "
        "He’s freezing.\n\n"
        "[steady and decisive] Let’s get him inside.\n\n"
        "Onto the sofa, by the fire.",
    ),
    (
        "AARON-002",
        "Priya. Not now.",
        "[controlled, firm, redirecting] Priya. Not now.",
    ),
    (
        "AARON-003",
        "Lift on three. One, two, three.",
        "[firm, coordinating the group before a heavy lift] "
        "Lift on three. One, two, three.",
    ),
    (
        "AARON-004",
        "Barely survived it.",
        "[relaxed, dryly joking] Barely survived it.",
    ),
    (
        "AARON-005",
        "…Two years?",
        "[quiet, flat, stunned] ...Two years?",
    ),
)


def read_wav_samples(path: Path) -> tuple[int, array[int]]:
    """Validate the WAV format and return its frame count and PCM samples."""
    with wave.open(str(path), "rb") as audio:
        properties = (
            audio.getnchannels(),
            audio.getframerate(),
            audio.getsampwidth(),
            audio.getcomptype(),
        )
        expected = (CHANNELS, SAMPLE_RATE, SAMPLE_WIDTH, "NONE")
        if properties != expected:
            raise ValueError(
                f"Invalid WAV format for {path.name}: {properties}; "
                f"expected {expected}"
            )
        frame_count = audio.getnframes()
        if frame_count <= 0:
            raise ValueError(f"WAV contains no audio frames: {path.name}")
        samples = array("h", audio.readframes(frame_count))

    if sys.byteorder != "little":
        samples.byteswap()
    return frame_count, samples


def validate_wav(path: Path, require_clean_tail: bool = True) -> int:
    """Validate the production WAV contract and return its frame count."""
    frame_count, samples = read_wav_samples(path)
    clipping_count = sum(
        sample in (-32_768, 32_767) for sample in samples
    )
    if clipping_count:
        raise ValueError(
            f"WAV contains {clipping_count} clipped samples: {path.name}"
        )

    if require_clean_tail:
        zero_tail_frames = 0
        for sample in reversed(samples):
            if sample != 0:
                break
            zero_tail_frames += 1
        if zero_tail_frames < MINIMUM_ZERO_TAIL_FRAMES:
            raise ValueError(
                f"WAV has only {zero_tail_frames} exact-zero tail frames: "
                f"{path.name}; expected at least {MINIMUM_ZERO_TAIL_FRAMES}"
            )
    return frame_count


def run_ffmpeg(arguments: list[str]) -> None:
    """Run FFmpeg without interactive input or noisy routine output."""
    ffmpeg_path = shutil.which(FFMPEG_BINARY)
    if ffmpeg_path is None:
        raise RuntimeError("ffmpeg is required but was not found on PATH")
    subprocess.run(
        [
            ffmpeg_path,
            "-nostdin",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            *arguments,
        ],
        check=True,
    )


def write_wav_safely(path: Path, pcm: bytes) -> None:
    """Clean raw PCM, add a safe tail, then atomically install the WAV."""
    if not pcm:
        raise ValueError(f"Empty audio response for {path.stem}")
    if len(pcm) % SAMPLE_WIDTH:
        raise ValueError(f"Unaligned PCM response for {path.stem}")

    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_paths: list[Path] = []
    try:
        for stage in ("raw", "denoised", "cleaned"):
            with tempfile.NamedTemporaryFile(
                dir=path.parent,
                prefix=f".{path.stem}.{stage}.",
                suffix=".tmp.wav",
                delete=False,
            ) as temporary:
                temporary_paths.append(Path(temporary.name))
        raw_path, denoised_path, cleaned_path = temporary_paths

        with wave.open(str(raw_path), "wb") as output:
            output.setnchannels(CHANNELS)
            output.setsampwidth(SAMPLE_WIDTH)
            output.setframerate(SAMPLE_RATE)
            output.writeframes(pcm)

        validate_wav(raw_path, require_clean_tail=False)
        run_ffmpeg(
            [
                "-i",
                str(raw_path),
                "-af",
                CLEANUP_FILTER,
                "-ac",
                str(CHANNELS),
                "-ar",
                str(SAMPLE_RATE),
                "-c:a",
                "pcm_s16le",
                "-map_metadata",
                "-1",
                str(denoised_path),
            ]
        )

        denoised_frames, _samples = read_wav_samples(denoised_path)
        denoised_duration = denoised_frames / SAMPLE_RATE
        fade_start = max(0.0, denoised_duration - FADE_DURATION_SECONDS)
        tail_filter = (
            f"afade=t=out:st={fade_start:.9f}:d={FADE_DURATION_SECONDS:.3f},"
            f"apad=pad_dur={SILENCE_DURATION_SECONDS:.3f}"
        )
        run_ffmpeg(
            [
                "-i",
                str(denoised_path),
                "-af",
                tail_filter,
                "-ac",
                str(CHANNELS),
                "-ar",
                str(SAMPLE_RATE),
                "-c:a",
                "pcm_s16le",
                "-map_metadata",
                "-1",
                str(cleaned_path),
            ]
        )

        validate_wav(cleaned_path)
        os.replace(cleaned_path, path)
        temporary_paths.remove(cleaned_path)
    finally:
        for temporary_path in temporary_paths:
            temporary_path.unlink(missing_ok=True)


def main() -> int:
    """Generate missing lines, or overwrite all lines when requested."""
    parser = ArgumentParser()
    parser.add_argument(
        "--overwrite",
        "--force",
        dest="overwrite",
        action="store_true",
        help="Regenerate and overwrite every existing production WAV.",
    )
    args = parser.parse_args()

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    for line_id, _canonical_words, prompt in LINES:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.overwrite:
            frame_count = validate_wav(output_path)
            print(
                f"SKIP {output_path.name} ({frame_count} frames)",
                flush=True,
            )
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
        frame_count = validate_wav(output_path)
        print(
            f"GENERATED {output_path.name} ({frame_count} frames)",
            flush=True,
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
