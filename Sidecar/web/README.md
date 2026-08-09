# FALSE POSITIVE web fallback

Dependency-free browser version of the game. The Sidecar serves it at:

```text
http://127.0.0.1:8765/fallback/
```

Use the configured host/port when they differ. The Vercel deployment keeps Live AI same-origin
through `/api/turn`; the function owns the backend key and a signed, HTTP-only session cookie, so
players never receive `FP_CLIENT_KEY`, choose backend session ids, or submit AI scene instructions.

For an offline-only preview without the Python service:

```bash
python3 -m http.server 4173 --directory Sidecar/web
```

Then open `http://127.0.0.1:4173`. This static preview supports Offline scripted mode only because
it does not run the `/api` functions. Microphone access requires HTTPS or localhost.

## Deploy Live AI on Vercel

Set the Vercel project root to `Sidecar/web` and configure these server-only variables for
Production and Preview:

```text
SIDECAR_BASE_URL=https://your-sidecar.example
FP_CLIENT_KEY=the-key-accepted-by-your-sidecar
SESSION_SIGNING_SECRET=a-random-secret-with-at-least-32-characters
```

Generate `SESSION_SIGNING_SECRET` with `openssl rand -hex 32`. Never prefix any of these variables
with `NEXT_PUBLIC_` or place their values in browser code. New players start in Live AI mode; the
Offline scripted mode remains a recoverable fallback if the hosted backend is unavailable.

## Add gameplay videos

Every canonical cutscene variant has a titled, sandboxed iframe whose initial source is
`about:blank`. During a demo, paste an iframe-compatible HTTPS URL (or localhost HTTP URL) into
that cutscene's **Video URL** field and choose **Attach**. Sources persist in this browser's local
storage.

Examples include a YouTube `/embed/...` URL or a directly hosted MP4 URL that the browser allows
inside an iframe. The story never blocks on an empty slot.

To preconfigure footage in code, update the `cutsceneSources` value in local storage through the
same controls rather than adding a secret or environment-specific URL to the repository.

## Modes

- **Offline scripted** records the player's microphone, uses browser speech recognition when
  available, and plays the reviewed Maksim/Spassky ElevenLabs V3 recordings for fixed authored
  replies, scene openings, and endings. Every officer entry in the interview log includes
  **Replay voice** in case automatic playback is blocked or the player wants to hear the line
  again. Browser text-to-speech is used only if a generated recording cannot be played. The mode
  is labelled scripted throughout.
  The recording is not sent to the FALSE POSITIVE backend, but browser speech recognition may
  use the browser provider's own service; the interface says so at consent and beside each turn.
- **Live AI** converts the browser recording to raw little-endian PCM16 mono at 16 kHz and posts it
  to the same-origin Vercel gateway. The gateway validates the phase and audio, creates a signed
  session, supplies the canonical scene instruction and server-side key, and forwards the existing
  `/turn` response without changing its schema.

Player recordings remain in memory for the current turn and are never stored by this website.
Speech transcripts are kept in `sessionStorage` so they survive reloads in the current tab but are
discarded when the tab session ends.
