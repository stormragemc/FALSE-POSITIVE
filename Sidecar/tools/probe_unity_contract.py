"""Exercises the live Cloud Run backend the way the Unity client does.

Reads the client key from Assets/StreamingAssets/backend.local.json so it is
never printed or pasted into a terminal — paste the key into that file first
(see Assets/StreamingAssets/backend.local.example.json for the shape), then
run this from anywhere:

    python Sidecar/tools/probe_unity_contract.py

Exercises, against the real deployed service:
  1. GET  /health           -> expects 200, models_loaded: true
  2. POST /session/reset    -> with the key: {"ok": true}; without it: 401
  3. POST /turn (no audio)  -> the scripted opening line
  4. POST /turn (~2s tone)  -> a real transcript + prosody + timings
  5. POST /turn (long scene_instruction) -> tells us whether the deployed
     backend has the Game-branch scene_instruction feature at all. A backend
     built from the merged code rejects an over-length scene_instruction with
     400; a backend that predates that feature just ignores the unknown form
     field and returns 200. Neither response is a failure of this script —
     it's a report on what's live.

Exit code is 0 only if steps 1-4 all behave as expected; the scene_instruction
probe never affects the exit code since either outcome is informative, not a
failure.
"""

import json
import math
import struct
import sys
import urllib.error
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
OVERRIDE_PATH = REPO_ROOT / "Assets" / "StreamingAssets" / "backend.local.json"
SAMPLE_RATE = 16000


def load_override():
    if not OVERRIDE_PATH.exists():
        print(f"ERROR: {OVERRIDE_PATH} does not exist.")
        print("Copy backend.local.example.json to backend.local.json and paste in the client key.")
        sys.exit(2)
    data = json.loads(OVERRIDE_PATH.read_text(encoding="utf-8"))
    base_url = (data.get("backendBaseUrl") or "").rstrip("/")
    client_key = data.get("backendClientKey") or ""
    if not base_url:
        print(f"ERROR: backendBaseUrl is empty in {OVERRIDE_PATH}.")
        sys.exit(2)
    if not client_key:
        print(f"ERROR: backendClientKey is empty in {OVERRIDE_PATH}. Paste in the key and re-run.")
        sys.exit(2)
    return base_url, client_key


def request(method, url, key=None, form=None, timeout=60):
    """Minimal multipart/form-data + auth-header client using only stdlib."""
    headers = {}
    body = None
    if form is not None:
        boundary = "----fp-probe-boundary----"
        parts = []
        for name, value in form.items():
            if isinstance(value, tuple):
                filename, content, content_type = value
                parts.append(
                    f"--{boundary}\r\n"
                    f'Content-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'
                    f"Content-Type: {content_type}\r\n\r\n".encode("utf-8")
                    + content
                    + b"\r\n"
                )
            else:
                parts.append(
                    (
                        f"--{boundary}\r\n"
                        f'Content-Disposition: form-data; name="{name}"\r\n\r\n'
                        f"{value}\r\n"
                    ).encode("utf-8")
                )
        parts.append(f"--{boundary}--\r\n".encode("utf-8"))
        body = b"".join(parts)
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"

    if key:
        headers["x-fp-client-key"] = key

    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8")
        try:
            return e.code, json.loads(raw)
        except json.JSONDecodeError:
            return e.code, {"_raw": raw}
    except urllib.error.URLError as e:
        return None, {"_error": str(e)}


def make_tone_pcm16(seconds=2.0, freq_hz=220.0, sample_rate=SAMPLE_RATE):
    n = int(seconds * sample_rate)
    samples = [int(8000 * math.sin(2 * math.pi * freq_hz * i / sample_rate)) for i in range(n)]
    return struct.pack(f"<{n}h", *samples)


def main():
    base_url, key = load_override()
    ok = True

    print(f"Target: {base_url}")
    print()

    # 1. /health
    print("1. GET /health")
    status, body = request("GET", f"{base_url}/health")
    print(f"   -> {status} {body}")
    if status != 200 or not body.get("models_loaded"):
        print("   FAIL: expected 200 with models_loaded: true")
        ok = False
    print()

    # 2a. /session/reset without the key -> 401
    print("2a. POST /session/reset WITHOUT key (expect 401)")
    status, body = request("POST", f"{base_url}/session/reset", key=None, form={"session_id": "probe-noauth"})
    print(f"   -> {status} {body}")
    if status != 401:
        print("   FAIL: expected 401 unauthorized")
        ok = False
    print()

    # 2b. /session/reset with the key -> {"ok": true}
    print("2b. POST /session/reset WITH key (expect ok: true)")
    status, body = request("POST", f"{base_url}/session/reset", key=key, form={"session_id": "probe-session"})
    print(f"   -> {status} {body}")
    if status != 200 or not body.get("ok"):
        print("   FAIL: expected 200 {ok: true} — check the client key matches the deployed FP_CLIENT_KEY")
        ok = False
    print()

    # 3. Opening turn (no audio)
    print("3. POST /turn with no audio (expect the scripted opening line)")
    status, body = request(
        "POST", f"{base_url}/turn", key=key,
        form={"session_id": "probe-session", "sample_rate": SAMPLE_RATE, "onset_delay_ms": 0},
    )
    summary = {k: body.get(k) for k in ("ok", "reply_text", "audio_sample_rate", "audio_channels")}
    prosody_reason = (body.get("prosody") or {}).get("reliability_reason")
    print(f"   -> {status} {summary} prosody.reliability_reason={prosody_reason}")
    if status != 200 or not body.get("ok") or not body.get("reply_text"):
        print("   FAIL: expected 200 ok:true with a non-empty reply_text")
        ok = False
    print()

    # 4. Turn with real audio
    print("4. POST /turn with ~2s of generated audio")
    pcm = make_tone_pcm16()
    status, body = request(
        "POST", f"{base_url}/turn", key=key,
        form={
            "session_id": "probe-session",
            "sample_rate": SAMPLE_RATE,
            "onset_delay_ms": 0,
            "audio": ("utterance.pcm", pcm, "application/octet-stream"),
        },
    )
    timings = {k: body.get(k) for k in ("stt_ms", "ser_ms", "llm_ms", "tts_ms", "total_ms")}
    print(f"   -> {status} ok={body.get('ok')} transcript={body.get('transcript')!r} timings={timings}")
    if status != 200 or not body.get("ok"):
        print("   FAIL: expected 200 ok:true")
        ok = False
    print()

    # 5. scene_instruction support probe (informational only)
    print("5. POST /turn with an over-length scene_instruction (informational, not a pass/fail)")
    status, body = request(
        "POST", f"{base_url}/turn", key=key,
        form={
            "session_id": "probe-scene-instruction",
            "sample_rate": SAMPLE_RATE,
            "onset_delay_ms": 0,
            "scene_instruction": "x" * 6001,
        },
    )
    print(f"   -> {status} {body if status != 200 else {'ok': body.get('ok')}}")
    if status == 400 and "scene_instruction" in json.dumps(body):
        print("   The deployed backend HAS the scene_instruction feature (rejected as expected).")
    elif status == 200:
        print("   The deployed backend does NOT recognise scene_instruction (silently ignored) —")
        print("   a redeploy of the merged Sidecar/ is needed for per-phase officer briefings to work.")
    else:
        print(f"   Unexpected response — inspect manually.")
    print()

    print("=" * 60)
    print("PASS" if ok else "FAIL — see above")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
