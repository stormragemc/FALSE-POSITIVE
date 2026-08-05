"""Feed a real PCM16/16kHz utterance through Google Cloud Speech-to-Text and the
HuBERT emotion classifier (SER) directly, proving both halves of the
"HuBERT detects the player's voice" pipeline independently of the HTTP
/turn endpoint (whose TTS stage currently 402s until .env is updated,
which would otherwise mask a working STT/SER stage as a total failure).

Usage:
    C:\\fpsc_venv\\Scripts\\python.exe tools\\probe_stt_ser.py <path_to.pcm>

<path_to.pcm> must be raw PCM16 LE mono at 16kHz (no header) — see
Sidecar/README.md's "Testing it in isolation" section for how to make one.
"""

import asyncio
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

if len(sys.argv) < 2:
    print("Usage: probe_stt_ser.py <path_to_16k_mono_pcm16.pcm>")
    sys.exit(1)

pcm_path = Path(sys.argv[1])
raw_bytes = pcm_path.read_bytes()
print(f"Loaded {len(raw_bytes)} bytes from {pcm_path}")

import audio_utils
import features_classical
import ser
import stt

audio_f32 = audio_utils.pcm16_bytes_to_float32(raw_bytes)
print(f"{len(audio_f32)} samples ({len(audio_f32) / 16000:.2f}s @ 16kHz)")

print("\nCalling Google Cloud Speech-to-Text...")
t0 = time.perf_counter()
transcript, stt_ms = asyncio.run(stt.transcribe(raw_bytes))
print(f"STT  ({stt_ms} ms, {int((time.perf_counter() - t0) * 1000)} ms incl. load): {transcript!r}")

print(f"\nLoading SER ({ser.config.HUBERT_MODEL_ID})...")
t0 = time.perf_counter()
observation = ser.analyze(audio_f32)
features = features_classical.extract(audio_f32)
print(
    f"SER  ({observation.elapsed_ms} ms, "
    f"{int((time.perf_counter() - t0) * 1000)} ms incl. load): "
    f"{observation.label!r} (confidence {observation.confidence:.2f})"
)
print(f"     probabilities={observation.probabilities}")
print(
    f"     entropy={observation.normalized_entropy:.3f} "
    f"margin={observation.top_two_margin:.3f} "
    f"hidden_instability={observation.frame_instability:.3f}"
)
print(
    f"     speech_ratio={features.speech_ratio:.2f} pauses={features.long_pause_count} "
    f"pitch_var={features.pitch_variability:.3f} energy_var={features.energy_variability:.3f} "
    f"flags={list(features.flags)}"
)

if transcript.strip():
    print("\nOK: STT produced a non-empty transcript from real speech audio.")
else:
    print("\nWARNING: STT returned empty transcript — check the input audio.")
