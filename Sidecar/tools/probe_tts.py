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
from elevenlabs.client import ElevenLabs

_MODEL_ID = "eleven_flash_v2_5"
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
        # The officer character (COP_PERSONA in llm.py) is Detective Mara
        # Voss, a woman — pick a female-labeled voice by default instead of
        # just usable[0], so this script's own suggestion doesn't clash with
        # the persona. Falls back to the first usable voice if no gender
        # label is present (some accounts' voices simply don't have one).
        female = [u for u in usable if u[3] == "female"]
        vid, name, category, gender = female[0] if female else usable[0]
        print(f"Recommended (matches the female Mara Voss persona in llm.py):")
        print(f"  ELEVENLABS_VOICE_ID={vid}   ({name}, {category}, gender={gender})")
        if not female:
            print("  (no female-labeled voice was usable — this is just the first usable one; pick by ear.)")
        if len(usable) > 1:
            print(f"All {len(usable)} usable voices are listed above (OK rows) — pick any by ear/label.")
    else:
        print("Branch B: no voice on this account works over the API (free-tier gate).")
        print("A local TTS fallback is needed until the account is upgraded.")


if __name__ == "__main__":
    main()
