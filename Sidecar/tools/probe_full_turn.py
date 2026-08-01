"""Exercise the real llm.py -> tts.py -> audio_utils.py code path end to end
with a specific voice override, without touching Sidecar/.env (that file is
never read or written by tooling here). Proves the production code for
Branch A works before asking the user to paste the chosen voice ID into
their own .env.

Run from Sidecar/ with the project venv:
    C:\\fpsc_venv\\Scripts\\python.exe tools\\probe_full_turn.py <VOICE_ID>

Writes a WAV of the synthesized reply to Sidecar/tools/_probe_out.wav so a
human can listen to confirm it's audible speech, not silence/noise.
"""

import sys
import wave
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import config

if len(sys.argv) < 2:
    print("Usage: probe_full_turn.py <ELEVENLABS_VOICE_ID>")
    sys.exit(1)

config.ELEVENLABS_VOICE_ID = sys.argv[1]  # in-process override only

import audio_utils
import llm
import tts


def main() -> None:
    reply_text, llm_ms = llm.generate_reply(
        history=[], transcript="", emotion="", confidence=0.0, is_opening=True
    )
    print(f"LLM  ({llm_ms} ms): {reply_text!r}")

    pcm, rate, channels, tts_ms = tts.synthesize(reply_text)
    print(f"TTS  ({tts_ms} ms): {len(pcm)} bytes @ {rate} Hz, {channels}ch")

    norm_pcm, norm_rate = audio_utils.normalize_to_canonical(pcm, rate, channels)
    print(f"Normalized: {len(norm_pcm)} bytes @ {norm_rate} Hz mono")

    out_path = Path(__file__).resolve().parent / "_probe_out.wav"
    with wave.open(str(out_path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(norm_rate)
        w.writeframes(norm_pcm)
    print(f"Wrote {out_path}")

    if len(norm_pcm) < 1000:
        print("WARNING: suspiciously small audio payload.")
    else:
        print("OK: full LLM -> TTS -> normalize chain produced audible-sized PCM.")


if __name__ == "__main__":
    main()
