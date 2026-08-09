"""Generate Priya's production dialogue with the selected Aaira voice."""

from argparse import ArgumentParser
from pathlib import Path
import os
import re
import shutil
import struct
import subprocess
import sys
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
VOICE_ID = "1XNFRxE3WBB7iI0jnm7p"  # Aaira
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
SAMPLE_WIDTH = 2
CHANNELS = 1
FADE_OUT_MILLISECONDS = 20
TRAILING_SILENCE_MILLISECONDS = 150
DENOISE_FILTER = "afftdn=nr=12:nf=-55:tn=1:gs=5"
_DAVID_RE = re.compile(r"\bDavid\b", re.IGNORECASE)

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
        "PRIYA-001",
        "[worried] Guys—help! Something’s happened to Nick.\n\n"
        "IVY! AARON! DAVID!\n\nPlease—come here!",
    ),
    ("PRIYA-002", "[panicked] What do we do?! What do we do?!"),
    ("PRIYA-003", "[stunned] How did this happen?"),
    ("PRIYA-004", "[skeptical] All night?"),
    ("PRIYA-005", "[realizing] The door was locked... who locked it?"),
    ("PRIYA-006", "[softly] Nick? ... Nick, can you hear me?"),
    (
        "PRIYA-007",
        "[panicked but clear] Police? Our friend is hurt! We found him "
        "outside—in the snow. Please send someone. Please hurry!",
    ),
    (
        "PRIYA-008",
        "[shaken] What happened...? Why won’t anyone tell me what happened?",
    ),
    (
        "PRIYA-014",
        "[warmly amused] Fifteen years—and you two still act exactly the same.",
    ),
    ("PRIYA-015", "[playfully] And two years for these two."),
    ("PRIYA-016", "[warm, lightly wistful] To us... somehow."),
)


def condition_pcm_tail(pcm: bytes) -> bytes:
    """Fade the final 20 ms to zero and append 150 ms of digital silence."""
    if not pcm:
        raise ValueError("Cannot condition an empty PCM response.")
    if len(pcm) % SAMPLE_WIDTH:
        raise ValueError("PCM response does not contain complete 16-bit samples.")

    sample_count = len(pcm) // SAMPLE_WIDTH
    samples = list(struct.unpack(f"<{sample_count}h", pcm))
    fade_sample_count = min(
        sample_count,
        SAMPLE_RATE * FADE_OUT_MILLISECONDS // 1_000,
    )
    fade_start = sample_count - fade_sample_count
    fade_denominator = max(fade_sample_count - 1, 1)
    for offset in range(fade_sample_count):
        gain = (fade_sample_count - 1 - offset) / fade_denominator
        samples[fade_start + offset] = round(samples[fade_start + offset] * gain)

    silence_sample_count = SAMPLE_RATE * TRAILING_SILENCE_MILLISECONDS // 1_000
    samples.extend([0] * silence_sample_count)
    return struct.pack(f"<{len(samples)}h", *samples)


def write_pcm_wav(path: Path, pcm: bytes) -> None:
    """Write raw 24 kHz mono 16-bit PCM to a WAV container."""
    with wave.open(str(path), "wb") as output:
        output.setnchannels(CHANNELS)
        output.setsampwidth(SAMPLE_WIDTH)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def read_pcm_wav(path: Path) -> bytes:
    """Read a WAV after checking the production audio format."""
    with wave.open(str(path), "rb") as source:
        actual = (
            source.getnchannels(),
            source.getframerate(),
            source.getsampwidth(),
            source.getcomptype(),
        )
        expected = (CHANNELS, SAMPLE_RATE, SAMPLE_WIDTH, "NONE")
        if actual != expected:
            raise ValueError(f"Unexpected WAV format from FFmpeg: {actual}")
        return source.readframes(source.getnframes())


def write_wav_safely(path: Path, pcm: bytes) -> None:
    """Denoise and finish raw PCM before atomically replacing the target WAV."""
    ffmpeg_path = shutil.which("ffmpeg")
    if not ffmpeg_path:
        raise RuntimeError("ffmpeg is required for production voice denoising.")

    source_path = path.with_name(f".{path.stem}.source.wav")
    denoised_path = path.with_name(f".{path.stem}.denoised.wav")
    temporary_path = path.with_name(f".{path.stem}.finished.wav")
    try:
        write_pcm_wav(source_path, pcm)
        subprocess.run(
            (
                ffmpeg_path,
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-i",
                str(source_path),
                "-af",
                DENOISE_FILTER,
                "-ar",
                str(SAMPLE_RATE),
                "-ac",
                str(CHANNELS),
                "-c:a",
                "pcm_s16le",
                str(denoised_path),
            ),
            check=True,
        )
        conditioned_pcm = condition_pcm_tail(read_pcm_wav(denoised_path))
        write_pcm_wav(temporary_path, conditioned_pcm)
        temporary_path.replace(path)
    finally:
        source_path.unlink(missing_ok=True)
        denoised_path.unlink(missing_ok=True)
        temporary_path.unlink(missing_ok=True)


def apply_pronunciation_aliases(text: str) -> str:
    """Enforce DAY-vid without changing the canonical line manifest."""
    return _DAVID_RE.sub(
        lambda match: "DAY-VID" if match.group(0).isupper() else "Day-vid",
        text,
    )


def main() -> int:
    """Generate missing lines, or every line when --force is supplied."""
    parser = ArgumentParser()
    parser.add_argument(
        "--force",
        action="store_true",
        help="Regenerate and overwrite every existing production WAV.",
    )
    parser.add_argument(
        "--only",
        metavar="ID",
        action="append",
        help="Render only this line ID. Repeatable.",
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

    client = None
    for line_id, prompt in lines:
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
            text=apply_pronunciation_aliases(prompt),
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=VOICE_SETTINGS,
        )
        pcm = b"".join(chunks)
        if not pcm:
            raise RuntimeError(f"Empty audio response for {line_id}")
        write_wav_safely(output_path, pcm)
        print(f"GENERATED {output_path.name}", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
