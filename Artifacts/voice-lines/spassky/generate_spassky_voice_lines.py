"""Generate Officer Spassky's production dialogue with the selected Maksim voice.

Lines are read straight out of `docs/HUMAN_SCRIPT.md` rather than copied here, so
the rendered audio cannot drift from the script. Delivery follows the register
table in `Artifacts/voice_guide/Spassky.md` §4.3, which is also what the sidecar
applies to Spassky's live turns — pre-rendered and live lines therefore match.
"""

from argparse import ArgumentParser
from dataclasses import dataclass
from pathlib import Path
import os
import re
import sys
import wave

import numpy as np
from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
REPOSITORY_ROOT = OUTPUT_DIRECTORY.parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "docs" / "HUMAN_SCRIPT.md"

VOICE_ID = "6sXsAlJKKBf265ucBSRt"  # Maksim — "Raw, unpolished, deep", Russian
MODEL_ID = "eleven_multilingual_v2"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
PEAK_CEILING = 0.97

_LINE_RE = re.compile(r"^\*\*\[(SPASSKY-\d+)\][^*]*\*\*\s*(.+?)\s*$")
_SCENE_RE = re.compile(r"^##\s+Scene\s+(\d+)\b")

# Which interrogation phase each script scene belongs to. Phase selects the LOW
# register for the verdict and the endings; everything else is text-driven.
_SCENE_PHASES = {
    1: "P1_TUTORIAL",
    3: "P2_RECALL",
    5: "P3_VERDICT",
    6: "P4_ENDING",
}


@dataclass(frozen=True)
class Delivery:
    """One delivery register. Fields are clamped to the API's accepted ranges."""

    name: str
    stability: float
    similarity_boost: float
    style: float
    speed: float
    gain_db: float

    def __post_init__(self) -> None:
        clamp = lambda value, low, high: max(low, min(high, value))
        object.__setattr__(self, "stability", clamp(self.stability, 0.0, 1.0))
        object.__setattr__(self, "similarity_boost", clamp(self.similarity_boost, 0.0, 1.0))
        object.__setattr__(self, "style", clamp(self.style, 0.0, 1.0))
        object.__setattr__(self, "speed", clamp(self.speed, 0.7, 1.2))
        object.__setattr__(self, "gain_db", clamp(self.gain_db, -6.0, 6.0))

    def voice_settings(self) -> VoiceSettings:
        return VoiceSettings(
            stability=self.stability,
            similarity_boost=self.similarity_boost,
            style=self.style,
            speed=self.speed,
        )


FLAT = Delivery("FLAT", 0.28, 1.00, 0.62, 0.92, 0.0)
PRESS = Delivery("PRESS", 0.20, 1.00, 0.78, 0.90, 1.0)
RAISED = Delivery("RAISED", 0.15, 1.00, 0.92, 0.95, 2.5)
LOW = Delivery("LOW", 0.15, 1.00, 0.85, 0.85, -1.5)

_LOW_PHASES = frozenset({"P3_VERDICT", "P4_ENDING"})


def choose(text: str, phase: str | None) -> Delivery:
    """Pick a register for one line. First match wins, in this order."""
    words = text.split()
    shouts = "!" in text or any(
        token.isupper() and sum(character.isalpha() for character in token) >= 2
        for token in words
    )
    if shouts:
        return RAISED
    if phase in _LOW_PHASES or ("?" not in text and len(words) >= 18):
        return LOW
    if text.endswith("?") and len(words) <= 5:
        return PRESS
    return FLAT


def read_lines() -> list[tuple[str, str, str | None, Delivery]]:
    """Parse every Spassky line out of the human script, in story order."""
    parsed: list[tuple[str, str, str | None, Delivery]] = []
    seen: set[str] = set()
    phase: str | None = None

    for raw in SCRIPT_PATH.read_text(encoding="utf-8").splitlines():
        scene = _SCENE_RE.match(raw)
        if scene:
            phase = _SCENE_PHASES.get(int(scene.group(1)))
            continue
        match = _LINE_RE.match(raw)
        if not match:
            continue
        line_id, text = match.group(1), match.group(2)
        if line_id in seen:
            raise RuntimeError(
                f"{line_id} appears more than once in {SCRIPT_PATH.name}; "
                "IDs are filenames and must be unique."
            )
        seen.add(line_id)
        parsed.append((line_id, text, phase, choose(text, phase)))

    if not parsed:
        raise RuntimeError(f"No Spassky lines found in {SCRIPT_PATH}")
    return parsed


def apply_gain_db(pcm: bytes, gain_db: float) -> bytes:
    """Apply a peak-limited trim so a loud line loses boost rather than clipping."""
    if gain_db == 0.0:
        return pcm

    samples = np.frombuffer(pcm, dtype=np.int16).astype(np.float32) / 32768.0
    samples *= 10.0 ** (gain_db / 20.0)
    peak = float(np.max(np.abs(samples))) if samples.size else 0.0
    if peak > PEAK_CEILING:
        samples *= PEAK_CEILING / peak
    return np.round(samples * 32767.0).astype(np.int16).tobytes()


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
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the register each line resolves to without calling the API.",
    )
    parser.add_argument(
        "--only",
        metavar="ID",
        action="append",
        help="Render only this line ID. Repeatable.",
    )
    args = parser.parse_args()

    lines = read_lines()
    if args.only:
        wanted = {value.upper() for value in args.only}
        lines = [entry for entry in lines if entry[0] in wanted]
        missing = wanted - {entry[0] for entry in lines}
        if missing:
            print(f"Unknown line IDs: {', '.join(sorted(missing))}", file=sys.stderr)
            return 2

    if args.dry_run:
        for line_id, text, phase, delivery in lines:
            print(f"{line_id}  {delivery.name:<6} {phase or '-':<12} {text}")
        print(f"\n{len(lines)} lines", flush=True)
        return 0

    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
        return 2

    client = ElevenLabs(api_key=api_key)
    for line_id, text, _phase, delivery in lines:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.force:
            print(f"SKIP {output_path.name}", flush=True)
            continue

        chunks = client.text_to_speech.convert(
            voice_id=VOICE_ID,
            text=text,
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=delivery.voice_settings(),
        )
        pcm = b"".join(chunks)
        if not pcm:
            raise RuntimeError(f"Empty audio response for {line_id}")
        write_wav(output_path, apply_gain_db(pcm, delivery.gain_db))
        print(f"GENERATED {output_path.name}  [{delivery.name}]", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
