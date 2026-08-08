"""Generate Nick's production dialogue with the selected Ivan Energetic voice."""

from argparse import ArgumentParser
from pathlib import Path
import os
import re
import sys
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
VOICE_ID = "JKtNvDNrWu33P1xzttP2"  # Ivan Energetic
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
SAMPLE_WIDTH = 2
CHANNELS = 1
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
        "NICK-001",
        "[frustrated, avoiding the conversation] Not tonight, David. "
        "I can’t do this with you right now. I need some air.",
    ),
    ("NICK-002", "[fondly teasing] He was worse at seventeen."),
    ("NICK-003", "[dryly, joking] Unfortunately."),
    ("NICK-004", "[warm, teasing] Here. You look fucking freezing."),
    (
        "NICK-005",
        "[slightly drunk, careless] You’ve been saying “after this trip” "
        "for two years.",
    ),
    ("NICK-006", "[angry, humiliated] He already knows."),
    (
        "NICK-007",
        "[tense, trying to end the argument] I need some air.",
    ),
)


def write_wav_safely(path: Path, pcm: bytes) -> None:
    """Write raw mono PCM to a temporary WAV, then atomically replace the target."""
    if not pcm:
        raise ValueError("Cannot write an empty PCM response.")
    if len(pcm) % SAMPLE_WIDTH:
        raise ValueError("PCM response does not contain complete 16-bit samples.")

    temporary_path = path.with_name(f".{path.name}.tmp")
    try:
        with wave.open(str(temporary_path), "wb") as output:
            output.setnchannels(CHANNELS)
            output.setsampwidth(SAMPLE_WIDTH)
            output.setframerate(SAMPLE_RATE)
            output.writeframes(pcm)
        temporary_path.replace(path)
    finally:
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

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)
    client = ElevenLabs(api_key=api_key)
    for line_id, prompt in lines:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.force:
            print(f"SKIP {output_path.name}", flush=True)
            continue

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
