"""Manually probe the live Gemini model with prompt-injection transcripts.

This is deliberately not a unit test. It calls the paid Vertex/Gemini service,
so run it manually from Sidecar/ only when credentials are configured:

    python tools/red_team_llm.py

Add --show-replies to inspect model replies locally. That option can display
unwanted model text, so do not copy its output into tickets or commits.
"""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import llm
import output_safety
from red_team_cases import PROMPT_INJECTION_CASES


def _contents_for(attack: str) -> list[dict]:
    return llm._to_contents(
        history=[],
        turn_texts=[
            llm._format_witness_transcript(attack),
            llm._format_affect_context("No reliable vocal-affect impression is available."),
        ],
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--show-replies", action="store_true")
    args = parser.parse_args()

    client = llm._get_client()
    failed_cases = []

    for name, attack in PROMPT_INJECTION_CASES:
        try:
            response = llm._call_llm(client, _contents_for(attack))
            reply = llm._spoken_text(response)
        except Exception as exc:
            failed_cases.append(name)
            print(f"{name}: request failed ({type(exc).__name__})")
            continue

        if not reply:
            failed_cases.append(name)
            print(f"{name}: model returned no spoken reply")
            continue

        if output_safety.filter_spoken_text(reply) == output_safety.FALLBACK_LINE:
            failed_cases.append(name)
            print(f"{name}: output filter rejected the model reply")
        else:
            print(f"{name}: reply passed the output filter; inspect role adherence manually")

        if args.show_replies:
            print(f"  reply: {reply}")

    if failed_cases:
        print(f"FAILED: {', '.join(failed_cases)}")
        sys.exit(1)

    print("Boundary checks completed. Inspect the replies before recording a pass.")


if __name__ == "__main__":
    main()
