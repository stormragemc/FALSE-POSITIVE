"""Generate Officer Spassky's production dialogue with the selected Maksim voice.

Lines are read straight out of `docs/HUMAN_SCRIPT.md` rather than copied here,
so the rendered audio cannot drift from the script. The authored registers map
to the validated ElevenLabs V3 mood vocabulary used by Spassky's live turns.
"""

from argparse import ArgumentParser
from dataclasses import dataclass
from pathlib import Path
import os
import re
import shutil
import subprocess
import sys
import tempfile
import wave

import numpy as np
from elevenlabs import VoiceSettings
from elevenlabs.client import ElevenLabs


OUTPUT_DIRECTORY = Path(__file__).resolve().parent
REPOSITORY_ROOT = OUTPUT_DIRECTORY.parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "docs" / "HUMAN_SCRIPT.md"

VOICE_ID = "6sXsAlJKKBf265ucBSRt"  # Maksim — "Raw, unpolished, deep", Russian
MODEL_ID = "eleven_v3"
OUTPUT_FORMAT = "pcm_24000"
SAMPLE_RATE = 24_000
PEAK_CEILING = 0.97
ACCENT_TAG = "strong Russian accent"
CLEANUP_FILTER = (
    "afftdn=nr=12:nf=-55:tn=1:gs=5,"
    "areverse,afade=t=in:st=0:d=0.020,areverse,"
    "apad=pad_dur=0.150"
)
ZERO_TAIL_FRAMES = round(SAMPLE_RATE * 0.150)

_LINE_RE = re.compile(r"^\*\*\[(SPASSKY-\d+)\][^*]*\*\*\s*(.+?)\s*$")
_SCENE_RE = re.compile(r"^##\s+Scene\s+(\d+)\b")
_DAVID_RE = re.compile(r"\bDavid\b", re.IGNORECASE)

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
    """One authored V3 performance register."""

    name: str
    mood: str | None
    gain_db: float

    def __post_init__(self) -> None:
        object.__setattr__(self, "gain_db", max(-6.0, min(6.0, self.gain_db)))

    def voice_settings(self) -> VoiceSettings:
        return VoiceSettings(
            stability=1.0,
            similarity_boost=1.0,
            style=0.0,
            use_speaker_boost=True,
        )

    def tagged(self, text: str) -> str:
        direction = ACCENT_TAG if self.mood is None else f"{ACCENT_TAG}, {self.mood}"
        return f"[{direction}] {text}"


FLAT = Delivery("FLAT", None, 0.0)
PRESS = Delivery("PRESS", "impatient", 1.0)
RAISED = Delivery("RAISED", "shouting", 2.5)
LOW = Delivery("LOW", "quietly menacing", -1.5)

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


def apply_pronunciation_aliases(text: str) -> str:
    """Enforce DAY-vid without changing the canonical script text."""
    return _DAVID_RE.sub(
        lambda match: "DAY-VID" if match.group(0).isupper() else "Day-vid",
        text,
    )


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


def write_pcm_wav(path: Path, pcm: bytes) -> None:
    """Write raw 24 kHz mono PCM to a WAV container."""
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def read_pcm_wav(path: Path) -> bytes:
    """Read one canonical production WAV, rejecting unexpected formats."""
    with wave.open(str(path), "rb") as source:
        actual = (
            source.getnchannels(),
            source.getframerate(),
            source.getsampwidth(),
            source.getcomptype(),
        )
        expected = (1, SAMPLE_RATE, 2, "NONE")
        if actual != expected:
            raise RuntimeError(
                f"{path.name} has format {actual}; expected {expected}."
            )
        pcm = source.readframes(source.getnframes())
    if not pcm:
        raise RuntimeError(f"{path.name} contains no audio frames.")
    return pcm


def has_exact_zero_tail(path: Path) -> bool:
    """Return whether a WAV already has the required exact digital-zero tail."""
    pcm = read_pcm_wav(path)
    tail_bytes = ZERO_TAIL_FRAMES * 2
    return len(pcm) >= tail_bytes and pcm[-tail_bytes:] == bytes(tail_bytes)


def write_clean_wav(path: Path, pcm: bytes) -> None:
    """Denoise, fade, pad, and atomically install one production WAV."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        raise RuntimeError("FFmpeg is required for production VO cleanup.")

    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        dir=path.parent,
        prefix=f".{path.stem}-",
    ) as temporary_directory:
        temporary_root = Path(temporary_directory)
        source_path = temporary_root / "source.wav"
        cleaned_path = temporary_root / path.name
        write_pcm_wav(source_path, pcm)

        subprocess.run(
            [
                ffmpeg,
                "-hide_banner",
                "-loglevel",
                "error",
                "-nostdin",
                "-y",
                "-i",
                str(source_path),
                "-af",
                CLEANUP_FILTER,
                "-ac",
                "1",
                "-ar",
                str(SAMPLE_RATE),
                "-c:a",
                "pcm_s16le",
                str(cleaned_path),
            ],
            check=True,
        )
        if not has_exact_zero_tail(cleaned_path):
            raise RuntimeError(
                f"FFmpeg did not produce a {ZERO_TAIL_FRAMES}-frame zero tail "
                f"for {path.name}."
            )
        os.replace(cleaned_path, path)


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
    parser.add_argument(
        "--clean-existing",
        action="store_true",
        help=(
            "Apply production cleanup to existing WAVs that do not already "
            "have the required exact zero tail; does not call the API."
        ),
    )
    args = parser.parse_args()

    if args.clean_existing and (args.force or args.dry_run):
        parser.error("--clean-existing cannot be combined with --force or --dry-run")

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

    if args.clean_existing:
        missing_files = []
        for line_id, _text, _phase, _delivery in lines:
            output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
            if not output_path.is_file():
                missing_files.append(output_path.name)
                continue
            if has_exact_zero_tail(output_path):
                print(f"SKIP CLEAN {output_path.name}", flush=True)
                continue
            write_clean_wav(output_path, read_pcm_wav(output_path))
            print(f"CLEANED {output_path.name}", flush=True)
        if missing_files:
            print(
                f"Missing production WAVs: {', '.join(missing_files)}",
                file=sys.stderr,
            )
            return 2
        return 0

    client = None
    for line_id, text, _phase, delivery in lines:
        output_path = OUTPUT_DIRECTORY / f"{line_id}.wav"
        if output_path.exists() and not args.force:
            print(f"SKIP {output_path.name}", flush=True)
            continue

        if client is None:
            api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
            if not api_key:
                print("ELEVENLABS_API_KEY is not set.", file=sys.stderr)
                return 2
            client = ElevenLabs(api_key=api_key)

        chunks = client.text_to_speech.convert(
            voice_id=VOICE_ID,
            text=delivery.tagged(apply_pronunciation_aliases(text)),
            model_id=MODEL_ID,
            output_format=OUTPUT_FORMAT,
            voice_settings=delivery.voice_settings(),
        )
        pcm = b"".join(chunks)
        if not pcm:
            raise RuntimeError(f"Empty audio response for {line_id}")
        write_clean_wav(output_path, apply_gain_db(pcm, delivery.gain_db))
        print(f"GENERATED {output_path.name}  [{delivery.name}]", flush=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
