"""Generate Ivy's production dialogue with the selected Laura voice."""

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
VOICE_ID = "FGY2WhTYpPnrIDTdsKH5"  # Laura
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
SAMPLE_WIDTH = 2
DENOISE_FILTER = "afftdn=nr=12:nf=-55:tn=1:gs=5"
FADE_DURATION_SECONDS = 0.020
SILENCE_DURATION_SECONDS = 0.150

VOICE_SETTINGS = VoiceSettings(
    stability=0.50,
    similarity_boost=0.75,
    style=0.00,
    use_speaker_boost=True,
    speed=1.00,
)

# Tags and punctuation direct the performance without changing the spoken words.
LINES = (
    (
        "IVY-001",
        "[shocked but restrained] Oh my God.\n\n"
        "What happened to him?\n\n"
        "[quieter, asking the others for direction] What do we do now?",
    ),
    (
        "IVY-002",
        "[guarded, answering quickly] I don’t know. I was upstairs with Aaron.",
    ),
    ("IVY-003", "[guarded] Yes. All night."),
    (
        "IVY-004",
        "[hushed, practical, concerned] Careful... careful... easy... "
        "[short pause]",
    ),
)
LINE_IDS = tuple(line_id for line_id, _ in LINES)


def make_temporary_path(label: str) -> Path:
    """Reserve a temporary path beside the production files."""
    file_descriptor, name = tempfile.mkstemp(
        prefix=f".ivy-{label}-",
        suffix=".wav",
        dir=OUTPUT_DIRECTORY,
    )
    os.close(file_descriptor)
    return Path(name)


def write_wav(path: Path, pcm: bytes) -> None:
    """Write raw 24 kHz mono PCM returned by ElevenLabs to a WAV container."""
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(SAMPLE_WIDTH)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def denoise_wav(source_path: Path, output_path: Path, ffmpeg: str) -> None:
    """Apply the documented conservative broadband cleanup with FFmpeg."""
    subprocess.run(
        (
            ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(source_path),
            "-af",
            DENOISE_FILTER,
            "-ac",
            "1",
            "-ar",
            str(SAMPLE_RATE),
            "-c:a",
            "pcm_s16le",
            str(output_path),
        ),
        check=True,
    )


def add_clean_tail(source_path: Path, output_path: Path) -> None:
    """Fade the final 20 ms to zero, then append 150 ms of digital silence."""
    with wave.open(str(source_path), "rb") as source:
        channels = source.getnchannels()
        sample_width = source.getsampwidth()
        sample_rate = source.getframerate()
        frames = source.readframes(source.getnframes())

    if (channels, sample_width, sample_rate) != (1, SAMPLE_WIDTH, SAMPLE_RATE):
        raise RuntimeError(
            "Unexpected denoised format: "
            f"channels={channels}, width={sample_width}, rate={sample_rate}"
        )

    samples = array("h")
    samples.frombytes(frames)
    if sys.byteorder != "little":
        samples.byteswap()
    if not samples:
        raise RuntimeError("Denoised audio contains no samples")

    fade_sample_count = min(
        len(samples),
        round(SAMPLE_RATE * FADE_DURATION_SECONDS),
    )
    fade_start = len(samples) - fade_sample_count
    denominator = max(1, fade_sample_count - 1)
    for offset in range(fade_sample_count):
        gain = (fade_sample_count - 1 - offset) / denominator
        samples[fade_start + offset] = round(samples[fade_start + offset] * gain)

    silence_sample_count = round(SAMPLE_RATE * SILENCE_DURATION_SECONDS)
    samples.extend([0] * silence_sample_count)
    if sys.byteorder != "little":
        samples.byteswap()

    with wave.open(str(output_path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(SAMPLE_WIDTH)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(samples.tobytes())


def validate_wav(path: Path) -> None:
    """Reject a staged WAV that does not meet the production contract."""
    with wave.open(str(path), "rb") as source:
        channels = source.getnchannels()
        sample_width = source.getsampwidth()
        sample_rate = source.getframerate()
        frame_count = source.getnframes()
        compression = source.getcomptype()
        frames = source.readframes(frame_count)

    actual_format = (channels, sample_width, sample_rate, compression)
    expected_format = (1, SAMPLE_WIDTH, SAMPLE_RATE, "NONE")
    if actual_format != expected_format:
        raise RuntimeError(f"Unexpected staged WAV format: {actual_format}")
    if frame_count <= 0:
        raise RuntimeError("Staged WAV contains no frames")

    samples = array("h")
    samples.frombytes(frames)
    if sys.byteorder != "little":
        samples.byteswap()
    if not samples or not any(samples):
        raise RuntimeError("Staged WAV contains no audible signal")
    if any(abs(sample) >= 32767 for sample in samples):
        raise RuntimeError("Staged WAV contains full-scale sample clipping")

    required_zero_samples = round(SAMPLE_RATE * SILENCE_DURATION_SECONDS)
    if any(samples[-required_zero_samples:]):
        raise RuntimeError("Staged WAV does not contain the required zero tail")


def render_line(output_path: Path, pcm: bytes, ffmpeg: str) -> None:
    """Clean one response and atomically replace its production WAV."""
    source_path = make_temporary_path("source")
    denoised_path = make_temporary_path("denoised")
    staged_path = make_temporary_path("staged")
    temporary_paths = (source_path, denoised_path, staged_path)
    try:
        write_wav(source_path, pcm)
        denoise_wav(source_path, denoised_path, ffmpeg)
        add_clean_tail(denoised_path, staged_path)
        validate_wav(staged_path)
        os.replace(staged_path, output_path)
    finally:
        for path in temporary_paths:
            path.unlink(missing_ok=True)


def main() -> int:
    """Generate selected missing lines, or overwrite them when --force is used."""
    parser = ArgumentParser()
    parser.add_argument("line_ids", nargs="*", choices=LINE_IDS)
    parser.add_argument(
        "--force",
        action="store_true",
        help="Regenerate and overwrite selected existing production WAVs.",
    )
    args = parser.parse_args()

    selected_ids = set(args.line_ids or LINE_IDS)
    selected_lines = tuple(
        (line_id, prompt)
        for line_id, prompt in LINES
        if line_id in selected_ids
    )
    pending_lines = []
    for line_id, prompt in selected_lines:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.force:
            print(f"SKIP {output_path.name}", flush=True)
            continue
        pending_lines.append((line_id, prompt, output_path))

    if not pending_lines:
        return 0

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("ffmpeg is not available on PATH.", file=sys.stderr)
        return 3

    client = ElevenLabs(api_key=api_key)
    for line_id, prompt, output_path in pending_lines:
        chunks = client.text_to_speech.convert(
            voice_id=VOICE_ID,
            text=prompt,
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=VOICE_SETTINGS,
        )
        pcm = b"".join(chunks)
        if not pcm:
            raise RuntimeError(f"Empty audio response for {line_id}")
        render_line(output_path, pcm, ffmpeg)
        print(f"GENERATED {output_path.name}", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
