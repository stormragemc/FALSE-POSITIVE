"""Local test bench for the hosted interrogation backend.

Serves a browser page that records from the mic and drives a real turn against
Cloud Run, so the affect channel can be exercised before any Unity wiring.

Why a proxy instead of calling the backend straight from the page:

1. The backend has no CORS middleware, and its auth middleware rejects the
   preflight OPTIONS with 401 before CORS could ever apply. A browser on a
   different origin cannot talk to it.
2. Adding CORSMiddleware means editing Sidecar/app.py, which stream A owns
   exclusively.
3. Proxying keeps the client key on this machine. The browser never sees it.

Run:
    python3 testbench/server.py

The client key is read from FP_CLIENT_KEY if set, otherwise pulled from Secret
Manager via gcloud. It is never logged.
"""

import os
import subprocess
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

BACKEND = os.environ.get(
    "FP_BACKEND_URL",
    "https://false-positive-backend-465469192069.us-central1.run.app",
).rstrip("/")
SECRET_NAME = os.environ.get("FP_CLIENT_KEY_SECRET", "fp-client-key")
PORT = int(os.environ.get("TESTBENCH_PORT", "8000"))
HERE = Path(__file__).resolve().parent

# Matches auth.CLIENT_KEY_HEADER.
CLIENT_KEY_HEADER = "x-fp-client-key"
# The backend caps a turn at 50s; allow a little more before giving up.
UPSTREAM_TIMEOUT = 70


def load_client_key() -> str:
    """Read the client key from the environment, else from Secret Manager."""
    from_env = os.environ.get("FP_CLIENT_KEY", "").strip()
    if from_env:
        print("[testbench] client key: from FP_CLIENT_KEY")
        return from_env

    print(f"[testbench] client key: reading secret {SECRET_NAME} via gcloud")
    try:
        out = subprocess.run(
            ["gcloud", "secrets", "versions", "access", "latest", f"--secret={SECRET_NAME}"],
            capture_output=True,
            text=True,
            check=True,
        )
    except FileNotFoundError:
        sys.exit("[testbench] FATAL: gcloud not found. Set FP_CLIENT_KEY instead.")
    except subprocess.CalledProcessError as e:
        sys.exit(f"[testbench] FATAL: could not read secret {SECRET_NAME}:\n{e.stderr.strip()}")

    key = out.stdout.strip()
    if not key:
        sys.exit(f"[testbench] FATAL: secret {SECRET_NAME} is empty.")
    return key


CLIENT_KEY = load_client_key()


class Handler(BaseHTTPRequestHandler):
    # Routes proxied upstream, mapped to their backend path.
    PROXY_ROUTES = {"/api/turn": "/turn", "/api/session/reset": "/session/reset"}

    def log_message(self, fmt, *args):
        sys.stderr.write("[testbench] %s\n" % (fmt % args))

    def _send(self, status, body: bytes, content_type: str):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = self.path.split("?", 1)[0]
        if path in ("/", "/index.html"):
            page = HERE / "index.html"
            if not page.exists():
                self._send(500, b"index.html missing", "text/plain; charset=utf-8")
                return
            self._send(200, page.read_bytes(), "text/html; charset=utf-8")
            return
        if path == "/api/health":
            self._proxy_get("/health")
            return
        self._send(404, b"not found", "text/plain; charset=utf-8")

    def do_POST(self):
        upstream_path = self.PROXY_ROUTES.get(self.path.split("?", 1)[0])
        if upstream_path is None:
            self._send(404, b"not found", "text/plain; charset=utf-8")
            return

        length = int(self.headers.get("Content-Length") or 0)
        body = self.rfile.read(length) if length else b""
        req = urllib.request.Request(
            BACKEND + upstream_path,
            data=body,
            method="POST",
            headers={
                "Content-Type": self.headers.get("Content-Type", "application/octet-stream"),
                CLIENT_KEY_HEADER: CLIENT_KEY,
            },
        )
        self._forward(req)

    def _proxy_get(self, upstream_path: str):
        req = urllib.request.Request(
            BACKEND + upstream_path,
            method="GET",
            headers={CLIENT_KEY_HEADER: CLIENT_KEY},
        )
        self._forward(req)

    def _forward(self, req: urllib.request.Request):
        """Relay upstream verbatim. HTTP errors carry a JSON body worth showing."""
        try:
            with urllib.request.urlopen(req, timeout=UPSTREAM_TIMEOUT) as resp:
                self._send(
                    resp.status,
                    resp.read(),
                    resp.headers.get("Content-Type", "application/json"),
                )
        except urllib.error.HTTPError as e:
            self._send(e.code, e.read(), e.headers.get("Content-Type", "application/json"))
        except Exception as e:
            msg = f'{{"ok":false,"error":"proxy: {type(e).__name__}: {e}"}}'
            self._send(502, msg.encode("utf-8"), "application/json")


if __name__ == "__main__":
    print(f"[testbench] backend  {BACKEND}")
    print(f"[testbench] serving  http://localhost:{PORT}")
    print("[testbench] the browser never receives the client key")
    try:
        ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
    except KeyboardInterrupt:
        print("\n[testbench] stopped")
