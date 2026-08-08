"""Generate Priya's production dialogue with the selected Aaira voice."""

from argparse import ArgumentParser
from pathlib import Path
import os
import sys
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
VOICE_ID = "1XNFRxE3WBB7iI0jnm7p"  # Aaira
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000

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


def write_wav(path: Path, pcm: bytes) -> None:
    """Write raw 24 kHz mono PCM returned by ElevenLabs to a WAV container."""
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def main() -> int:
    """Generate missing lines, or every line when --force is supplied."""
    parser = ArgumentParser()
    parser.add_argument(
        "--force",
        action="store_true",
        help="Regenerate and overwrite every existing production WAV.",
    )
    args = parser.parse_args()

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    for line_id, prompt in LINES:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.force:
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
        if not pcm:
            raise RuntimeError(f"Empty audio response for {line_id}")
        write_wav(output_path, pcm)
        print(f"GENERATED {output_path.name}", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
