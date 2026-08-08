# FALSE POSITIVE — story script

**Status:** canon. Supersedes `idea/bong.md` in full — see `idea/superseded/bong.md` for the
prior version and why it was replaced. Written 6 Aug 2026 as part of
[`GAME_COMPLETION_PLAN.md`](GAME_COMPLETION_PLAN.md).

This file is the single source of truth for what the game says and shows: ground truth, cast,
scene/phase map, the full beat script, the cutscene list, the story marks the AI detective must
cover, the traps, ending selection, and the clue ledger. Anyone writing dialogue, building a
cutscene, or dressing a scene works from this document.

---

## 1. Ground truth — what actually happened

Five friends at a rented cabin. The road closes at 23:00; the plough does not reach them until
morning. **Nobody arrived after, nobody left before.** Five cups on the table is the visual proof.

| Time | Event |
|---|---|
| ~21:00 | Heavy drinking. **David and Nick swap coats as a joke** — David ends up in Nick's heavy parka, Nick in a thin jacket. Nobody thinks of it again. |
| ~23:10 | The radio warns of the storm: *"…a snow storm, please stay indoors during these times."* Everyone hears it. This matters later — it makes locking the door lethal, not careless. |
| ~23:40 | **Aaron works out that Nick and Ivy have been involved for two years**, from something Nick says carelessly. He does not raise his voice. That is his entire visible reaction. |
| ~00:50 | **David argues with Nick by the fire** — David telling him to say it out loud before Aaron works it out himself. Nick goes outside to cool off, **in a thin jacket, because David has his coat.** |
| ~00:52 | The mantel clock reads **00:52**. It is visible in the scene if the player looks at it. |
| ~00:55 | **David goes to the door, opens it, calls Nick's name, gets no answer**, assumes he is round the side, goes back to the sofa, **leaves the door unlocked**, and passes out. |
| ~01:20 | **Aaron goes out.** He confronts Nick at the side of the cabin, under the front window. It becomes physical. Nick goes back into the window — **the pane breaks outward-in from the impact of his head and shoulder**, showering shards onto the interior floor. The **grille holds**; the opening is never passable. Nick falls into the snow, concussed and unconscious. |
| ~01:30 | Aaron comes back inside, **turns the key in the door and hangs it on the hook to the left of the frame.** Goes upstairs. |
| ~01:30–07:00 | Nick, unconscious, in a thin jacket, in a storm, **dies of hypothermia.** The head wound does not kill him. **The locked door does.** |
| ~02:00 | **Ivy sees Aaron come in from the landing.** She says nothing then and says nothing now: speaking makes her the motive. |
| ~09:00 | **Priya opens the curtains and sees Nick through the broken window.** She screams. |
| ~09:05 | The door is locked. The key is on the hook. **David unlocks it.** Everyone runs out. |
| ~09:10 | **Aaron says "He looks cold, let's bring him in to the sofa near the fireplace."** David takes the shoulders, Aaron the legs. They move the body indoors — **destroying the position, the snow around him, and the scene.** Aaron's second act, and the one that puts David's hands on the victim. |
| ~09:15 | Priya calls the police. |

**Did David do it? No.** He argued with Nick, took his coat, opened the door, called out, and left
it unlocked — every one of which is an act of decency or accident that reads as guilt in a
transcript. What David is actually guilty of is **two years of silence** about his best friend's
marriage, and **carrying a body across a crime scene because a man he trusted asked him to.**

**Why David is a suspect and not a witness:** he was the last person to speak to Nick; he was found
in the dead man's coat; he cannot account for himself between 00:55 and 09:00 because he was
blackout drunk; his prints are on the body, the door and the key; and the door was found locked
from the inside. **Every fact against him is real. None of them mean what they look like.**

**The false positive:** the officer is right about almost everything he detects. David *is*
hesitant. He *is* concealing something. His account *does* have a hole in it. Every inference is
sound and the conclusion is wrong — because the thing he is hiding is an affair, and the hole is a
blackout, not an alibi.

---

## 2. Cast

| Name | Role | Voice |
|---|---|---|
| **David** (player) | Witness/suspect. First person, never seen, never heard except through the mic. | The player's own |
| **Officer Spassky** | The interrogator. Male. Terse, watchful, unhurried. | ElevenLabs, live TTS from the LLM |
| **Nick** | The dead one. Warm, careless, talked too much. | ElevenLabs, pre-rendered memory lines |
| **Aaron** | Killed Nick and locked the door. Quiet, controlled, hosts the trip. | ElevenLabs, pre-rendered |
| **Ivy** | Aaron's wife. Two years with Nick. Saw Aaron come in. Cannot speak without becoming the motive. | ElevenLabs, pre-rendered |
| **Priya** | Passed out early. Finds the body. Calls the police. Knows nothing. | ElevenLabs, pre-rendered |

> **Voice-cast note — resolved 7 Aug 2026.** Spassky is voiced by **Maksim** ("Raw, unpolished,
> deep", Russian accent), ElevenLabs `6sXsAlJKKBf265ucBSRt`, on `eleven_multilingual_v2`. This is
> now the committed default in `Sidecar/config.py`, so no `.env` entry is needed to get the right
> voice on a fresh machine — only `ELEVENLABS_API_KEY`. `GAME_COMPLETION_PLAN.md` §7 B0 is closed;
> its fallback (keep the female voice, rename the character) is dead. Casting rationale, the
> delivery settings, and the latency this model costs are in
> `docs/superpowers/specs/2026-08-07-spassky-voice-and-delivery-design.md`.
>
> The earlier claim that `llm.py` hardcodes *Detective Mara Voss* was stale: `llm.py:55` has read
> `You are Officer Spassky` since the rename. Only the voice ID was ever wrong.

---

## 3. Scene and phase map

| # | Unity scene | Phase | Player control |
|---|---|---|---|
| S0 | `MainMenu` | — | Menu |
| S1 | `Interrogation` | `P1_TUTORIAL` | Seated, mic, scripted prompt |
| S2 | `Memory_CabinNight` | `M1_NIGHT` | Free-roam FPS |
| S1 | `Interrogation` | `P2_RECALL` | Seated, mic, free speech |
| S3 | `Memory_CabinMorning` | `M2_MORNING` | Free-roam FPS |
| S1 | `Interrogation` | `P3_VERDICT` | Seated, mic, free speech |
| S1 | `Interrogation` | `P4_ENDING` | Cutscene only |

`Memory_CabinMorning` is **the same cabin scene re-lit and re-staged**, not a second build:
morning light, curtains open, broken window, shards on the floor, body outside, door locked.
Built as a second scene file that duplicates `NobodyWentOut_CabinNight.unity` so the two states
can be edited independently without a runtime state-swap system.

> **The interrogation scene is loaded once and never unloaded.** S1 appears four times in the
> table above, and reloading it would mint a new `_sessionId` each time (`DialogueManager.cs:55`),
> which resets the backend's conversation history **and** `ProsodyTracker`'s early-session
> reference on every return. That destroys consistency-across-the-interrogation — the pitch's
> stated "biggest determining factor" — and resets the affect baseline twice. Instead:
> `Interrogation` is loaded **additively at boot and deactivated** during memory scenes, and the
> session ID is owned by `GameFlowDirector` and injected into `DialogueManager`. See
> `GAME_COMPLETION_PLAN.md` T02.

---

## 4. Full beat script

---

**S0 — MAIN MENU**

Cabin exterior at night through falling snow, storm audio bed. Three items: **Play · Settings ·
Quit**. Settings: mic device, master/voice/SFX volume, mouse sensitivity, subtitles on/off,
invert-Y.

**Play →** consent card. Diegetic, not an OS dialog:

> *This game listens. Your microphone stays on for the whole session so the officer can hear you.
> Your voice is processed to transcribe what you say and read the tone you say it in. Nothing is
> recorded to disk.*
> **[ Enable microphone ]  [ Back ]**

Enable → **calibration**: *"Speak normally for a few seconds. Say anything."* A level meter fills;
`VoiceActivityDetector` samples the noise floor, sets the enter/exit thresholds, and stores a
**loud reference RMS** (used later by the yell gate). Fails after 10 s of no signal → *"I can't
hear you. Check your microphone."* with a device dropdown and Retry. Success → *"Good. The officer
can hear you."* → fade to S1.

A **mic indicator** (small icon, top-right) is visible for the rest of the session, and is
**active/inactive** — never "recording", never a red dot suggesting a file is being written.

---

**S1 / P1_TUTORIAL — THE INTERROGATION, WAKING**

Black. Muffled, underwater audio. A voice, three times, each one clearer: *"David."* … *"David."*
… **"David!"** The screen opens as eyelids: two horizontal blur bands retracting, heavy vignette,
focus pulling from 0 to sharp over ~4 s. The room resolves: a table, a hanging lamp, **Officer
Spassky** across from you. Ambient: a fluorescent hum, rain outside.

Mic indicator activates. Prompt appears, bottom-centre:

> **Say:** *"Who are you? Where am I?"*

Free speech is accepted — the gate is *any* utterance over the minimum length, not a phrase match.
If the player says nothing for 15 s, Spassky says *"…You with me?"* and the prompt pulses.

Spassky's answer — pre-rendered VO + Timeline (he leans back, opens a folder):

> **SPASSKY:** *"I'm Officer Spassky. You're one of the suspects involved in the death of Nick. We
> have just finished interrogating the rest of your friends. So here we are. Please — try to
> recall everything that happened last night."*

Fuzzy rewind → S2. Audio pitches down and reverses. Chromatic aberration and radial blur ramp up.
The room's light drains to blue. Hard cut to white, then to the cabin.

---

**S2 / M1_NIGHT — THE CABIN, 00:50**

Stand from the chair — first person, seated at the table. In front of you: **an empty cup**.
Around the table, **four more cups** and a scatter of beer bottles. The camera rises as David
stands; control hands over to `CabinFirstPersonController`.

Free roam. Fire crackling, wind against the walls, the radio hissing static and fragments of
speech. **Objective HUD:** *"Fix the radio."*

**Interactables:**

| Object | Where | Behaviour |
|---|---|---|
| **Radio** | On the fireplace mantel | Objective 1. Look at it, hold **E** — a short tuning minigame (one axis, snap into a band). On success → radio clears. |
| **Mantel clock** | Above the fireplace, beside the radio | Reads **00:52**. Purely optional. Recorded in `MemoryFlags` as `saw_clock`. |
| **The table** | Centre | Look at the cups → `saw_five_cups`. |
| **Coat on the chair** | By the door | Nick's parka, heavy. David is wearing it. Look → `saw_coat_swap`. |
| **Stairs / landing** | Rear | Blocked: *"Aaron and Ivy went up an hour ago."* |
| **Window** | Front wall | Night, curtains drawn. Nothing to see. Intentionally so — this is the same window that is broken in M2. |

Radio clears — camera locks softly to the radio, static resolves:

> **RADIO:** *"…a snow storm. Please stay indoors during these times."*

The door — as the radio line ends, **an off-screen door latch clicks.** The camera turns David's
head involuntarily to the door — it is **swinging closed**, and for ~1.5 s while it is open, the
storm is visible outside: whiteout, near-horizontal snow. It shuts. Objective updates: *"Go to the
door."*

Call for Nick — reaching the door triggers it. **Movement locks. The mic indicator goes active.
The prompt appears:**

> **Call out for Nick.**

The player must speak **loudly** — peak RMS above the calibrated loud reference × `yellFactor`
(default 1.6). Too quiet → the prompt changes to **"Louder — the storm is taking your voice."**
and retries indefinitely. On success: the door swings open onto the whiteout, the wind roars, the
player's yell is swallowed. Silence. **Nothing comes back.**

Fuzzy → S1. Same treatment as the rewind, forward this time.

---

**S1 / P2_RECALL — WHAT DO YOU REMEMBER**

Spassky, live LLM:

> **SPASSKY (opening line for this phase):** *"So. What's the last thing you remember?"*

**Free voice.** No prompts, no menu. This is the heart of the game and where the AI does the work.
The phase runs until **all story marks are covered** (§5) or the turn cap is hit. The officer
presses on gaps, revisits contradictions, changes tone, and sets traps (§6).

Phase ends with Spassky asking, regardless of route:

> **SPASSKY:** *"What happened to Nick?"*

Fuzzy → S3.

---

**S3 / M2_MORNING — THE CABIN, 09:00**

Priya screams — the player wakes on the sofa. Grey morning light. Priya is at the window, curtains
just pulled back, **glass shards on the floor at her feet, the pane gone, the grille intact**:

> **PRIYA:** *"GUYS! GUYS! HELP! WHAT HAPPENED TO NICK? IVY! AARON! DAVID! GUYS, COME HERE PLEASE!"*

Through the window frame: **Nick, face down in the snow, blood matted behind the head, frozen.**

Ivy and Aaron come down — landing, then the stairs.

Control returns. **Objective:** *"Get outside."*

| Object | Behaviour |
|---|---|
| **Front door** | **Locked.** Interacting → *"It's locked."* Flags `found_door_locked`. |
| **Key** | On a hook **immediately left of the door frame**. Look + **E** → *"You take the key."* Flags `found_key_inside`. |
| **Window** | Inspectable. Shards are **inside**. The grille is unbroken. Flags `saw_grille_intact`, `saw_glass_inside`. |
| **Door, with key** | → out into the snow |

Out into the snow — the door opens, all four run out. Snow depth, wind, the body.

> **PRIYA:** *"What do we do?? What do we do??"*
> **IVY:** *"Oh my god, what happened to him? What do we do now?"*
> **AARON:** *"He looks cold. Let's bring him in — to the sofa, near the fireplace."*

The carry — **the longest cutscene.** First person. David takes the shoulders, Aaron the legs. The
camera walks backward at carrying height, swaying, Nick's face in frame the whole way. Slow. Over
it:

> **PRIYA:** *"What could have happened here?"*
> **IVY:** *"I don't know! I was with Aaron upstairs!!"*
> **PRIYA:** *"…All night?"*
> **IVY:** *"…Yes. All night."*
> **AARON:** *"Priya. Not now."*
> **PRIYA:** *"The door was locked. Who locked the door?"*
> **AARON:** *"Lift on three."*

Ivy's line is the alibi she volunteers **for Aaron, unprompted, before anyone accuses anyone.**
That is the clue. Flags `heard_ivy_alibi`, `heard_aaron_deflect`.

The sofa — they lay Nick down. Priya dials. Fuzzy → S1.

---

**S1 / P3_VERDICT — TELL ME WHY I SHOULD SPARE YOUR LIFE**

> **SPASSKY:** *"Tell me why I should spare your life."*

Free voice. The player defends themselves, blames someone, or does neither. If they claim
innocence, the officer **fact-checks against the ground truth and the player's own earlier turns**
before accepting anything.

Spassky does not immediately ask for a name. He looks down at the folder, turns one page, then
slides a **printed group photograph** across the table toward David. It was taken earlier that
night: all five of them crowded together, drunk and smiling.

> **SPASSKY:** *"That's what I don't understand."*
>
> **SPASSKY:** *"Old friends. An anniversary. Drinks. From the way they tell it, things were going
> well."*
>
> **SPASSKY:** *"So when did it all go wrong?"*

**The microphone stays inactive.** David looks down at the photograph.

The fluorescent hum softens into laughter. The photograph fills the frame, then becomes motion.

→ **memory fragment A — THE GOOD YEARS, ~13 s.** This is warm, stable and almost entirely
undistorted. No horror treatment yet. The point is to establish **who everyone is to each other**
without an exposition dump, and to let the later memory destroy this version of the group.

**0–4 s — old friends.** Earlier that evening, ~21:00. Warm fireplace light. Bottles and half-full
glasses across the table. Nick has David in a loose one-armed headlock while Priya holds up an old
school photograph of the two of them, both much younger.

> **PRIYA:** *"Fifteen years and you two still act exactly the same."*
>
> **NICK:** *"He was worse at seventeen."*

Nick grins toward David. David's camera bumps him away; everyone laughs. No David VO.

**4–8 s — Aaron and Ivy.** Priya swipes to another photograph on her phone: **Aaron and Ivy on
their wedding day.** She turns the screen toward them.

> **PRIYA:** *"And two years for these two."*
>
> **AARON:** *"Barely survived it."*

Ivy laughs and leans into Aaron; he puts an arm around her. It reads immediately as a comfortable
married couple. Nick raises his glass toward them. For less than half a second, **Nick and Ivy look
at each other instead of at Aaron.** David notices; the camera drops to the table before the moment
can linger.

**8–11 s — the toast.** Priya lifts her glass.

> **PRIYA:** *"To us. Somehow."*
>
> **NICK:** *"Unfortunately."*

Five glasses meet in the middle of frame. Laughter. This is the cleanest image of the friend group
the player will ever see.

**11–13 s — the coat swap.** As everyone breaks from the toast, Nick notices David's thin jacket
and pulls off his own heavy parka.

> **NICK:** *"Here. You look fucking freezing."*

He throws the parka at David. David catches it and tosses his thin jacket back. Nick puts it on
with exaggerated pride while Priya laughs. The exchange reads as an ordinary best-friend joke.

**Hard cut back to the interrogation room on the sound of the glasses touching.**

The printed photograph is still on the table. Spassky watches David look at it. He lets the silence
sit for one beat.

> **SPASSKY:** *"And somewhere between that photograph and sunrise, Nick ended up dead."*

He pulls the photograph back into the folder.

> **SPASSKY:** *"If it wasn't you, David — who killed Nick?"*

**The microphone still does not activate.**

The instant Spassky says **"Nick"**, the warm laughter from the first memory returns — slowed,
stretched and wrong. The interrogation room begins to become the cabin again, but this time the
transition is violent: light flickers, the photograph disappears, the table is suddenly littered
with empty bottles, and the fire is much lower.

→ **memory fragment B — WHEN IT WENT WRONG, ~13 s.** This is the answer to Spassky's question, but
not an answer David can simply give him. The memory supplies **motive and sequence, not proof of
murder**. Each beat hard-cuts forward in time while the cabin remains spatially recognisable.

The radio warning from M1 bleeds underneath the fragment in broken pieces:

> **RADIO (memory bleed):** *"…snow storm…"*

**0–5 s — Aaron learns the truth, ~23:40.** Nick and Ivy stand too close near the table. Nick,
drunk and careless, says something he should not say while Aaron is close enough to hear:

> **NICK:** *"You've been saying 'after this trip' for two years."*

Ivy freezes. She looks at Nick, then at Aaron.

Aaron has stopped moving. **He does not shout. He does not approach them.** His whole visible
reaction is one quiet question:

> **AARON:** *"…Two years?"*

Ivy says nothing.

> **RADIO (memory bleed):** *"…please stay indoors…"*

David turns toward the fireplace.

**5–11 s — the argument, ~00:50.** Hard jump forward. The party is over without anyone having left
the room: empty bottles, dying fire, chairs abandoned. David and Nick are by the fireplace.
David's remembered side of the conversation appears only as subtitle fragments — **no prerecorded
David VO**, preserving that David is heard only through the player's microphone.

> **DAVID (memory subtitle only):** *"You need to tell him."*
>
> **NICK:** *"He already knows."*
>
> **DAVID (memory subtitle only):** *"Then say it to his face."*

Nick looks back at David, angry and humiliated. He grabs a bottle from the table.

**11–13 s — Nick goes outside.** Nick turns toward the front door. The shot deliberately catches
the jacket from the earlier memory: **he is still wearing David's thin jacket.**

> **NICK:** *"I need some air."*

He opens the door onto the whiteout.

> **RADIO (memory bleed):** *"…during these times."*

The door **slams**.

**Hard return to the interrogation room on the slam.** David is facing the interrogation-room
door. The fluorescent hum snaps back. Spassky has not moved.

One beat.

> **SPASSKY:** *"Who, David?"*

**The microphone activates.**

The system watches for exactly three names: **Aaron · Ivy · Priya.** On a **single unambiguous**
name (not "maybe Aaron or Ivy"):

> **SPASSKY:** *"So you think it's [X], huh? Tell me why."*

→ accusation flashback, one of three, **~3 s each**, no dialogue, heavily degraded. These remain
brief evidence fragments rather than another full memory beat:

- **Aaron** — the landing at night, seen from the sofa, upside down: a figure crossing to the door.
  A bolt or key sound. He does not look back.
- **Ivy** — the top of the stairs. She is already awake, already looking down, and she does not
  move.
- **Priya** — the armchair, hours earlier, asleep with a glass still in her hand. She never moved
  all night, and you know it.

Then:

> **SPASSKY:** *"That's enough from you. I've heard enough."*

→ `P4_ENDING`.

---

**S1 / P4_ENDING — FOUR ENDINGS**

Selection rules in §7. Each is a Timeline cutscene, ~15–25 s, ending on a black card.

| ID | Ending | Beat |
|---|---|---|
| `E_DAVID` | **David is taken.** | Spassky stands, closes the folder, does not look at you. Two officers at the door. The chair scrapes. Black. *"— you were the only one who couldn't tell me where you were."* |
| `E_AARON` | **Aaron is taken.** | Through the observation glass: Aaron in the next chair, being read to. He does not react. Spassky, to you: *"He locked it. You unlocked it. One of those took a decision."* |
| `E_IVY` | **Ivy is taken.** | Ivy through the glass, saying nothing, exactly as she has said nothing all along. Spassky: *"She agreed with you. That's not the same as it being true."* |
| `E_PRIYA` | **Priya is taken.** | Priya through the glass, still crying, still asking what happened. Spassky: *"She's the one who called us. Sit with that."* |

Every ending closes on the same card, which is where the title lands:

> **14:20 — Ivy has asked to make a second statement.**
> **She is still waiting.**
> **FALSE POSITIVE**

The outcome screen then quotes the player back to themselves: 2–3 verbatim lines they said, with
turn numbers. **It never says they lied.** (G6.)

---

## 5. Complete cutscene list

> **P3 memory-pair rule:** `CS-16A` and `CS-16B` are deliberately split by Spassky dialogue. `CS-16A` establishes the social baseline; `CS-16B` corrupts that exact baseline with the affair, motive and final argument. They should reuse the same cabin staging wherever possible so the difference is carried by blocking, props, lighting and performance rather than a new environment.

20 Timeline assets. "Cheap form" is the degradation each falls back to under `GAME_COMPLETION_PLAN.md`
§10.

| ID | Name | Scene | ~sec | Contents | Cheap form |
|---|---|---|---|---|---|
| CS-00 | Consent + calibration | S0 | — | UI only, no Timeline | — |
| CS-01 | Wake | S1 | 8 | Eyelid open, focus pull, 3× "David!", room resolve | Fade from black + VO |
| CS-02 | Spassky's answer | S1 | 12 | Lean back, folder open, VO | Static + VO + subtitle |
| CS-03 | Fuzzy rewind → night | S1→S2 | 3 | Reverse audio, aberration, blue drain, white cut | Fade to white |
| CS-04 | Stand from the chair | S2 | 4 | Camera rise, control handoff | Start standing |
| CS-05 | Radio clears | S2 | 6 | Soft camera lock, static resolves, storm warning | Audio only, no lock |
| CS-06 | Someone left | S2 | 4 | Forced head turn, door swinging shut, whiteout visible | Door already shut + sound |
| CS-07 | Call for Nick | S2 | 10 | Movement lock, mic prompt, loudness gate, door onto storm, silence | Prompt + door open, no gate |
| CS-08 | Fuzzy → interrogation | S2→S1 | 3 | As CS-03, forward | Fade to black |
| CS-09 | Fuzzy → morning | S1→S3 | 3 | As CS-03 | Fade to white |
| CS-10 | Priya screams / body reveal | S3 | 14 | Wake on sofa, Priya at window, VO, body outside | Static camera + VO |
| CS-11 | Ivy and Aaron come down | S3 | 5 | Landing → stairs, two characters moving | Characters already downstairs |
| CS-12 | Out into the snow | S3 | 10 | Door opens, all four out, 3 VO lines | Fade + VO over black |
| CS-13 | **The carry** | S3 | 25 | Backward walking camera, body in frame, 7 VO lines | Fade + VO, camera static at sofa |
| CS-14 | The sofa / Priya dials | S3 | 8 | Lay down, phone, dial tone | Static + VO |
| CS-15 | Fuzzy → verdict | S3→S1 | 3 | As CS-03 | Fade to black |
| CS-16A | The good years | S1 | 13 | Group-photo trigger → old school photo / David + Nick → Aaron + Ivy anniversary → five-person toast → coat swap → hard return to Spassky | One warm cabin setup + still-photo inserts + 4 short VO lines |
| CS-16B | When it went wrong | S1 | 13 | "Who killed Nick?" trigger → Aaron learns about Nick + Ivy → David / Nick fire argument → Nick exits in the thin jacket → hard return to Spassky | Same cabin setup, darker lighting + hard cuts + VO/subtitles + radio bleed |
| CS-17a/b/c | Accusation flashback ×3 | S1 | 3 ea | Brief degraded evidence fragment after the player names Aaron / Ivy / Priya | Still frame + SFX |
| CS-18a/b/c/d | Endings ×4 | S1 | 15–25 ea | Per §4 | Still + VO + card |

---

## 6. Story marks — what P2_RECALL must cover

The phase ends when the officer has an answer, however evasive, on **all seven**. Marks are
matched from the transcript (backend ask B2, or the client-side keyword fallback below).

| Mark | The question behind it | Keywords (client fallback) |
|---|---|---|
| `m_fire` | Was he drinking, and with whom, and until when? | drink, drank, beer, fire, fireplace, sat |
| `m_argument` | What was the argument with Nick about? | argue, argument, fight, told him, said to him |
| `m_nick_left` | Did he see Nick go out? | left, went out, outside, door closed, gone |
| `m_door` | Did he go to the door? Did he call out? | door, opened, called, shouted, yelled, his name |
| `m_lock` | Was the door locked when he left it? | lock, locked, unlocked, bolt, key |
| `m_sleep` | What did he do after? How long was he out? | sofa, slept, passed out, blacked out, don't remember |
| `m_morning` | What happened when Priya screamed? | scream, morning, window, carried, sofa, Aaron |

---

## 7. The traps — details the player cannot have seen

A confident answer to any of these is a **fabrication**, and fabrication is the heaviest single
input to the ending. The officer baits them.

| Trap | Why it is unseeable | The bait |
|---|---|---|
| **Who left through the door in the "someone left" beat** | The door is already closing; nobody is visible | *"You said you looked up. Who was it?"* |
| **The time of anything** | Only readable if the player chose to look at the mantel clock | *"You're very precise about one o'clock. How do you know?"* |
| **Whether Nick answered** | The yell gets nothing back — deliberately | *"Did you hear him out there, or did you assume?"* |
| **Whether Nick was already outside** | Never seen after the fire | *"How do you know he was even out there yet?"* |
| **Who locked the door** | David was unconscious | *"So the door was locked before you fell asleep, or after?"* |
| **The window** | Broken at ~01:20 while David was unconscious; not visible at night | *"Did you hear glass?"* |

> **The cruellest one, kept from `bong.md`:** naming Aaron for the bolt is a *fabrication that
> happens to be correct.* The officer catches the fabrication. Being right is not the same as
> having seen it — and the game punishes the gap. This is the thesis in one mechanic.

---

## 8. Ending selection

Three tracked quantities, all computed from the interrogation, none of them ever shown as truth:

| Quantity | Source | Range |
|---|---|---|
| `credibility` | Consistency across turns, coverage of the seven marks, absence of caught fabrications | 0..1 |
| `composure` | Affect signal aggregated across turns (hesitation, onset delay, tension trend) | 0..1 |
| `accusation` | Which of Aaron / Ivy / Priya was named unambiguously in P3, plus how well the reasoning matched the real clue set | enum + 0..1 |

| Ending | Condition |
|---|---|
| `E_DAVID` | `credibility < 0.45` **or** two-plus caught fabrications **or** no name given in P3 |
| `E_AARON` | `credibility ≥ 0.6`, named **Aaron**, and cited **≥ 2** of: door locked / key inside / grille intact / thin jacket / Aaron learning about the affair / Ivy's volunteered alibi / Aaron proposing the move |
| `E_IVY` | `credibility ≥ 0.6`, named **Ivy** |
| `E_PRIYA` | `credibility ≥ 0.6`, named **Priya** |

Naming Aaron with weak or no reasoning falls to `E_DAVID` — you were right and could not say why,
which is exactly the trap in §7. `composure` never decides an ending on its own; it moves
`credibility` by at most ±0.1. **This is a G6 requirement, not a balance choice.**

---

## 9. Clue ledger — what a player can actually piece together

| # | Clue | Where |
|---|---|---|
| 1 | Five cups. Nobody came in from outside. | M1 table |
| 2 | The radio warned everyone to stay in. | M1, radio clears |
| 3 | Nick left in a thin jacket because David had his coat. | P3 good-years memory, M1 coat, M2 body |
| 4 | David left the door **unlocked**. | M1, call for Nick |
| 5 | The door was **locked** in the morning. | M2 door |
| 6 | The key is on a hook **inside**. Only someone inside could lock it. | M2 key |
| 7 | The grille is intact and the shards are **inside** — nobody came through the window. | M2 window |
| 8 | Nick and Ivy had been involved for **two years**, and Aaron learned about it that night. This establishes motive, not proof of murder. | P3, when-it-went-wrong memory |
| 9 | Ivy volunteers an alibi **for Aaron**, unprompted. | M2, the carry |
| 10 | Aaron is the one who proposes moving the body. | M2, out into the snow / the carry |
| 11 | The mantel clock read 00:52. | M1, optional |
