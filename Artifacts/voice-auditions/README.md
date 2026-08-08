# FALSE POSITIVE voice auditions

This directory preserves historical ElevenLabs audition metadata and playback scripts. Generated audition WAVs are removed after a character's voice is finalized; rerunning a historical player requires regenerating its referenced files.

<<<<<<< HEAD
Finalized production voices: Priya uses Aaira and Ivy uses Laura, both with Eleven V3 Natural stability. Their complete ID-based production sets and settings are in `../voice-lines/priya/` and `../voice-lines/ivy/`. David's auditions were reference-only because the canonical game normally uses the player's microphone for his dialogue.
=======
Priya's finalized production voice is Aaira with Eleven V3 Natural stability. Her complete ID-based production set and settings are in `../voice-lines/priya/`. Officer Spassky's finalized production voice is Maksim; his set is in `../voice-lines/spassky/`. No other character has been finalized. David's auditions were reference-only because the canonical game normally uses the player's microphone for his dialogue.
>>>>>>> 1013b3247cd915b1d060ac68ca9a2833fcbf00b5

## Running an audition

Run the playback file for the character and line you want to compare:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-auditions\spassky\play_spassky_line_03_verdict.py
```

Pass `--dry-run` to verify candidate numbering and file paths without playing audio:

```powershell
Sidecar\.venv\Scripts\python.exe Artifacts\voice-auditions\spassky\play_spassky_line_03_verdict.py --dry-run
```

## Playback file index

### David, reference only

- `david/play_david_line_01_confusion.py`
- `david/play_david_line_02_confession.py`
- `david/play_david_line_03_defense.py`

Candidates: 1 Brian, 2 Daniel, 3 George, 4 Eric, 5 Liam, 6 Will, 7 Callum, 8 Roger.

### Officer Spassky

- `spassky/play_spassky_line_01_introduction.py`
- `spassky/play_spassky_line_02_evidence_pressure.py`
- `spassky/play_spassky_line_03_verdict.py`

Candidates: 1 Stanislav, 2 Alexei, 3 Ivan, 4 Denis, 5 Alex Bell, 6 Artem Lebedev, 7 Dmitry, 8 Valery.

Final selection: **Maksim — "Raw, unpolished, deep"** (`6sXsAlJKKBf265ucBSRt`), `eleven_multilingual_v2`. Maksim is not among the eight numbered candidates above — he came from a later round against the brief *semi-deep, snarly, raspy, Russian, angry in a contained way*, which none of the first eight carried. Casting rationale and the delivery-register table are in `../voice_guide/Spassky.md`. Production files are `../voice-lines/spassky/SPASSKY-001.wav` through `SPASSKY-062.wav`.

### Nick

- `nick/play_nick_line_01_fire_argument.py`

Candidates: 1 Ivan, 2 Denis, 3 Ivan Energetic, 4 Alexei, 5 Oleg, 6 Guy, 7 Alex Bell, 8 Escobar.

### Aaron

- `aaron/play_aaron_line_01_body.py`
- `aaron/play_aaron_line_02_deflection.py`
- `aaron/play_aaron_line_03_command.py`

Candidates: 1 Brian, 2 Daniel, 3 George, 4 Eric, 5 Liam, 6 Will, 7 Callum, 8 Roger.

### Ivy

- `ivy/play_ivy_line_01_shock.py`
- `ivy/play_ivy_line_02_alibi.py`
- `ivy/play_ivy_line_03_confirmation.py`
- `ivy/play_ivy_final_decider.py`
- `ivy/play_ivy_long_final_decider.py`

Candidates: 1 Jessica, 2 Matilda, 3 Laura, 4 Lily, 5 Sarah, 6 Alice, 7 Aria, 8 Charlotte.

Final selection: Laura (`FGY2WhTYpPnrIDTdsKH5`), Eleven V3 Natural stability. Production files are `../voice-lines/ivy/IVY-001.wav` through `IVY-004.wav`.

### Priya

- `priya/play_priya_line_01_panic.py`
- `priya/play_priya_line_01_panic_v3_natural.py`
- `priya/play_priya_aaira_name_call_variations.py`
- `priya/play_priya_aaira_v3_final_validation_lines.py`
- `priya/play_priya_line_02_suspicion.py`
- `priya/play_priya_line_03_concern.py`

Candidates: 1 Anika, 2 Monika Sogam, 3 Mahi, 4 Aisha, 5 Aaira, 6 Aaliyah, 7 Aasha, 8 Saavi.

Final selection: Aaira (`1XNFRxE3WBB7iI0jnm7p`), Eleven V3 Natural stability. Production files are `../voice-lines/priya/PRIYA-001.wav` through `PRIYA-008.wav`.

### Radio announcer

- `radio-announcer/play_radio_announcer_line_01_storm_warning.py`

Candidates: 1 Roger, 2 Sarah, 3 Daniel, 4 Matilda, 5 George, 6 Jessica, 7 Chris, 8 Alice.

## Generation reference

`generate_all_auditions.py` records every candidate's exact ElevenLabs voice ID and the shared synthesis settings. It reads `ELEVENLABS_API_KEY` from the process environment and never stores the key. Existing WAVs are skipped, so rerunning it only fills missing files.
