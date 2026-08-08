"""Measure where a /turn actually spends its wall clock, end to end.

The debug overlay's `total_ms` is the server's own stopwatch (app.py starts it
after the request body has arrived and stops it before the response is
serialized). Everything outside that — uploading the utterance, downloading a
few hundred KB of base64 audio — is invisible to it, and on a link with real
RTT that outside portion is a third of the turn. This script measures both
halves against a live service so a change can be argued from numbers.

Usage
-----
    python3 tools/measure_turn_latency.py --runs 3
    python3 tools/measure_turn_latency.py --url http://127.0.0.1:8080 --runs 1
    python3 tools/measure_turn_latency.py --json before.json
    python3 tools/measure_turn_latency.py --compare before.json

Auth: reads FP_CLIENT_KEY from the environment, else falls back to
`gcloud secrets versions access latest --secret=fp-client-key`. The key is
never printed.

Audio: pass --wav to use a real recording. With no --wav, macOS generates one
with `say` + `afconvert`; elsewhere the script asks for a file rather than
inventing a tone, because a tone transcribes to nothing and a turn with an
empty transcript skips the fabrication judge and shortens the LLM prompt —
it would measure a pipeline the player never runs.

Stdlib only, so it runs with any python3 without touching the venv.
"""

import argparse
import http.client
import json
import os
import shutil
import statistics
import subprocess
import sys
import tempfile
import time
import urllib.parse
import uuid
import wave
from pathlib import Path

DEFAULT_URL = "https://false-positive-backend-465469192069.us-central1.run.app"

# Long enough to be a realistic answer rather than a "yes", short enough to stay
# well under SIDECAR_MAX_AUDIO_SECONDS. Deliberately in-fiction so the officer's
# reply is a normal-length line and TTS is measured on representative text.
SPOKEN_LINE = (
    "Look, I already told you. I was at the cabin the whole evening. "
    "I did not hear anything unusual, and I did not see anyone come or go "
    "before midnight."
)


def resolve_key() -> str:
    key = os.environ.get("FP_CLIENT_KEY", "").strip()
    if key:
        return key
    try:
        out = subprocess.run(
            ["gcloud", "secrets", "versions", "access", "latest", "--secret=fp-client-key"],
            capture_output=True,
            text=True,
            timeout=30,
            check=True,
        )
        return out.stdout.strip()
    except (OSError, subprocess.SubprocessError):
        sys.exit(
            "No client key. Set FP_CLIENT_KEY, or authenticate gcloud against the "
            "project so the secret can be read."
        )


def generate_utterance(destination: Path) -> Path:
    """macOS-only convenience path. Anywhere else, --wav is required."""
    if sys.platform != "darwin":
        sys.exit(
            "No --wav given and automatic generation only works on macOS "
            "(needs `say` and `afconvert`). Record a few seconds of speech, save "
            "it as 16 kHz mono WAV, and pass it with --wav."
        )
    if not (shutil.which("say") and shutil.which("afconvert")):
        sys.exit("`say` or `afconvert` is missing; pass a recording with --wav.")

    aiff = destination.with_suffix(".aiff")
    subprocess.run(["say", "-v", "Daniel", "-o", str(aiff), SPOKEN_LINE], check=True)
    subprocess.run(
        ["afconvert", "-f", "WAVE", "-d", "LEI16@16000", "-c", "1", str(aiff), str(destination)],
        check=True,
    )
    aiff.unlink(missing_ok=True)
    return destination


def read_pcm16_mono_16k(wav_path: Path) -> bytes:
    """The backend wants headerless PCM, and it wants it at the rate it was told.

    Refusing a mismatched file rather than resampling here is deliberate: a
    silent resample in the measuring instrument would hide exactly the kind of
    rate bug (48 kHz samples labelled 16 kHz) that stt.py's own comments warn
    about.
    """
    with wave.open(str(wav_path), "rb") as w:
        if (w.getnchannels(), w.getsampwidth(), w.getframerate()) != (1, 2, 16000):
            sys.exit(
                f"{wav_path} is {w.getnchannels()}ch/{w.getsampwidth() * 8}-bit/"
                f"{w.getframerate()}Hz. Needs mono 16-bit 16000Hz. Convert it with:\n"
                f"  afconvert -f WAVE -d LEI16@16000 -c 1 in.wav out.wav"
            )
        return w.readframes(w.getnframes())


def build_multipart(fields: dict[str, str], audio: bytes) -> tuple[bytes, str]:
    boundary = uuid.uuid4().hex
    parts: list[bytes] = []
    for name, value in fields.items():
        parts.append(
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n"
            f"{value}\r\n".encode()
        )
    if audio:
        parts.append(
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"audio\"; "
            f"filename=\"utterance.pcm\"\r\nContent-Type: application/octet-stream\r\n\r\n".encode()
        )
        parts.append(audio)
        parts.append(b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


def post_turn(url: str, key: str, session_id: str, audio: bytes, accept_gzip: bool) -> dict:
    """One turn, timed. Returns the parsed body plus observed transport timings.

    Timings come from http.client rather than curl so this stays stdlib-only.
    getresponse() returns once the response *headers* land, which is the
    server's last word before it starts streaming the body — so it is a true
    time-to-first-byte, and everything after it is download.
    """
    parsed = urllib.parse.urlparse(url)
    body, content_type = build_multipart(
        {"session_id": session_id, "sample_rate": "16000", "onset_delay_ms": "0"},
        audio,
    )
    headers = {
        "Content-Type": content_type,
        "Content-Length": str(len(body)),
        "x-fp-client-key": key,
    }
    # Opt in explicitly rather than relying on a default: whether the response
    # is compressed is one of the things being measured.
    if accept_gzip:
        headers["Accept-Encoding"] = "gzip"

    connection_class = (
        http.client.HTTPSConnection if parsed.scheme == "https" else http.client.HTTPConnection
    )
    conn = connection_class(parsed.netloc, timeout=180)

    t_start = time.perf_counter()
    conn.connect()
    t_connected = time.perf_counter()
    conn.request("POST", parsed.path + "/turn", body=body, headers=headers)
    response = conn.getresponse()
    t_first_byte = time.perf_counter()
    raw = response.read()
    t_done = time.perf_counter()
    encoding = (response.getheader("Content-Encoding") or "identity").lower()
    conn.close()

    if response.status != 200:
        sys.exit(f"HTTP {response.status} from /turn: {raw[:400]!r}")

    payload = raw
    if "gzip" in encoding:
        import gzip

        payload = gzip.decompress(raw)

    parsed_body = json.loads(payload)
    return {
        "body": parsed_body,
        "connect_ms": (t_connected - t_start) * 1000,
        "ttfb_ms": (t_first_byte - t_connected) * 1000,
        "download_ms": (t_done - t_first_byte) * 1000,
        "observed_total_ms": (t_done - t_connected) * 1000,
        "wire_bytes_down": len(raw),
        "decoded_bytes_down": len(payload),
        "content_encoding": encoding,
        "bytes_up": len(body),
    }


def reset_session(url: str, key: str, session_id: str) -> None:
    """Leave no state behind — each run gets a virgin session, and the prosody
    baseline (first 3 turns) must not be half-built by a measurement."""
    parsed = urllib.parse.urlparse(url)
    body, content_type = build_multipart({"session_id": session_id}, b"")
    connection_class = (
        http.client.HTTPSConnection if parsed.scheme == "https" else http.client.HTTPConnection
    )
    conn = connection_class(parsed.netloc, timeout=30)
    try:
        conn.request(
            "POST",
            parsed.path + "/session/reset",
            body=body,
            headers={
                "Content-Type": content_type,
                "Content-Length": str(len(body)),
                "x-fp-client-key": key,
            },
        )
        conn.getresponse().read()
    finally:
        conn.close()


def summarize(run: dict, audio_seconds: float) -> dict:
    body = run["body"]
    server_total = body.get("total_ms", 0)
    audio_b64 = body.get("audio_b64", "")
    reply_rate = body.get("audio_sample_rate", 0) or 1
    raw_audio_bytes = int(len(audio_b64) * 0.75)

    # ttfb covers upload + all server work, so what remains is the upload.
    upload_ms = max(0.0, run["ttfb_ms"] - server_total)

    return {
        "utterance_seconds": round(audio_seconds, 2),
        "bytes_up": run["bytes_up"],
        "upload_ms": round(upload_ms),
        "stt_ms": body.get("stt_ms", 0),
        "ser_ms": body.get("ser_ms", 0),
        "llm_ms": body.get("llm_ms", 0),
        "tts_ms": body.get("tts_ms", 0),
        "server_total_ms": server_total,
        "download_ms": round(run["download_ms"]),
        "wire_bytes_down": run["wire_bytes_down"],
        "decoded_bytes_down": run["decoded_bytes_down"],
        "content_encoding": run["content_encoding"],
        "base64_padding_bytes": len(audio_b64) - raw_audio_bytes,
        "reply_audio_seconds": round(raw_audio_bytes / 2 / reply_rate, 2),
        "reply_sample_rate": body.get("audio_sample_rate", 0),
        "observed_total_ms": round(run["observed_total_ms"]),
        "transcript": body.get("transcript", ""),
        "reply_text": body.get("reply_text", ""),
    }


def kb(n: float) -> str:
    return f"{n / 1024:.0f} KB"


def report(rows: list[dict], vad_wait_ms: float) -> dict:
    def med(field: str) -> float:
        return statistics.median(r[field] for r in rows)

    first = rows[0]
    parallel_stage = med("ser_ms") if med("ser_ms") >= med("stt_ms") else med("stt_ms")
    critical = "ser" if med("ser_ms") >= med("stt_ms") else "stt"

    print()
    print(f"  utterance      {first['utterance_seconds']}s, {kb(first['bytes_up'])} uploaded")
    print(f"  reply audio    {first['reply_audio_seconds']}s @ {first['reply_sample_rate']} Hz")
    print(f"  transcript     {first['transcript'][:70]}")
    print(f"  reply          {first['reply_text'][:70]}")
    print()
    print(f"  {'stage':<22}{'median ms':>10}   notes")
    print(f"  {'-' * 22}{'-' * 10}   {'-' * 34}")
    print(f"  {'VAD silence wait':<22}{vad_wait_ms:>10.0f}   client-side, before the POST")
    print(f"  {'upload':<22}{med('upload_ms'):>10.0f}   {kb(first['bytes_up'])}")
    print(f"  {'STT || HuBERT':<22}{parallel_stage:>10.0f}   parallel; critical path is {critical}"
          f" (stt {med('stt_ms'):.0f} / ser {med('ser_ms'):.0f})")
    print(f"  {'Gemini':<22}{med('llm_ms'):>10.0f}")
    print(f"  {'ElevenLabs':<22}{med('tts_ms'):>10.0f}")
    print(f"  {'download':<22}{med('download_ms'):>10.0f}   {kb(first['wire_bytes_down'])} on the wire"
          + (f" ({first['content_encoding']}, {kb(first['decoded_bytes_down'])} decoded)"
             if first["content_encoding"] != "identity" else ""))
    print(f"  {'-' * 22}{'-' * 10}")
    print(f"  {'server total_ms':<22}{med('server_total_ms'):>10.0f}   what the F1 overlay shows")
    print(f"  {'HTTP round trip':<22}{med('observed_total_ms'):>10.0f}")
    print(f"  {'player-perceived':<22}{med('observed_total_ms') + vad_wait_ms:>10.0f}   "
          f"stop speaking -> first audio byte")
    print()
    print(f"  base64 padding on the wire: {kb(first['base64_padding_bytes'])} "
          f"({first['base64_padding_bytes'] / max(1, first['wire_bytes_down']) * 100:.0f}% of download)")
    print()

    return {
        "runs": rows,
        "median": {
            "upload_ms": med("upload_ms"),
            "stt_ms": med("stt_ms"),
            "ser_ms": med("ser_ms"),
            "llm_ms": med("llm_ms"),
            "tts_ms": med("tts_ms"),
            "download_ms": med("download_ms"),
            "server_total_ms": med("server_total_ms"),
            "observed_total_ms": med("observed_total_ms"),
            "perceived_total_ms": med("observed_total_ms") + vad_wait_ms,
        },
    }


def compare(before_path: Path, after: dict) -> None:
    before = json.loads(before_path.read_text())["median"]
    now = after["median"]
    print(f"  {'stage':<22}{'before':>9}{'after':>9}{'delta':>9}")
    print(f"  {'-' * 22}{'-' * 9}{'-' * 9}{'-' * 9}")
    for field, label in [
        ("upload_ms", "upload"),
        ("stt_ms", "STT"),
        ("ser_ms", "HuBERT"),
        ("llm_ms", "Gemini"),
        ("tts_ms", "ElevenLabs"),
        ("download_ms", "download"),
        ("server_total_ms", "server total"),
        ("observed_total_ms", "HTTP round trip"),
        ("perceived_total_ms", "player-perceived"),
    ]:
        b, a = before.get(field, 0), now.get(field, 0)
        print(f"  {label:<22}{b:>9.0f}{a:>9.0f}{a - b:>+9.0f}")
    print()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default=DEFAULT_URL)
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--wav", type=Path, help="16 kHz mono 16-bit WAV to send")
    parser.add_argument("--json", type=Path, help="write results here for later --compare")
    parser.add_argument("--compare", type=Path, help="print a delta against an earlier --json")
    parser.add_argument("--gzip", action="store_true", help="send Accept-Encoding: gzip")
    parser.add_argument(
        "--vad-wait-ms",
        type=float,
        default=700.0,
        help="InterrogationConfig.vadSilenceTimeoutSeconds, for the perceived total",
    )
    args = parser.parse_args()

    key = resolve_key()
    url = args.url.rstrip("/")

    with tempfile.TemporaryDirectory() as tmp:
        wav = args.wav or generate_utterance(Path(tmp) / "utterance.wav")
        audio = read_pcm16_mono_16k(wav)
        audio_seconds = len(audio) / 2 / 16000

        print(f"==> {url}  ({args.runs} run(s), {audio_seconds:.1f}s utterance"
              + (", Accept-Encoding: gzip" if args.gzip else "") + ")")

        rows = []
        for index in range(args.runs):
            session_id = f"latency-probe-{uuid.uuid4().hex[:8]}"
            try:
                run = post_turn(url, key, session_id, audio, args.gzip)
                rows.append(summarize(run, audio_seconds))
                print(f"    run {index + 1}: {rows[-1]['observed_total_ms']} ms round trip")
            finally:
                # Each probe turn otherwise occupies a session slot and counts
                # against MAX_TURNS_PER_SESSION for an hour (SESSION_IDLE_TTL).
                reset_session(url, key, session_id)

    results = report(rows, args.vad_wait_ms)

    if args.json:
        args.json.write_text(json.dumps(results, indent=2))
        print(f"  wrote {args.json}")
    if args.compare:
        compare(args.compare, results)


if __name__ == "__main__":
    main()
