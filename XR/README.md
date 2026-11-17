# FALSE POSITIVE — WebXR

A seated, hands-first adaptation of the existing Unity mystery. The story uses the original character models, dialogue, sound effects and cinematic recipes. The Unity project remains available at the repository root.

Play: https://false-positive-xr.vercel.app

## Run and build

Use Node 24 and Git LFS. From the repository root:

```sh
git lfs install
git lfs pull --include="Assets/_Project/Art/NewCop.glb,Assets/_Project/Art/Characters/*.glb,Assets/_Project/Art/Audio/**,Sidecar/web/**" --exclude=""
cd XR
npm ci
npm run build
npm run dev:runtime
```

The build derives four smaller WebP/Draco cast models from the original GLBs, checks the skeleton and facial targets, and copies the recorded dialogue. Generated public models/media are ignored by Git. No ElevenLabs account is needed to build or play the recorded scenes.

`npm test` checks all four endings and actual Experience/cinematic coordination, including failed audio, repeated Skip, pause, reset, terminal live sessions and stale callbacks. `npm run typecheck` checks source syntax. CI runs the game and cinematic tests without paid services.

## Controls

Desktop: click the current task or objects. Write testimony into the single statement box. After enough testimony, Continue becomes available; further statements are optional.

VR: use Enter VR in a compatible headset browser. Stay seated, pinch or poke the spatial controls, and grab the photo or key. The spatial keyboard allows completion without speech. The desktop HTML controls are hidden during immersion; the spatial controls are hidden outside it.

For desktop XR emulation, open `?emulate=1`, then Enter VR. This is the official IWER Quest 3 hand runtime. It is an emulator, not physical-device validation. A developer-only scene review URL is `?qa=1&cinematic=TheSofa`; Take your seat plays that specific authored scene after a user gesture.

## Deployment

Vercel project root: `XR`. Build: `npm run build`. Output: `dist`. Node: 24. Enable Git LFS and source files outside the root directory, because the API imports the shared gateway in Sidecar/web/server.

Server-only environment variables:

- `SIDECAR_BASE_URL`: deployed Cloud Run service.
- `FP_CLIENT_KEY`: the **value** of the backend's `fp-client-key` secret. It must match the Cloud Run value.
- `SESSION_SIGNING_SECRET`: a separate random signing secret for browser sessions.

Never prefix these secrets with `VITE_` or commit them. Redeploy after changing environment variables. `/api/ready` performs a non-mutating authenticated route check; Live AI remains disabled when the backend rejects the key.

The Cloud Run backend uses Google Speech-to-Text V2 and, by default, Gemini 2.5 Flash TTS with its existing Vertex AI service account. Voice generation is optional: a failed provider preserves the completed reply as text. Recorded cutscene audio is independent of this live service. See Sidecar/README.md for deployment and provider configuration.

## Verification limits

The recorded runtime evidence in test/xr-runtime-evidence.json concerns an earlier source revision and is explicitly scoped. It proves immersive hand-session initialization and an initial spatial poke, plus a separate grab/carry/release probe. It does not prove a complete hands-only playthrough, Quest frame rate, microphone behavior or physical comfort. Those require a real headset test. Desktop end-to-end testing and state/cinematic integration tests are separate checks.
