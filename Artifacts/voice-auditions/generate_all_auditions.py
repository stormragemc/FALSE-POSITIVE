"""Generate missing FALSE POSITIVE voice audition WAV files.

The ElevenLabs API key must be supplied through ELEVENLABS_API_KEY. The key is
read from the process environment and is never written to disk.
"""

from argparse import ArgumentParser
from pathlib import Path
import os
import sys
import time
import wave

from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


BASE_DIR = Path(__file__).resolve().parent
MODEL_ID = "eleven_multilingual_v2"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000

VOICE_SETTINGS = VoiceSettings(
    stability=0.40,
    similarity_boost=0.75,
    style=0.55,
    use_speaker_boost=True,
    speed=1.00,
)

# Neutral male pool. Used for David and Aaron.
NEUTRAL_MALE = (
    ("01-brian", "Brian", "nPczCjzI2devNBz1zQrb"),
    ("02-daniel", "Daniel", "onwK4e9ZLuTAKqWW03F9"),
    ("03-george", "George", "JBFqnCBsd6RMkjVDRZzb"),
    ("04-eric", "Eric", "cjVigY5qzO86Huf0OWal"),
    ("05-liam", "Liam", "TX3LPaxmHKxFdv7VOQHJ"),
    ("06-will", "Will", "bIHbv24MWmeRgasZH58o"),
    ("07-callum", "Callum", "N2lVS1w4EtoT3dr4eOWO"),
    ("08-roger", "Roger", "CwhRBWXzGAHq8TQ4Fs17"),
)

# Russian male officer pool. Used for Officer Spassky.
SPASSKY_RUSSIAN_MALE = (
    ("01-stanislav", "Stanislav", "ogi2DyUAKJb7CEdqqvlU"),
    ("02-alexei", "Alexei", "NQJnREzQtnAHHZnia0tY"),
    ("03-ivan", "Ivan", "1qd9R09Ljlx9V1Ok0t5S"),
    ("04-denis", "Denis", "1EVds7FNGSXoKeOiMXuf"),
    ("05-alex-bell", "Alex Bell", "TUQNWEvVPBLzMBSVDPUA"),
    ("06-artem-lebedev", "Artem Lebedev", "rQOBu7YxCDxGiFdTm28w"),
    ("07-dmitry", "Dmitry", "vnUSJFFoxRr5JFjw51pu"),
    ("08-valery", "Valery", "gXMhWmiqsFkrcssqVb5k"),
)

# Russian male character pool. Used for Nick.
NICK_RUSSIAN_MALE = (
    ("01-ivan", "Ivan", "1qd9R09Ljlx9V1Ok0t5S"),
    ("02-denis", "Denis", "0BcDz9UPwL3MpsnTeUlO"),
    ("03-ivan-energetic", "Ivan Energetic", "JKtNvDNrWu33P1xzttP2"),
    ("04-alexei", "Alexei", "NQJnREzQtnAHHZnia0tY"),
    ("05-oleg", "Oleg", "MWyJiWDobXN8FX3CJTdE"),
    ("06-guy", "Guy", "zvm1P65eFt40xSwMli2k"),
    ("07-alex-bell", "Alex Bell", "TUQNWEvVPBLzMBSVDPUA"),
    ("08-escobar", "Escobar", "XGyi3FDBCYWBQ6vRd0FV"),
)

# Historical neutral female pool. Ivy's finalized selection is Laura using
# eleven_v3; see ../voice-lines/ivy/README.md for production settings.
NEUTRAL_FEMALE = (
    ("01-jessica", "Jessica", "cgSgspJ2msm6clMCkdW9"),
    ("02-matilda", "Matilda", "XrExE9yKIg1WjnnlVkGX"),
    ("03-laura", "Laura", "FGY2WhTYpPnrIDTdsKH5"),
    ("04-lily", "Lily", "pFZP5JQG7iQjIQuC4Bku"),
    ("05-sarah", "Sarah", "EXAVITQu4vr4xnSDxMaL"),
    ("06-alice", "Alice", "Xb7hH8MSUJpSbSDYk0k2"),
    ("07-aria", "Aria", "9BWtsMINqrJLrRacOk9x"),
    ("08-charlotte", "Charlotte", "XB0fDUnXU5powFXDhCwa"),
)

# Historical Indian female pool. Priya's finalized selection is Aaira using
# eleven_v3; see ../voice-lines/priya/README.md for production settings.
PRIYA_INDIAN_FEMALE = (
    ("01-anika", "Anika", "90ipbRoKi4CpHXvKVtl0"),
    ("02-monika-sogam", "Monika Sogam", "2zRM7PkgwBPiau2jvVXc"),
    ("03-mahi", "Mahi", "yD0Zg2jxgfQLY8I2MEHO"),
    ("04-aisha", "Aisha", "MjJrIRgwH0lZCuxcakAW"),
    ("05-aaira", "Aaira", "1XNFRxE3WBB7iI0jnm7p"),
    ("06-aaliyah", "Aaliyah", "aUTn6mevnrM9pqtesisb"),
    ("07-aasha", "Aasha", "rxvktZTNrsQlsGIpOQGz"),
    ("08-saavi", "Saavi", "a4BpQNxKFbuzzTj2JRQc"),
)

# Mixed professional pool. Used for the radio announcer.
RADIO_PROFESSIONAL = (
    ("01-roger", "Roger", "CwhRBWXzGAHq8TQ4Fs17"),
    ("02-sarah", "Sarah", "EXAVITQu4vr4xnSDxMaL"),
    ("03-daniel", "Daniel", "onwK4e9ZLuTAKqWW03F9"),
    ("04-matilda", "Matilda", "XrExE9yKIg1WjnnlVkGX"),
    ("05-george", "George", "JBFqnCBsd6RMkjVDRZzb"),
    ("06-jessica", "Jessica", "cgSgspJ2msm6clMCkdW9"),
    ("07-chris", "Chris", "iP95p4xoKVk53GoZ742B"),
    ("08-alice", "Alice", "Xb7hH8MSUJpSbSDYk0k2"),
)

AUDITIONS = (
    (
        "david",
        NEUTRAL_MALE,
        (
            ("line-01-confusion", "Who are you? Where am I?"),
            (
                "line-02-confession",
                "Nick and Ivy. They'd been seeing each other for two years. "
                "I knew, and I kept it from Aaron.",
            ),
            (
                "line-03-defense",
                "Because I didn't kill Nick. I argued with him, let him walk out "
                "into that storm, and passed out. I did not lock that door.",
            ),
        ),
    ),
    (
        "spassky",
        SPASSKY_RUSSIAN_MALE,
        (
            (
                "line-01-introduction",
                "I'm Officer Spassky. Nick is dead, and right now you're one of "
                "the suspects. Take your time and tell me everything you remember "
                "from last night.",
            ),
            (
                "line-02-evidence-pressure",
                "You were unconscious when someone turned that key. You can draw "
                "a conclusion, but you cannot say you saw it happen.",
            ),
            ("line-03-verdict", "Tell me why I should spare your life."),
        ),
    ),
    (
        "nick",
        NICK_RUSSIAN_MALE,
        (
            (
                "line-01-fire-argument",
                "Not tonight, David. I can't do this with you right now. I need "
                "some air.",
            ),
            (
                "line-02-two-years",
                "You've been saying \"after this trip\" for two years.",
            ),
        ),
    ),
    (
        "aaron",
        NEUTRAL_MALE,
        (
            (
                "line-01-body",
                "He's freezing. Let's get him inside, onto the sofa by the fire.",
            ),
            ("line-02-deflection", "Priya. Not now."),
            ("line-03-command", "Lift on three."),
        ),
    ),
    (
        "ivy",
        NEUTRAL_FEMALE,
        (
            (
                "line-01-shock",
                "Oh my God. What happened to him? What do we do now?",
            ),
            ("line-02-alibi", "I don't know. I was upstairs with Aaron."),
            ("line-03-confirmation", "Yes. All night."),
        ),
    ),
    (
        "priya",
        PRIYA_INDIAN_FEMALE,
        (
            (
                "line-01-panic",
                "Guys! Help! Something's happened to Nick! Ivy! Aaron! David! "
                "Please, come here!",
            ),
            ("line-02-suspicion", "All night?"),
            ("line-03-concern", "Nick? Nick, can you hear me?"),
        ),
    ),
    (
        "radio-announcer",
        RADIO_PROFESSIONAL,
        (
            (
                "line-01-storm-warning",
                "A snowstorm is moving through the area. Please stay indoors "
                "until conditions improve.",
            ),
        ),
    ),
)


def write_wav(path: Path, pcm: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def main() -> int:
    parser = ArgumentParser()
    parser.add_argument(
        "--character",
        action="append",
        choices=tuple(audition[0] for audition in AUDITIONS),
        help="Generate only this character; may be supplied more than once.",
    )
    args = parser.parse_args()

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    failures: list[str] = []

    for character, candidates, lines in AUDITIONS:
        if args.character and character not in args.character:
            continue

        for line_slug, text in lines:
            for file_slug, voice_name, voice_id in candidates:
                output_path = BASE_DIR / character / line_slug / f"{file_slug}.wav"
                if output_path.exists() and output_path.stat().st_size > 44:
                    print(f"SKIP {output_path.relative_to(BASE_DIR)}", flush=True)
                    continue

                started = time.perf_counter()
                try:
                    chunks = client.text_to_speech.convert(
                        voice_id=voice_id,
                        text=text,
                        model_id=MODEL_ID,
                        output_format=OUTPUT_FORMAT,
                        voice_settings=VOICE_SETTINGS,
                    )
                    pcm = b"".join(chunks)
                    if not pcm:
                        raise RuntimeError("empty audio response")
                    write_wav(output_path, pcm)
                    elapsed_ms = int((time.perf_counter() - started) * 1000)
                    print(
                        f"CREATED {output_path.relative_to(BASE_DIR)} "
                        f"voice={voice_name} bytes={len(pcm)} elapsed_ms={elapsed_ms}",
                        flush=True,
                    )
                except Exception as error:
                    relative_path = output_path.relative_to(BASE_DIR)
                    failures.append(str(relative_path))
                    print(f"FAILED {relative_path}: {error}", file=sys.stderr, flush=True)

    if failures:
        print(f"Generation failed for {len(failures)} file(s).", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
