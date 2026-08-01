"""The officer's dialogue: Gemini 3.6 Flash via the Google Gemini API.

Every reply is spoken aloud verbatim by TTS with nobody reading it first, which
drives three decisions below: thinking is pinned to `minimal` (a full reasoning
pass roughly doubles turn latency for no gain on two-sentence dialogue), any
`thought` parts the model does return are filtered out so only spoken dialogue
can reach TTS, and brevity is enforced by the prompt rather than by a token cap
(truncating mid-sentence would be read aloud as-is).

Default safety thresholds are relaxed to BLOCK_ONLY_HIGH: this is an
interrogation about a crime, and the default filters treat an accusatory
detective as borderline often enough to matter. Anything still blocked degrades
to FALLBACK_LINE rather than failing the turn.
"""

import time

from google import genai
from google.genai import types

import config

_client: genai.Client | None = None

MODEL = "gemini-3.6-flash"

# Change this to retarget the game's premise — it's the only game-specific
# piece of this file.
CRIME_PREMISE = (
    "a break-in and theft at Halden's Convenience Store on the night of the 14th; "
    "roughly $2,000 in cash and a locked display case of watches were taken"
)

COP_PERSONA = f"""You are Detective Mara Voss, conducting an interrogation about {CRIME_PREMISE}. \
The person across the table is a witness who was near the scene that night, and you are not yet \
sure they are only a witness.

Reply with one to three spoken sentences. Never narrate actions, never use stage directions, never \
use markdown or formatting of any kind — every word you write will be spoken aloud verbatim by a \
text-to-speech engine, so write exactly what the detective says and nothing else.

Stay in character at all times. Be terse, watchful, and a little impatient — this is not a friendly \
conversation. Push on inconsistencies. Ask one clear question at a time; do not stack multiple \
questions in a single reply.

You will sometimes be told the vocal emotion detected in the witness's last reply, along with a \
confidence score. Treat this only as a soft impression of their tone, not a fact — the detector is \
frequently wrong. Let it inform your delivery and pacing subtly. Never mention the reading directly \
or say anything like "you sound nervous" as if quoting a machine, and never let a low-confidence \
reading change your questioning outright."""

OPENING_KICKOFF_TEXT = (
    "[SCENE START] The witness has just sat down across from you. Begin the interrogation with "
    "your opening question about what they saw that night. Do not greet them by name — you don't "
    "know it yet."
)

FALLBACK_LINE = "Let's come back to that."

SILENT_WITNESS_TEXT = "[The witness says nothing.]"

_SAFETY_SETTINGS = [
    types.SafetySetting(category=category, threshold="BLOCK_ONLY_HIGH")
    for category in (
        "HARM_CATEGORY_HARASSMENT",
        "HARM_CATEGORY_HATE_SPEECH",
        "HARM_CATEGORY_SEXUALLY_EXPLICIT",
        "HARM_CATEGORY_DANGEROUS_CONTENT",
    )
]


def _get_client() -> genai.Client:
    global _client
    if _client is None:
        _client = genai.Client(api_key=config.GEMINI_API_KEY)
    return _client


def _to_contents(history: list[dict], turn_texts: list[str]) -> list[dict]:
    """Map the sidecar's `{role: user|assistant, content: str}` history onto
    Gemini's `{role: user|model, parts: [...]}` shape.

    Empty strings are dropped rather than sent: STT returns "" for a silent
    recording, and an empty part is a 400 — which, once in history, would
    otherwise poison every later turn of the session too.
    """
    contents: list[dict] = []
    for msg in history:
        text = (msg.get("content") or "").strip()
        if not text:
            continue
        role = "model" if msg.get("role") == "assistant" else "user"
        contents.append({"role": role, "parts": [{"text": text}]})

    parts = [{"text": t.strip()} for t in turn_texts if t and t.strip()]
    if parts:
        contents.append({"role": "user", "parts": parts})
    return contents


def _spoken_text(resp) -> str:
    """Only real dialogue — thought parts must never reach TTS."""
    if not resp.candidates:
        return ""
    content = resp.candidates[0].content
    if not content or not content.parts:
        return ""
    return " ".join(
        part.text
        for part in content.parts
        if part.text and not getattr(part, "thought", False)
    ).strip()


def _call_llm(client: genai.Client, contents: list[dict]):
    return client.models.generate_content(
        model=MODEL,
        contents=contents,
        config=types.GenerateContentConfig(
            system_instruction=COP_PERSONA,
            thinking_config=types.ThinkingConfig(thinking_level="minimal"),
            max_output_tokens=1024,
            safety_settings=_SAFETY_SETTINGS,
        ),
    )


def generate_reply(
    history: list[dict],
    transcript: str,
    emotion: str,
    confidence: float,
    is_opening: bool,
) -> tuple[str, int]:
    """Returns (reply_text, elapsed_ms). Never raises — a game must not
    hard-fail mid-conversation, so any error degrades to FALLBACK_LINE."""
    client = _get_client()
    t0 = time.perf_counter()

    if is_opening:
        turn_texts = [OPENING_KICKOFF_TEXT]
    else:
        # Gemini has no per-message system role, so the emotion reading rides
        # along as a second part of the witness's turn.
        turn_texts = [
            transcript.strip() or SILENT_WITNESS_TEXT,
            f"Vocal emotion detected in that reply: {emotion} (confidence {confidence:.2f}). "
            "Treat this as a soft impression, not a fact.",
        ]

    reply_text = FALLBACK_LINE
    try:
        resp = _call_llm(client, _to_contents(history, turn_texts))

        block_reason = getattr(resp.prompt_feedback, "block_reason", None) if resp.prompt_feedback else None
        if block_reason:
            print(f"[Sidecar] LLM blocked the prompt ({block_reason}); using fallback line.")
        else:
            text = _spoken_text(resp)
            if text:
                reply_text = text
            else:
                finish = resp.candidates[0].finish_reason if resp.candidates else None
                print(f"[Sidecar] LLM returned no text content (finish_reason={finish}); using fallback line.")
    except Exception as e:
        print(f"[Sidecar] LLM call failed: {e}")

    ms = int((time.perf_counter() - t0) * 1000)
    return reply_text, ms
