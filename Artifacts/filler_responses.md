# Interviewer filler responses

These are the 10 canonical acknowledgements for Officer Spassky to play after
the player's voice turn has been captured and while the backend prepares his
actual response. They are intentionally terse, watchful, and noncommittal so
they do not imply that the player's account is true, false, clear, or
suspicious.

The corresponding production WAVs and synthesis manifest live in
`Artifacts/voice-lines/spassky_filler/`.

## Production pool

| ID | Spoken line |
|---|---|
| `SPASSKY-FILLER-001` | "Hm." |
| `SPASSKY-FILLER-003` | "I see." |
| `SPASSKY-FILLER-004` | "Interesting." |
| `SPASSKY-FILLER-005` | "All right." |
| `SPASSKY-FILLER-006` | "Very well." |
| `SPASSKY-FILLER-007` | "Noted." |
| `SPASSKY-FILLER-009` | "That's noted." |
| `SPASSKY-FILLER-010` | "Hm. I see." |
| `SPASSKY-FILLER-013` | "Interesting. All right." |
| `SPASSKY-FILLER-023` | "Hm... interesting." |

## Usage notes

- Play a filler only after voice capture has stopped, so it cannot leak into the player's recording.
- Treat these as latency cover, never as Spassky's complete response. The generated response should still contain the substantive follow-up.
- Prefer the one-phrase lines for normal turns. Use the two-phrase lines sparingly for variation.
- Randomize with a short no-repeat history rather than selecting uniformly every turn.
- Do not use lines such as "You're right," "I believe you," "That sounds suspicious," or "Now we're getting somewhere." They would judge the testimony before the backend has finished processing it.
- Avoid "Go on," "Keep talking," and "Take your time." They invite more speech after the player's turn has already been submitted.
