"""Generate Nick's two-voice final-decider audition."""

from pathlib import Path
import os
import sys
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = (
    Path(__file__).resolve().parent / "final-decider" / "line-03-freezing"
)
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

# Finalist 1: Ivan Energetic, original candidate 3.
# ElevenLabs voice ID: JKtNvDNrWu33P1xzttP2
# Finalist 2: Alexei, original candidate 4.
# ElevenLabs voice ID: NQJnREzQtnAHHZnia0tY
FINALISTS = (
    (1, "Ivan Energetic", "JKtNvDNrWu33P1xzttP2", "01-ivan-energetic.wav"),
    (2, "Alexei", "NQJnREzQtnAHHZnia0tY", "02-alexei.wav"),
)

PROMPT = "[warm, teasing] Here. You look fucking freezing."


def write_wav(path: Path, pcm: bytes) -> None:
    """Write raw mono PCM to a WAV container."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def main() -> int:
    """Generate missing finalist files."""
    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    for number, voice_name, voice_id, filename in FINALISTS:
        output_path = OUTPUT_DIRECTORY / filename
        if output_path.exists() and output_path.stat().st_size > 44:
            print(f"SKIP finalist {number}: {voice_name}", flush=True)
            continue

        chunks = client.text_to_speech.convert(
            voice_id=voice_id,
            text=PROMPT,
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=VOICE_SETTINGS,
        )
        pcm = b"".join(chunks)
        if not pcm:
            raise RuntimeError(f"Empty audio response for finalist {number}")
        write_wav(output_path, pcm)
        print(f"GENERATED finalist {number}: {voice_name}", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
