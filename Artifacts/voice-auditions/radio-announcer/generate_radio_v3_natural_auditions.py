"""Generate the clean V3 Natural radio-announcer audition round."""

from argparse import ArgumentParser
from pathlib import Path
import os
import sys
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_ROOT = Path(__file__).resolve().parent / "v3-natural-round"
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

# Candidate 1: Roger. ElevenLabs voice ID: CwhRBWXzGAHq8TQ4Fs17
# Candidate 2: Sarah. ElevenLabs voice ID: EXAVITQu4vr4xnSDxMaL
# Candidate 3: Daniel. ElevenLabs voice ID: onwK4e9ZLuTAKqWW03F9
# Candidate 4: Matilda. ElevenLabs voice ID: XrExE9yKIg1WjnnlVkGX
# Candidate 5: George. ElevenLabs voice ID: JBFqnCBsd6RMkjVDRZzb
# Candidate 6: Jessica. ElevenLabs voice ID: cgSgspJ2msm6clMCkdW9
# Candidate 7: Chris. ElevenLabs voice ID: iP95p4xoKVk53GoZ742B
# Candidate 8: Alice. ElevenLabs voice ID: Xb7hH8MSUJpSbSDYk0k2
CANDIDATES = (
    (1, "Roger", "CwhRBWXzGAHq8TQ4Fs17", "01-roger.wav"),
    (2, "Sarah", "EXAVITQu4vr4xnSDxMaL", "02-sarah.wav"),
    (3, "Daniel", "onwK4e9ZLuTAKqWW03F9", "03-daniel.wav"),
    (4, "Matilda", "XrExE9yKIg1WjnnlVkGX", "04-matilda.wav"),
    (5, "George", "JBFqnCBsd6RMkjVDRZzb", "05-george.wav"),
    (6, "Jessica", "cgSgspJ2msm6clMCkdW9", "06-jessica.wav"),
    (7, "Chris", "iP95p4xoKVk53GoZ742B", "07-chris.wav"),
    (8, "Alice", "Xb7hH8MSUJpSbSDYk0k2", "08-alice.wav"),
)

LINES = (
    (
        "RADIO-001",
        "line-01-storm-warning",
        "[neutral, impersonal, calm public safety announcement] A snowstorm is "
        "moving through the area. Please stay indoors until conditions improve.",
    ),
    (
        "RADIO-002",
        "line-02-snow-storm",
        "[neutral, impersonal, calm public safety announcement] ...snow storm...",
    ),
    (
        "RADIO-003",
        "line-03-stay-indoors",
        "[neutral, impersonal, calm public safety announcement] "
        "...please stay indoors...",
    ),
    (
        "RADIO-004",
        "line-04-during-these-times",
        "[neutral, impersonal, calm public safety announcement] "
        "...during these times.",
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
    """Generate any missing clean V3 Natural radio candidates."""
    parser = ArgumentParser()
    parser.add_argument(
        "--line",
        action="append",
        choices=tuple(line[0] for line in LINES),
        help="Generate this canonical line; defaults to RADIO-001.",
    )
    parser.add_argument(
        "--candidate",
        action="append",
        type=int,
        choices=tuple(candidate[0] for candidate in CANDIDATES),
        help="Generate only this numbered candidate; may be repeated.",
    )
    args = parser.parse_args()

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    selected_lines = set(args.line or ("RADIO-001",))
    selected_candidates = set(args.candidate or tuple(range(1, 9)))

    for line_id, line_slug, prompt in LINES:
        if line_id not in selected_lines:
            continue

        for number, voice_name, voice_id, filename in CANDIDATES:
            if number not in selected_candidates:
                continue

            output_path = OUTPUT_ROOT / line_slug / filename
            if output_path.exists() and output_path.stat().st_size > 44:
                print(f"SKIP {line_id} candidate {number}: {voice_name}", flush=True)
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
            print(f"GENERATED {line_id} candidate {number}: {voice_name}", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
