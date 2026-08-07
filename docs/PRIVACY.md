# Privacy

Before the game records microphone audio, it must show an in-game notice and let the player choose
whether to continue. The scene still needs this notice wired in before release.

## What leaves the game

The game sends microphone audio to Google Cloud Speech-to-Text to create a transcript. The service
sends the transcript and a limited prosody context to Vertex AI so the detective can reply. It sends
the detective's reply text to ElevenLabs to create speech. The service does not send raw audio,
embeddings, or model scores to Vertex AI or ElevenLabs.

## What the game keeps

The application holds audio, transcripts, session history, and prosody data in memory while a
session is active. It does not write player audio, transcripts, or embeddings to its own database
or files. Cloud providers may process request data under their own service terms and retention
settings. This project does not claim to control those provider policies.

## What the game does not do

FALSE POSITIVE does not detect lies, truthfulness, guilt, or intent. It may use broad voice and
conversation signals to shape a fictional detective's questions, but those signals are not proof
of what a player believes or has done.

## Required in-game notice

Show this before the first microphone capture:

> This game sends your voice to Google Cloud for speech recognition. The transcript and limited
> voice context help generate the detective's reply through Google Cloud and ElevenLabs. The game
> does not store your audio and does not detect lies. Continue only if you agree.

The player must be able to decline before recording begins. The Unity scene owner must connect this
notice before release.
