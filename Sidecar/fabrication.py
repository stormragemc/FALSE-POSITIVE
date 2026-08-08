"""Detection of details the witness could not have observed (STORY_SCRIPT §7).

The officer's reply and this judgement are deliberately two separate model
calls. The reply is spoken aloud and is wrapped in the S1/S2 output filters;
this module's output is a small array of ids that never reaches TTS, the
officer's context, or session history. Keeping them apart means a malformed or
blocked judgement can never cost the officer his voice, and that the trap
catalog can never enter `llm.COP_PERSONA` — where it would join
`_PERSONA_NGRAMS` and start muting real dialogue that happens to quote a bait
line back.

Worth stating plainly: because nothing here is spoken or fed back to the model
that speaks, the worst outcome of a successful prompt injection in a transcript
is that the player loses credibility they should have kept.

The prompt itself lives in `prompts/unsupported_detail_judge.txt` rather than in
this file (G3 — prompts are a graded deliverable and belong in `prompts/`).
Note its vocabulary: it says "unsupported detail", never "fabrication", because
`llm._UNSAFE_SPOKEN_REPLY` blocks `fabricat*` and `invent*` in spoken output and
prompt wording has a habit of migrating between files. The wire field the client
reads is still `fabrications`, per docs/TASK_S7_TRAPS.md §4.
"""

import json
import time
from pathlib import Path

from google.genai import types

import config
import llm

# The ids docs/STORY_SCRIPT.md §7 enumerates, mirrored in the Unity client by
# Assets/_Project/Scripts/Flow/TrapIds.cs. A rename on one side alone is caught
# by tests/test_unity_contract.py, because the client silently drops ids it does
# not recognise and nothing would ever be caught again.
TRAP_IDS = (
    "trap_door",
    "trap_time",
    "trap_answer",
    "trap_outside",
    "trap_lock",
    "trap_window",
)

MODEL = "gemini-2.5-flash"

# The reply is prose and needs room; this is an array of at most six short
# enum strings, so it needs almost none. Small caps also keep the judge well
# inside the reply's latency, which is what makes it free (see app.py).
MAX_OUTPUT_TOKENS = 128

_PROMPT_PATH = Path(__file__).resolve().parent / "prompts" / "unsupported_detail_judge.txt"

# Marks the start of the witness-knowledge briefing inside scene_instruction.
# Written by PhaseDialogueController.BuildSceneInstruction on the client; matched
# loosely (the client joins it with an em dash) and with a whole-text fallback,
# because a briefing that fails to match should degrade to "send everything",
# never to "send nothing" — the block is the judge's only armed/unarmed oracle.
_KNOWLEDGE_HEADER = "WITNESS KNOWLEDGE"

_RESPONSE_SCHEMA = {
    "type": "OBJECT",
    "properties": {
        "unsupported_details": {
            "type": "ARRAY",
            "items": {"type": "STRING", "enum": list(TRAP_IDS)},
        }
    },
    "required": ["unsupported_details"],
}

_prompt_cache: str | None = None
_prompt_failed = False


def _prompt() -> str:
    """Read the judge prompt once. A missing file disables detection for the
    process rather than failing turns — it is logged loudly and every call
    thereafter returns no ids."""
    global _prompt_cache, _prompt_failed
    if _prompt_cache is not None:
        return _prompt_cache
    if _prompt_failed:
        return ""
    try:
        _prompt_cache = _PROMPT_PATH.read_text(encoding="utf-8").strip()
    except Exception as e:
        _prompt_failed = True
        print(f"[Sidecar] unsupported-detail prompt unavailable ({e}); detection is off.")
        return ""
    return _prompt_cache


def extract_knowledge(scene_instruction: str) -> str:
    """Pull the witness-knowledge briefing out of the phase scene instruction.

    The rest of that string is stage direction for the officer ("Open this phase
    with exactly this question...") and would read to the judge as instructions
    addressed to it. Only the knowledge block says what the witness was in a
    position to observe, which is the one thing the judge needs.
    """
    text = (scene_instruction or "").strip()
    if not text:
        return ""
    index = text.find(_KNOWLEDGE_HEADER)
    return text[index:].strip() if index >= 0 else text


def build_contents(
    scene_instruction: str, officer_question: str, transcript: str
) -> list[dict]:
    """Assemble the judge's single user turn.

    The transcript is wrapped by `llm._format_witness_transcript` rather than by
    a local copy: that function HTML-escapes tag characters and neutralises the
    reserved affect-marker phrase, and having exactly one implementation of the
    witness-quoting rule is worth reaching across the module boundary for.
    """
    parts: list[str] = []

    knowledge = extract_knowledge(scene_instruction)
    if knowledge:
        parts.append(f"<WITNESS_KNOWLEDGE>\n{knowledge}\n</WITNESS_KNOWLEDGE>")

    question = " ".join((officer_question or "").split())
    if question:
        parts.append(f"OFFICER_QUESTION: {question}")

    parts.append(llm._format_witness_transcript(transcript))
    return [{"role": "user", "parts": [{"text": text} for text in parts]}]


def parse_ids(raw: str) -> list[str]:
    """Turn model output into a clean id list, trusting none of its shape.

    The response schema already constrains this, but the schema is enforced by
    the vendor and this runs on the far side of a network call, so every layer
    is re-checked here: any failure yields no ids at all.
    """
    try:
        payload = json.loads(raw)
    except Exception:
        return []
    if not isinstance(payload, dict):
        return []
    items = payload.get("unsupported_details")
    if not isinstance(items, list):
        return []

    seen: list[str] = []
    for item in items:
        if isinstance(item, str) and item in TRAP_IDS and item not in seen:
            seen.append(item)
    return seen


def _call_judge(client, contents: list[dict]):
    return client.models.generate_content(
        model=MODEL,
        contents=contents,
        config=types.GenerateContentConfig(
            system_instruction=_prompt(),
            thinking_config=types.ThinkingConfig(thinking_budget=0),
            max_output_tokens=MAX_OUTPUT_TOKENS,
            # This is a classification, not a performance: the same answer every
            # time is the desired behaviour.
            temperature=0.0,
            response_mime_type="application/json",
            response_schema=_RESPONSE_SCHEMA,
            # Same relaxed thresholds as the officer — the subject matter is a
            # death in the snow, and the defaults treat that as borderline often
            # enough to matter.
            safety_settings=llm._SAFETY_SETTINGS,
        ),
    )


def _response_text(resp) -> str:
    if not resp.candidates:
        return ""
    content = resp.candidates[0].content
    if not content or not content.parts:
        return ""
    return "".join(part.text for part in content.parts if part.text).strip()


def judge(
    scene_instruction: str, officer_question: str, transcript: str
) -> tuple[list[str], int]:
    """Returns (trap_ids, elapsed_ms). Never raises.

    Every failure path — disabled by config, no prompt file, no transcript, a
    vendor error, a blocked prompt, unparseable output — returns no ids. The
    game must be able to treat "nothing was caught" and "the judge did not run"
    identically, because a witness who is never caught is a valid playthrough.
    """
    t0 = time.perf_counter()

    def elapsed() -> int:
        return int((time.perf_counter() - t0) * 1000)

    if not getattr(config, "TRAP_JUDGE_ENABLED", True):
        return [], 0
    if not (transcript or "").strip():
        return [], 0
    if not _prompt():
        return [], elapsed()

    ids: list[str] = []
    try:
        contents = build_contents(scene_instruction, officer_question, transcript)
        resp = _call_judge(llm.get_client(), contents)

        block_reason = (
            getattr(resp.prompt_feedback, "block_reason", None)
            if resp.prompt_feedback
            else None
        )
        if block_reason:
            print(f"[Sidecar] unsupported-detail judge blocked ({block_reason}); no ids.")
        else:
            ids = parse_ids(_response_text(resp))
    except Exception as e:
        print(f"[Sidecar] unsupported-detail judge failed: {e}")

    return ids, elapsed()
