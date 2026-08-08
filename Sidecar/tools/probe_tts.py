"""Probe which ElevenLabs voices this account can actually use over the API.

Listing voices (`client.voices.get_all()`) is NOT sufficient: on a free plan,
voices from the shared Voice Library appear in that list but still 402 on an
actual `text_to_speech.convert()` call ("Free users cannot use library voices
via the API."). The only reliable test is a real convert call per candidate
voice, which is what this script does.

Run from Sidecar/ with the project venv:
    C:\\fpsc_venv\\Scripts\\python.exe tools\\probe_tts.py

Reads GEMINI/ELEVENLABS credentials the normal way, via config.py -> .env.
Never prints the API key. Writes a short PCM sample per usable voice into
the scratchpad dir named on the command line (or cwd) so a human can
sanity-check the audio, but the exit summary is what matters for the plan
decision (Branch A vs Branch B).
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import config
import tts
from elevenlabs.client import ElevenLabs

# Deliberately reach into tts.py rather than restating the model and settings
# here. This script's whole job is to answer "will this voice work in
# production", and a second copy of those constants answers it for a
# configuration production does not use — which is exactly what happened when
# the officer moved to eleven_multilingual_v2 and this probe stayed on flash.
_MODEL_ID = tts._MODEL_ID
_VOICE_SETTINGS = tts._VOICE_SETTINGS

# Maksim — the cast officer voice. See
# Artifacts/voice_guide/Spassky.md §2.3.
_SPASSKY_VOICE_ID = "6sXsAlJKKBf265ucBSRt"

_TEST_TEXT = "Testing one two."


def main() -> None:
    if not config.ELEVENLABS_API_KEY:
        print("FATAL: ELEVENLABS_API_KEY not set in Sidecar/.env")
        sys.exit(1)

    client = ElevenLabs(api_key=config.ELEVENLABS_API_KEY)

    print("Fetching visible voices (NOTE: visibility != API usability)...")
    voices = client.voices.get_all().voices
    print(f"{len(voices)} voices visible in this account.\n")

    usable = []
    for v in voices:
        vid = v.voice_id
        name = getattr(v, "name", "?")
        category = getattr(v, "category", "?")
        labels = getattr(v, "labels", None) or {}
        gender = labels.get("gender", "?") if isinstance(labels, dict) else "?"
        try:
            chunks = client.text_to_speech.convert(
                voice_id=vid,
                text=_TEST_TEXT,
                model_id=_MODEL_ID,
                output_format="mp3_44100_128",
                voice_settings=_VOICE_SETTINGS,
            )
            raw = b"".join(chunks)
            if raw:
                usable.append((vid, name, category, gender))
                print(f"  OK    {vid}  {name!r:30s} category={category:10s} gender={gender}")
            else:
                print(f"  EMPTY {vid}  {name!r:30s} category={category:10s} gender={gender}")
        except Exception as e:
            msg = str(e)
            reason = "402 payment_required" if "402" in msg or "payment_required" in msg else msg[:80]
            print(f"  FAIL  {vid}  {name!r:30s} category={category:10s} gender={gender}  -> {reason}")

    print()
    if usable:
        print(f"Branch A: {len(usable)} usable voice(s) over the API.")
        # The officer (COP_PERSONA in llm.py) is Officer Spassky, cast as
        # Maksim: male, Russian-accented, semi-deep and raspy. Prefer him by ID
        # so this script agrees with config.py's committed default; fall back to
        # any male-labeled voice, then to the first usable one, since some
        # accounts' voices carry no gender label at all.
        cast = [u for u in usable if u[0] == _SPASSKY_VOICE_ID]
        male = [u for u in usable if u[3] == "male"]
        vid, name, category, gender = (cast or male or usable)[0]
        if cast:
            print("Recommended (the cast Spassky voice — already the default in config.py):")
        elif male:
            print("Recommended (Maksim is NOT usable on this account; nearest male voice):")
        else:
            print("Recommended (no male-labeled voice was usable — first usable one; pick by ear):")
        print(f"  ELEVENLABS_VOICE_ID={vid}   ({name}, {category}, gender={gender})")
        if cast:
            print("  Nothing to set — config.py already defaults to this. Only override to change voice.")
        if len(usable) > 1:
            print(f"All {len(usable)} usable voices are listed above (OK rows) — pick any by ear/label.")
    else:
        print("Branch B: no voice on this account works over the API (free-tier gate).")
        print("A local TTS fallback is needed until the account is upgraded.")


if __name__ == "__main__":
    main()
