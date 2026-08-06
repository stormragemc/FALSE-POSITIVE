# FALSE POSITIVE — Day 2 plan (8 Aug)

**Written:** 6 Aug 2026, after the Day-1 error fixes below landed. **Freeze:** 8 Aug 23:59.
**Supersedes** `GAME_COMPLETION_PLAN.md` §5's Day-2 table where the two disagree — this file
reflects what is actually built, not what was estimated three days ago.

---

## 1. Where Day 1 actually left off

`GAME_COMPLETION_PLAN.md`'s Day-1 exit criterion — *"MainMenu → consent → calibration → P1 →
M1 (radio, door, yell) → P2 → M2 → P3 → placeholder ending"* — is met and verified (offline, F2
phase-walk, backend down; see the commit `Day 1: fix boot/menu/props/cutscenes/ending`). Beyond
that baseline, five things that were still broken going into today are now fixed:

| # | Was | Now |
|---|---|---|
| 1 | Game only playable from `_Persistent.unity`; MainMenu alone was dead (no EventSystem) | `PersistentSceneBootstrap` loads `_Persistent` from any scene; `MainMenuController` also carries a same-scene EventSystem backstop |
| 2 | Menu camera: perspective, Skybox clear, grey placeholder cube | Orthographic, Solid Color, UI-only culling mask, no 3D geometry at all |
| 3 | All 12 story props were coloured cubes with floating labels | Real low-poly models (Blender-generated FBX, `Assets/_Project/Art/Props/`), correctly oriented, URP/Lit materials, colliders sized from actual mesh bounds |
| 4 | 11 of 22 cutscenes were empty fades or silent black holds | Every recipe has content — dialogue, VO, or a diegetic SFX beat (Ivy's flashback is deliberately silent, per the story script) |
| 5 | `P4_Ending` always played `EndingDavid`; `GamePhase.Outcome` did nothing — the game never actually ended | Day-1 stopgap: ending picked from a naive keyword scan of P3 transcripts; `OutcomeScreen` shows the closing card with a working "Return to menu" button |

One real bug was also found and fixed during verification, independent of the above:
`SpeechPrompt.Pulse()` called `StartCoroutine` on its own (sometimes-inactive) GameObject —
`PhaseDialogueController`'s 15-second no-speech nudge could fire after the prompt was already
answered and hidden, throwing. Both the symptom (guard in `SpeechPrompt`) and the cause (stop the
nudge coroutine the moment the prompt is satisfied, not just on phase exit) are fixed.

**Also landed after this file was first written (6 Aug, later that day):** the game is now
playable end to end with **no backend running at all**. Pressing **Play** with the sidecar down
still dead-ends at P2 exactly as described above — that gap is unchanged. What's new is a
second main-menu button, **Offline demo**, which runs the identical consent → calibration → P1
→ … → outcome flow but replaces P2/P3's live officer with a fixed 14-line authored script
(`Assets/_Project/Config/OfflineDialogueScript.asset`, built by
`Editor/OfflineScriptBuilder.cs`, real ElevenLabs VO in `Art/Audio/VO/spassky_offline_*`),
played by `DialogueManager.PlayOfflineTurn` instead of `InterrogationSidecarClient.PostTurn`.
It's honestly labelled on screen throughout ("OFFLINE — scripted interrogation") and always
lands on the David ending, since there's no transcript to detect a named suspect from. **A13
below is now specifically the online-with-no-backend case** — Offline demo covers the
"no backend at all" case, A13 covers "backend expected but unreachable mid-session."

**Not done, and explicitly Day-2 scope per the sections below:** the full `EndingSelector` /
`SessionScore`-driven ending rule (`STORY_SCRIPT.md` §8), verbatim-quote `OutcomeScreen`, real
`SuspectNameDetector`, `OutputGuard`, fault UX beyond the mic-calibration failure card, backend
integration/`X-FP-Client-Key`, settings persistence, pause menu, and B8–B15's remaining cutscene
polish/audio/lighting pass.

---

## 2. Track A — systems, voice, ending

| ID | Task | Depends on | Cheap-form fallback if cut |
|---|---|---|---|
| A9 | **`SuspectNameDetector`** replacing `PhaseDialogueController.DetectNamedSuspect`'s naive scan — reject "maybe Aaron or Ivy", handle possessives ("Aaron's fault") and mid-sentence mentions, single unambiguous name only. File: new `Scripts/Dialogue/SuspectNameDetector.cs`, called from `PhaseDialogueController.OnTurnCompleted`. | — | Keep the Day-1 keyword scan; note it in the deck as a known simplification. |
| A10 | **`EndingSelector`** implementing `STORY_SCRIPT.md` §8's real rule: `credibility`, `composure`, `accusation` + cited-clue count, not just "was a name said." `SessionScore.RecordTurn` already accumulates `Tension`/`Arousal`/`SignalConfidence` per turn (`Flow/SessionScore.cs`) — this task turns that into the three tracked quantities and replaces `PhaseDialogueController.EnterP4Placeholder`'s switch statement. | A9, existing `SessionScore` | Day-1 stopgap ships as-is, labelled honestly. |
| A11 | **`OutcomeScreen` verbatim quotes** — 2–3 player lines with turn numbers, pulled from `SessionScore`'s recorded `TurnRecord`s (`Transcript` field already stored). Extend `UI/OutcomeScreen.cs` (`Show(string)` → `Show(EndingResult)`) rather than replacing it. **Re-run the G6 grep** (`rg -i "lie\|lying\|deception\|truth ?meter\|honest" Assets/_Project`) after — it passed clean today, must stay that way. | A10 | Fixed card stays (already shipped, not a placeholder — just less specific). |
| A12 | **`OutputGuard`** — client-side second layer between the LLM reply and TTS/subtitles: length cap, strip markdown/stage directions, block persona-leak phrases. **ROADMAP S1, never-cut.** New `Scripts/Dialogue/OutputGuard.cs`, called from `DialogueManager` before `CopVoicePlayback`/`SubtitleUI` see a reply. Nothing currently implements this — it is a real gap, not a nice-to-have. | — | None — this is on the never-cut list. |
| A13 | **Fault UX** beyond what exists: F1 empty transcript → officer re-asks, turn not consumed; F5 mic lost mid-session → blocking in-fiction card with Retry; F6 backend unreachable at a *live turn* (not just at boot, which `BackendHealthProbe` already surfaces) → menu-level "Interrogation service offline" card with the exact recovery command. Today's `MicCalibration` failure path (device dropdown + Retry) already demonstrates the correct pattern to copy. | — | Today's backend-down behaviour (a logged error, no crash) stays as the honest floor. |
| A14 | **Backend integration** — swap the client-side keyword fallbacks (marks, name detection) for the real fields *if* the backend team's B1–B4 (`GAME_COMPLETION_PLAN.md` §7) landed; wire `X-FP-Client-Key` + `backendBaseUrl` on `InterrogationConfig`. Keep both paths working — this is additive, not a replacement. | Backend team's schedule | Client fallbacks (already built and verified today) keep working regardless. |
| A15 | **Settings persistence, pause menu (Esc), quit-to-menu, full session reset.** `SettingsStore` (PlayerPrefs) and `SettingsPanel` already exist and are wired into `_Persistent`; this task is mostly exposing `SettingsPanel.Show()` from an Esc keybind during any live phase, plus wiring `POST /session/reset` (already implemented in `InterrogationSidecarClient.ResetSession`, called today by `StartNewPlaythrough`) to a pause-menu "Restart" button. | A2 (done) | `OutcomeScreen`'s "Return to menu" (shipped today) is the only reset path; acceptable but worse UX. |

---

## 3. Track B — remaining cutscene content, audio, lighting

Today's cutscene pass filled every *empty* recipe but did not add camera work, blocking/staging
beyond the existing fade-to-black form, or full audio mixing. That is genuinely Day-2 scope —
`GAME_COMPLETION_PLAN.md` §10 sanctions the cheap form as shippable, so nothing here is a bug fix,
it is content depth.

| ID | Task | Depends on | Cheap-form fallback if cut |
|---|---|---|---|
| B8 | Priya screams / body reveal — a real camera move (wake on sofa, pan to the window) instead of the current static fade+VO. Ivy and Aaron coming down the stairs — actually show them walking, using the existing cast prefabs (`CabinNightCharacterBuilder`'s output, renamed Nico→Nick per T04). | `CutsceneDirector`'s existing beat system (no Timeline needed — coroutine-driven camera lerp is enough) | Ships as today's fade+VO. Already acceptable, not broken. |
| B9 | Out into the snow — door opens, four characters visibly run out into the exterior snow terrain that already exists (`Memory_CabinMorning`, duplicated from `NobodyWentOut_CabinNight`). | B8 | Fade + VO over black (current state). |
| B10 | **The carry** — the expensive one. Backward-walking camera at carry height, sway curve, Nick's body model (today's `Prop_NickBody.fbx`) in frame, all 7 VO lines already attached and verified today. Budget a half day; this is the single most valuable remaining cutscene. | B9 | Fade + VO, camera static (current state) — keeps all 7 lines, which are the actual content. |
| B11 | The sofa / Priya dials — lay Nick down on the actual sofa geometry, phone dial-tone SFX (today's `body_settle_thud.mp3` covers the lay-down; dial tone is a new short SFX). | B10 | Static + VO (current state). |
| B12 | Accusation flashbacks — today's cheap form (bolt-click / silence / glass-clink over black) is honest and shippable per `STORY_SCRIPT.md`'s "heavily degraded, no dialogue." Upgrading further (actual degraded imagery) is optional polish, not a gap. | — | Ships as-is. |
| B13 | The four endings — observation-glass framing, one camera setup reused four times with a different character behind the glass. All four VO lines are attached and verified today; this task is the camera/staging only. | B1 (done) | Ships as today's fade+VO+card. |
| B14 | Audio pass: fire, wind, storm, door, glass, radio static, room tone per scene, mixed at sane relative levels (today's SFX are individually correct but never level-balanced against each other). Pull ambience from `IL3DN/Audio` first. | B3/B6 (done) | Individual SFX already play; just unmixed. |
| B15 | Lighting + post pass so M1 (night) and M2 (morning) read as unmistakably different times — today's verification screenshot showed a warm firelit interior at night; morning's lighting swap needs a pass to confirm it reads as daylight, not just "less red." | — | Existing lighting is functional, not cinematic. |

---

## 4. Sync points — 8 Aug afternoon

Unchanged from `GAME_COMPLETION_PLAN.md` §5, restated with what's already true:

| ID | Owner | Task | Status |
|---|---|---|---|
| Z1 | ⊕ | Full playthrough on a machine that is not the developer's, twice, timed. | Not started — today's verification was offline/scripted (F2 phase-walk), not a real playthrough. **This still needs to happen for real**, including with the backend up. |
| Z2 | A | Standalone Windows build; confirm mic works in the **build**, not just the Editor. | Not started. |
| Z3 | A | `docs/PRIVACY.md` — already exists per the README's citations; re-check it still matches A14's backend wiring once that lands. | Exists, verify currency. |
| Z4 | B | README third-party table — today's pass added the `ElevenLabs Sound Generation` row for the new SFX; the o3n/IL3DN/TDG/Materials Collection rows from `ASSETS_TODO.md` still need the same disclosure treatment in the main README table (currently only in `ASSETS_TODO.md`). | Partially done. |
| Z5 | ⊕ | Demo video ≤5 min, per `docs/DEMO_SCRIPT.md` (write it first — the P2:14 + P3:8 turn caps at ~3s/turn plus exploration overruns 5 minutes by default). | Not started. |
| Z6 | ⊕ | Deck — 6 required sections, ≤15 slides. | Not started. |
| Z7 | ⊕ | **Freeze 23:59. Tag it.** | — |

---

## 5. Priority order if the day runs short

Highest value first, per the cut ladder in `GAME_COMPLETION_PLAN.md` §10 (never cut: consent
card, output guard, prompts-as-files, `PRIVACY.md`, the seven story marks, the ten-clue ledger):

1. **A12 `OutputGuard`** — never-cut, currently the single biggest real gap.
2. **A10 `EndingSelector`** — the actual thesis mechanic; today's stopgap is honest but shallow.
3. **B10 The carry** — highest per-minute narrative value of the remaining cutscene work.
4. **Z1/Z2** — a real playthrough and a real build are the only way to catch what today's offline
   verification structurally cannot (the live voice loop, actual mic hardware, real latency).
5. **A9 `SuspectNameDetector`** — today's naive scan works but is easy to fool; low effort to fix properly.
6. Everything else in §2/§3, in the order listed, degrading gracefully per §10's cut ladder — no
   cut here leaves a hole the player can fall through, only content depth on top of an already
   walkable game.

**Never fake:** if B8–B13 ship in today's cheap form, say so in the deck and demo video exactly as
`GAME_COMPLETION_PLAN.md` §10 requires. The Day-1 ending stopgap (§1, row 5) must be described
accurately until A10 replaces it — do not claim the full credibility/fabrication rule is live
before it is.
