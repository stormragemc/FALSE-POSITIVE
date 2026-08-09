# FALSE POSITIVE web fallback

Dependency-free browser version of the game. The Sidecar serves it at:

```text
http://127.0.0.1:8765/fallback/
```

Use the configured host/port when they differ. Serving the page from the Sidecar keeps Live AI
requests same-origin and does not expose `/turn`: the API still requires `X-FP-Client-Key`, which
the operator enters under **Setup** and the page holds in `sessionStorage` for that tab only. A
browser cannot keep a bearer key secret from someone with DevTools access, so enable Live AI only
on a supervised, trusted device. Use Offline scripted mode for an untrusted public deployment.

For an offline-only preview without the Python service:

```bash
python3 -m http.server 4173 --directory Sidecar/web
```

Then open `http://127.0.0.1:4173`. Microphone access requires HTTPS or localhost. A separately
served page will normally need an explicit backend CORS policy for Live AI; prefer the Sidecar's
`/fallback/` route for live play.

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
  to the existing `/turn` contract. The response transcript, affect-aware reply, timing, and PCM
  audio are handled without changing the backend schema.

Player recordings remain in memory for the current turn and are never stored by this website.
Speech transcripts are kept in `sessionStorage` so they survive reloads in the current tab but are
discarded when the tab session ends.
