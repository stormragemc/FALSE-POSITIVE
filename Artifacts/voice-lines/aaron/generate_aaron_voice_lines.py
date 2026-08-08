"""Generate Aaron's production dialogue with the selected Liam voice."""

from argparse import ArgumentParser
from pathlib import Path
import os
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
        "[confident, athletic, urgent but controlled] He’s freezing. "
        "Let’s get him inside—onto the sofa, by the fire.",
    ),
    (
        "AARON-002",
        "Priya. Not now.",
        "[controlled, firm, redirecting] Priya. Not now.",
    ),
    (
        "AARON-003",
        "Lift on three.",
        "[terse, physically decisive] Lift on three.",
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


def validate_wav(path: Path) -> int:
    """Validate the production WAV contract and return its frame count."""
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
        return frame_count


def write_wav_safely(path: Path, pcm: bytes) -> None:
    """Validate raw PCM, then atomically install it in a WAV container."""
    if not pcm:
        raise ValueError(f"Empty audio response for {path.stem}")
    if len(pcm) % SAMPLE_WIDTH:
        raise ValueError(f"Unaligned PCM response for {path.stem}")

    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            dir=path.parent,
            prefix=f".{path.stem}.",
            suffix=".tmp.wav",
            delete=False,
        ) as temporary:
            temporary_path = Path(temporary.name)

        with wave.open(str(temporary_path), "wb") as output:
            output.setnchannels(CHANNELS)
            output.setsampwidth(SAMPLE_WIDTH)
            output.setframerate(SAMPLE_RATE)
            output.writeframes(pcm)

        validate_wav(temporary_path)
        os.replace(temporary_path, path)
        temporary_path = None
    finally:
        if temporary_path is not None:
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
