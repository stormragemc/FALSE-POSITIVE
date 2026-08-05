# Your voice in FALSE POSITIVE

**Short version:** the game records you speaking, sends those recordings to our server to be
turned into text and read for tone of voice, and throws the audio away. It cannot tell whether
you are lying. Nothing you say is used to train anything.

The long version is below, because you are talking to a machine about a crime and you deserve
to know exactly where that goes.

---

## What gets recorded

The game is played by voice. There are no dialogue menus, so the microphone has to stay open
during an interrogation — otherwise the game could not tell when you had started speaking.

While it is open, a few seconds of audio sit in a rolling buffer **on your own machine**, which
is continuously overwritten and never sent anywhere. When the game detects that you have started
and then finished a sentence, it takes that one stretch of speech — an *utterance* — and sends
it to us. Utterances are short by design and are cut off after 20 seconds.

Silence, background noise, and everything you say before or after an utterance never leave your
computer.

## Where it goes

Each utterance is sent over an encrypted HTTPS connection to the game's backend, which runs on
Google Cloud. There, three things happen to it:

1. **Google Cloud Speech-to-Text** turns it into text, so the detective can understand what you
   said.
2. **A speech model called HuBERT** reads it for *tone* — hesitation, tension, pauses, changes
   in pace and pitch. This model runs on our own server, not at a third party.
3. **The audio is discarded.** It exists in the server's memory for the few seconds a single
   turn takes, and is never written to a disk, a database, or a log file.

The detective's reply is then written by **Google's Gemini** model and spoken aloud by
**ElevenLabs**. Those two see the *text* of the conversation and a short summary of your tone —
they never receive your audio.

## What is kept, and for how long

| Thing | Kept | For how long |
|---|---|---|
| Your recorded audio | No | Discarded as soon as the turn is answered |
| The text of what you said | Yes, in the server's memory | Until reset/restart, or one hour after the last activity (cleanup may take up to one additional minute) |
| Tone measurements | Yes, in the server's memory | Same as above |
| Anything on disk | **No** | — |
| Your name, email, or account | **Not collected at all** | — |

The detective remembers your testimony for the length of one interrogation, because catching you
contradicting yourself is the entire game. That memory lives in the server's working memory
only. It expires automatically after one hour without activity (with cleanup at least once per
minute), or sooner when it is reset or the server restarts, and there is no copy.

We do not know who you are. The game identifies your session with a random ID it generates when
the scene loads, and that ID is not linked to anything.

## What this system cannot do

**It cannot detect lies. Neither can anything else.**

This matters enough to be the name of the game. What the model measures is *affect* — that your
voice tightened, that you hesitated, that your pace changed. It has no way of knowing *why*.
Fear sounds like guilt. It also sounds like a faulty memory, or like being accused of something
you did not do.

The detective in the game will act as if it knows. That is the drama, and it is meant to be
unfair. The system underneath makes no such claim, produces no truth score, and returns no
verdict about your honesty. If you ever see the game assert that you lied, that is a bug worth
reporting.

The tone model is also just not very accurate. It recognises four broad categories — neutral,
happy, angry, sad — was trained on acted emotional speech rather than real interrogations, and
is right roughly two thirds of the time. The game caps how confident it is allowed to sound
because of this.

## Training

**Nothing you say is used to train any model.** Not ours, not Google's, not ElevenLabs'. We keep
no corpus of player audio because we keep no player audio at all.

## Changing your mind

Stop speaking and the recording stops. Quitting does not send more audio. Session text and tone
state then expire automatically after one hour without activity; a normal reset or server restart
can remove them sooner. There is no account or long-term archive to request.

---

## Notes for the team and for judges

*This section is not aimed at players.*

- Audio leaves the player's machine as of the 4 Aug 2026 cloud migration. Before that the sidecar
  ran locally and this document would have read very differently. The change was deliberate — it
  is what makes a downloadable build possible — and the old "audio never leaves your machine"
  claim has been removed everywhere it appeared.
- Where the code backs each claim: audio is never persisted (`Sidecar/app.py` holds the buffer
  for one request), session text lives in `Sidecar/session_store.py` in memory, the affect
  pipeline and its confidence cap are in `Sidecar/prosody.py`, and the trust-block separation
  that keeps witness speech out of the model's instructions is in `Sidecar/llm.py`.
- `SESSION_IDLE_TTL_SECONDS` defaults to one hour. The backend reaper runs at least once per
  minute and clears matching conversation and affect state together.
- **Not yet built:** the in-game notice shown before the first recording (roadmap S8). This
  document exists; the in-product disclosure does not. Do not describe S8 as finished until a
  player can read something like this without opening the repo.
