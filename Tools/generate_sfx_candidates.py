"""Generate SFX candidates for FALSE POSITIVE via the ElevenLabs sound-generation API.

Auditioning tool, not a build step. Writes MP3s to ArtSource/AudioCandidates/SFX/,
which is gitignored — nothing lands in Assets/ until a take is chosen.

The radio set covers the M1 beat end to end (STORY_SCRIPT.md ss.159-176):
free-roam hiss -> tuning minigame -> lock -> the storm-warning line sits on carrier_bed.

Usage:
    python3 Tools/generate_sfx_candidates.py                    # all cues
    python3 Tools/generate_sfx_candidates.py static_bed_harsh   # named cues only
    python3 Tools/generate_sfx_candidates.py --list             # no API calls

Key handling is shared with generate_music_candidates.py: $ELEVENLABS_API_KEY, then
the ELEVENLABS_API_KEY line in Sidecar/.env. Never printed, logged, or written to disk.

Stdlib only — no pip install, no venv.
"""

from __future__ import annotations

import sys
import json
import urllib.error
import urllib.request
from pathlib import Path

from generate_music_candidates import read_api_key

REPO = Path(__file__).resolve().parent.parent
OUT_DIR = REPO / "ArtSource" / "AudioCandidates"
ENDPOINT = "https://api.elevenlabs.io/v1/sound-generation"

# Same open-mic constraint as the music: this plays through the speakers while the
# player's mic is live, so nothing here should sit in the way of a speaking voice.
COMMON = " No music, no melody, no singing."

CUES: dict[str, dict] = {
    "static_bed_harsh": {
        "file": "sfx_radio_bed_harsh_11l.mp3",
        "label": "Radio bed, harsh — free roam, loop",
        "note": "The 'Fix the radio' objective bed. Abrasive; pushes you to fix it.",
        "seconds": 20,
        "loop": True,
        "prompt": (
            "Continuous harsh analogue radio static from an old mantel radio tuned "
            "between stations. Dense white-noise hiss with an unstable carrier tone "
            "drifting underneath, occasional crackle and brief bursts of interference. "
            "Faint unintelligible fragments of distant speech surface and vanish in the "
            "noise, never clear enough to make out a word. Constant level throughout."
        ),
    },
    "static_bed_soft": {
        "file": "sfx_radio_bed_soft_11l.mp3",
        "label": "Radio bed, soft — free roam, loop",
        "note": "Same beat, gentler. Livable under 2-3 minutes of exploration.",
        "seconds": 20,
        "loop": True,
        "prompt": (
            "Soft continuous analogue radio static from a small old radio across a room, "
            "muffled by its paper speaker. Warm low-mid hiss rather than bright white "
            "noise, a slow drifting carrier hum underneath, occasional gentle crackle. "
            "Very faint traces of distant unintelligible speech buried deep in the noise. "
            "Quiet, steady and unobtrusive, constant level throughout."
        ),
    },
    "tuning_sweep": {
        "file": "sfx_radio_tuning_11l.mp3",
        "label": "Tuning sweep — the dial minigame",
        "note": "One-axis dial hunt. Trim to taste and drive playback rate from dial speed.",
        "seconds": 12,
        "loop": False,
        "prompt": (
            "Turning the tuning dial of an old analogue radio slowly across the band. "
            "Heterodyne whistles sliding up and down in pitch, bursts of squelch, static "
            "swelling and thinning, fragments of distant stations passing by and breaking "
            "up. Faint mechanical friction of the dial turning."
        ),
    },
    "radio_lock": {
        "file": "sfx_radio_lock_11l.mp3",
        "label": "Lock / clears — CS-05 one-shot",
        "note": "The resolve moment. Storm-warning VO starts right after this.",
        "seconds": 6,
        "loop": False,
        "prompt": (
            "An analogue radio snapping into a clear station. Harsh static rapidly "
            "resolving and settling into a clean stable signal, with a short squelch pop "
            "at the moment it locks on, followed by quiet steady broadcast room tone."
        ),
    },
    "carrier_bed": {
        "file": "sfx_radio_carrier_11l.mp3",
        "label": "Clean carrier — bed under the VO line",
        "note": "Sits under 'a snow storm. Please stay indoors during these times.'",
        "seconds": 15,
        "loop": True,
        "prompt": (
            "Quiet clean analogue radio carrier from a small mantel radio speaker. Gentle "
            "tape-like hiss, faint broadcast room tone, a trace of mains hum. Very low "
            "level, calm and steady, the sound of an open station waiting between "
            "announcements. Constant throughout."
        ),
    },
}


def generate(name: str, cue: dict, key: str) -> bool:
    out_path = OUT_DIR / cue["file"]
    if out_path.exists():
        print(f"  {cue['label']}: already exists, skipping")
        return True

    payload = {
        "text": cue["prompt"] + COMMON,
        "duration_seconds": cue["seconds"],
        "prompt_influence": 0.7,
    }
    if cue.get("loop"):
        payload["loop"] = True

    request = urllib.request.Request(
        ENDPOINT,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "xi-api-key": key,
            "Content-Type": "application/json",
            "Accept": "audio/mpeg",
        },
        method="POST",
    )

    print(f"  {cue['label']}: generating {cue['seconds']}s ...", flush=True)
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            audio = response.read()
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", "replace")[:400]
        hint = {
            401: "key rejected — check ELEVENLABS_API_KEY",
            402: "out of credits",
            403: "your plan may not grant API access to sound generation",
            422: "request body rejected (duration limits, or 'loop' unsupported)",
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

    if "--list" in sys.argv:
        for name, cue in CUES.items():
            loop = "loop" if cue.get("loop") else "one-shot"
            print(f"{name:<18} {cue['label']}  [{cue['seconds']}s, {loop}]\n"
                  f"{'':<18} {cue['note']}")
        return 0

    unknown = [a for a in argv if a not in CUES]
    if unknown:
        print(f"Unknown cue(s): {', '.join(unknown)}", file=sys.stderr)
        print(f"Available: {', '.join(CUES)}", file=sys.stderr)
        return 2

    selected = argv or list(CUES)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    key = read_api_key()

    print(f"Generating {len(selected)} cue(s) into {OUT_DIR.relative_to(REPO)}/\n")
    failures = [name for name in selected if not generate(name, CUES[name], key)]

    print()
    if failures:
        print(f"{len(selected) - len(failures)}/{len(selected)} succeeded. "
              f"Failed: {', '.join(failures)}")
        return 1
    print(f"All {len(selected)} cues written.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
