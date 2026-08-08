"""Generate Aaron's grounded-jock voice audition round."""

from pathlib import Path
import os
import sys
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_ROOT = Path(__file__).resolve().parent / "jock-round"
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

# Candidate 1: Brian. ElevenLabs voice ID: nPczCjzI2devNBz1zQrb
# Candidate 2: Daniel. ElevenLabs voice ID: onwK4e9ZLuTAKqWW03F9
# Candidate 3: George. ElevenLabs voice ID: JBFqnCBsd6RMkjVDRZzb
# Candidate 4: Eric. ElevenLabs voice ID: cjVigY5qzO86Huf0OWal
# Candidate 5: Liam. ElevenLabs voice ID: TX3LPaxmHKxFdv7VOQHJ
# Candidate 6: Will. ElevenLabs voice ID: bIHbv24MWmeRgasZH58o
# Candidate 7: Callum. ElevenLabs voice ID: N2lVS1w4EtoT3dr4eOWO
# Candidate 8: Roger. ElevenLabs voice ID: CwhRBWXzGAHq8TQ4Fs17
CANDIDATES = (
    (1, "Brian", "nPczCjzI2devNBz1zQrb", "01-brian.wav"),
    (2, "Daniel", "onwK4e9ZLuTAKqWW03F9", "02-daniel.wav"),
    (3, "George", "JBFqnCBsd6RMkjVDRZzb", "03-george.wav"),
    (4, "Eric", "cjVigY5qzO86Huf0OWal", "04-eric.wav"),
    (5, "Liam", "TX3LPaxmHKxFdv7VOQHJ", "05-liam.wav"),
    (6, "Will", "bIHbv24MWmeRgasZH58o", "06-will.wav"),
    (7, "Callum", "N2lVS1w4EtoT3dr4eOWO", "07-callum.wav"),
    (8, "Roger", "CwhRBWXzGAHq8TQ4Fs17", "08-roger.wav"),
)

LINES = (
    (
        "line-01-body",
        "AARON-001",
        "[confident, athletic, urgent but controlled] He’s freezing. "
        "Let’s get him inside—onto the sofa, by the fire.",
    ),
    (
        "line-02-deflection",
        "AARON-002",
        "[controlled, firm, redirecting] Priya. Not now.",
    ),
    (
        "line-03-command",
        "AARON-003",
        "[terse, physically decisive] Lift on three.",
    ),
    (
        "line-04-two-years",
        "AARON-005",
        "[quiet, flat, stunned] ...Two years?",
    ),
)


def write_wav(path: Path, pcm: bytes) -> None:
    """Write raw mono PCM to a WAV container."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def main() -> int:
    """Generate any missing jock-round candidates."""
    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    for line_slug, line_id, prompt in LINES:
        for number, voice_name, voice_id, filename in CANDIDATES:
            output_path = OUTPUT_ROOT / line_slug / filename
            if output_path.exists() and output_path.stat().st_size > 44:
                print(
                    f"SKIP {line_id} candidate {number}: {voice_name}",
                    flush=True,
                )
                continue

            chunks = client.text_to_speech.convert(
                voice_id=voice_id,
                text=prompt,
                model_id=MODEL_ID,
                output_format=OUTPUT_FORMAT,
                voice_settings=VOICE_SETTINGS,
            )
            pcm = b"".join(chunks)
            if not pcm:
                raise RuntimeError(
                    f"Empty audio response for {line_id} candidate {number}"
                )
            write_wav(output_path, pcm)
            print(
                f"GENERATED {line_id} candidate {number}: {voice_name}",
                flush=True,
            )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
