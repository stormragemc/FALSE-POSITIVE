"""Generate soundtrack style candidates for FALSE POSITIVE via the ElevenLabs Music API.

Auditioning tool, not a build step. Writes MP3s to ArtSource/AudioCandidates/Music/,
which is gitignored — nothing lands in Assets/ until a style is chosen.

Every candidate renders the SAME dramatic moment (the interrogation-loop bed) so the
styles can be judged against each other rather than against different scenes.

Usage:
    python3 Tools/generate_music_candidates.py                # all styles
    python3 Tools/generate_music_candidates.py pressure storm # named styles only
    python3 Tools/generate_music_candidates.py --list         # no API calls

The API key is read from $ELEVENLABS_API_KEY, falling back to the ELEVENLABS_API_KEY
line in Sidecar/.env. It is never printed, logged, or written to disk by this script.

Stdlib only — no pip install, no venv.
"""

from __future__ import annotations

import os
import sys
import json
import urllib.error
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
OUT_DIR = REPO / "ArtSource" / "AudioCandidates"
ENDPOINT = "https://api.elevenlabs.io/v1/music"
MODEL_ID = "music_v1"
LENGTH_MS = 45_000

# Shared brief appended to every prompt.
#
# The two hard constraints come from the game, not from taste:
#   1. The mic is open. VAD drives the whole loop, so anything that leaks from the
#      speakers into the mic can trip or mask the player's turn. Sparse and quiet.
#   2. Officer Spassky's TTS has to stay intelligible over the top, which means
#      leaving the 300 Hz - 4 kHz speech band as clear as the style allows.
COMMON = (
    "Instrumental only, no vocals, no vocal samples, no spoken word. "
    "No drum kit, no percussion loop, no beat. "
    "Very sparse and quiet, with a low noise floor and plenty of space — this is an "
    "underscore that sits beneath dialogue and must never compete with a speaking voice. "
    "Keep the midrange between 300 Hz and 4 kHz uncluttered. "
    "No dramatic climax, no big swell, no resolution — it stays unresolved throughout. "
    "Consistent texture from start to end so it can be seamlessly looped."
)

STYLES: dict[str, dict[str, str]] = {
    "pressure": {
        "file": "music_A_pressure.mp3",
        "label": "A — Pressure (sub-bass drone)",
        "note": "Barely music. Safest possible bed for an open mic.",
        "prompt": (
            "Dark ambient underscore for a police interrogation scene in a cold room. "
            "A single sustained sub-bass drone in D, almost motionless, with very slow "
            "swells that rise and fall over about twenty seconds. Faint low brass sustain "
            "underneath it. No melody, no chord changes, no arpeggios. Almost all of the "
            "energy sits below 200 Hz. Oppressive, patient, physically heavy — the sound "
            "of being kept in a room. "
        ),
    },
    "testimony": {
        "file": "music_B_testimony.mp3",
        "label": "B — Testimony (solo cello)",
        "note": "Human and mournful. Scores David's decency, not the accusation.",
        "prompt": (
            "Score for a serious literary crime drama. Solo cello, close-miked and dry, "
            "playing long sustained bowed notes in a minor key, very slow, with audible "
            "bow noise and rosin against the string. A double bass pedal note underneath, "
            "rarely. Long silences between phrases — the instrument is alone in the room. "
            "Mournful, restrained, dignified. Scandinavian noir, not Hollywood thriller. "
        ),
    },
    "interview": {
        "file": "music_C_interview.mp3",
        "label": "C — Interview room (felt piano + tape)",
        "note": "Procedural slow-burn. The most conventional, most instantly readable.",
        "prompt": (
            "Score for a slow-burn detective drama. Felt-hammer upright piano, single "
            "notes and bare two-note intervals with long natural decay, played sparsely "
            "and very quietly in a minor key. Warm analogue tape hiss and faint room tone "
            "running underneath throughout. An occasional low string swell far back in the "
            "mix. Melancholy, patient, procedural, unglamorous. "
        ),
    },
    "machine": {
        "file": "music_D_machine.mp3",
        "label": "D — The machine (granular / analytical)",
        "note": "Scores the detective as an AI. Closest to the game's actual thesis.",
        "prompt": (
            "Cold electronic underscore for a scene in which an artificial intelligence "
            "analyses a human voice for signs of stress. Granular processed texture, faint "
            "metallic shimmer, sparse irregular clicks and quiet data-like ticks, over one "
            "slowly evolving low drone. Tonal, but with no melody and no chord progression. "
            "Clinical, watchful, alien, with no warmth and no sympathy. "
        ),
    },
    "storm": {
        "file": "music_E_storm.mp3",
        "label": "E — Storm (ambience-led, near-scoreless)",
        "note": "Diegetic-adjacent: the storm is what actually killed Nick.",
        "prompt": (
            "Winter ambience underscore with almost no music in it. Wind pressing against "
            "a wooden cabin, faint structural creaks and settling timber, a distant low "
            "howl, snow-muffled air. Beneath it, one barely audible sustained low string "
            "drone giving only the faintest hint of tonality. No recognisable instruments "
            "in the foreground. Bleak, cold, isolated, documentary-real. "
        ),
    },
}


def read_api_key() -> str:
    """Env var first, then the ELEVENLABS_API_KEY line in Sidecar/.env.

    The value is returned for use in the request header and never surfaced anywhere else.
    """
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if key:
        return key

    env_path = REPO / "Sidecar" / ".env"
    if env_path.is_file():
        for line in env_path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line.startswith("ELEVENLABS_API_KEY"):
                _, _, value = line.partition("=")
                value = value.strip().strip("'\"")
                if value:
                    return value

    sys.exit(
        "No ELEVENLABS_API_KEY found.\n"
        "  Set it in the shell:  export ELEVENLABS_API_KEY=...\n"
        "  or add a line to:     Sidecar/.env"
    )


def generate(name: str, style: dict[str, str], key: str) -> bool:
    out_path = OUT_DIR / style["file"]
    if out_path.exists():
        print(f"  {style['label']}: already exists, skipping")
        return True

    body = json.dumps(
        {
            "prompt": style["prompt"] + COMMON,
            "music_length_ms": LENGTH_MS,
            "model_id": MODEL_ID,
        }
    ).encode("utf-8")

    request = urllib.request.Request(
        ENDPOINT,
        data=body,
        headers={
            "xi-api-key": key,
            "Content-Type": "application/json",
            "Accept": "audio/mpeg",
        },
        method="POST",
    )

    print(f"  {style['label']}: generating {LENGTH_MS // 1000}s ...", flush=True)
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            audio = response.read()
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", "replace")[:400]
        hint = {
            401: "key rejected — check ELEVENLABS_API_KEY",
            402: "out of credits — Eleven Music bills per second of audio",
            403: "your plan may not grant API access to Eleven Music",
            422: "the API rejected the request body (model id or length)",
        }.get(exc.code, "")
        print(f"    FAILED {exc.code} {hint}\n    {detail}", file=sys.stderr)
        return False
    except urllib.error.URLError as exc:
        print(f"    FAILED network: {exc.reason}", file=sys.stderr)
        return False

    if not audio:
        print("    FAILED empty response", file=sys.stderr)
        return False

    out_path.write_bytes(audio)
    print(f"    -> {out_path.relative_to(REPO)}  ({len(audio) / 1024:.0f} KB)")
    return True


def main() -> int:
    argv = [a for a in sys.argv[1:] if a != "--list"]
    listing = "--list" in sys.argv

    if listing:
        for name, style in STYLES.items():
            print(f"{name:<10} {style['label']}\n           {style['note']}")
        return 0

    unknown = [a for a in argv if a not in STYLES]
    if unknown:
        print(f"Unknown style(s): {', '.join(unknown)}", file=sys.stderr)
        print(f"Available: {', '.join(STYLES)}", file=sys.stderr)
        return 2

    selected = argv or list(STYLES)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    key = read_api_key()

    print(f"Generating {len(selected)} candidate(s) into {OUT_DIR.relative_to(REPO)}/\n")
    failures = [name for name in selected if not generate(name, STYLES[name], key)]

    print()
    if failures:
        print(f"{len(selected) - len(failures)}/{len(selected)} succeeded. "
              f"Failed: {', '.join(failures)}")
        return 1
    print(f"All {len(selected)} candidates written. Listen, then tell Claude which style wins.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
