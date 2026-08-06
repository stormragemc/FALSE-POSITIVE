"""Verify llm.MODEL is a live model id and llm.generate_reply actually
works, bypassing the fact that generate_reply swallows every exception into
FALLBACK_LINE (see llm.py's docstring — deliberate for gameplay, but it means
a dead model id currently looks identical to a working turn from Unity's
side). This script calls the same function but also prints the raw exception
if one occurs, so a real failure is visible instead of silently degrading.

Run from Sidecar/ with the project venv:
    C:\\fpsc_venv\\Scripts\\python.exe tools\\probe_llm.py
"""

import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import config
from google import genai
from google.genai import types

import llm


def main() -> None:
    if not config.GCP_PROJECT:
        print("FATAL: GCP_PROJECT not set in Sidecar/.env")
        sys.exit(1)

    print(f"Model under test: {llm.MODEL}")

    # First, call the raw client directly so a bad model id / auth error
    # surfaces instead of being swallowed by generate_reply's try/except.
    client = genai.Client(
        vertexai=True,
        project=config.GCP_PROJECT,
        location=config.GCP_LOCATION,
    )
    t0 = time.perf_counter()
    try:
        resp = client.models.generate_content(
            model=llm.MODEL,
            contents=[{"role": "user", "parts": [{"text": "Say the word OK and nothing else."}]}],
            # No thinking_config here on purpose: this call exists to prove the
            # model id and auth are good, and the argument's name differs across
            # model generations (`thinking_level` on 3.x, `thinking_budget` on
            # 2.x). Pinning it here would fail a healthy model on a 400.
            config=types.GenerateContentConfig(
                system_instruction="Reply with exactly one word.",
                max_output_tokens=64,
            ),
        )
        ms = int((time.perf_counter() - t0) * 1000)
        text = llm._spoken_text(resp)
        print(f"RAW CALL OK  ({ms} ms)  model responded: {text!r}")
    except Exception as e:
        print(f"RAW CALL FAILED: {type(e).__name__}: {e}")
        print(f"\n'{llm.MODEL}' does not appear usable through Vertex in this project/location.")
        sys.exit(1)

    # Now exercise the real code path (opening line + one witness turn).
    reply, ms = llm.generate_reply(history=[], transcript="", emotion="", confidence=0.0, is_opening=True)
    print(f"generate_reply(is_opening=True) -> {ms} ms: {reply!r}")
    if reply == llm.FALLBACK_LINE:
        print("WARNING: got FALLBACK_LINE from the opening call despite a working raw call above — check logs.")

    reply2, ms2 = llm.generate_reply(
        history=[{"role": "user", "content": llm.OPENING_KICKOFF_TEXT}, {"role": "assistant", "content": reply}],
        transcript="I didn't see anything, I was just walking by.",
        emotion="neutral",
        confidence=0.6,
        is_opening=False,
    )
    print(f"generate_reply(witness turn) -> {ms2} ms: {reply2!r}")


if __name__ == "__main__":
    main()
