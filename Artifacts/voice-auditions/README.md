# FALSE POSITIVE voice auditions

This directory preserves historical ElevenLabs audition metadata and playback scripts. Generated audition WAVs are removed after a character's voice is finalized; rerunning a historical player requires regenerating its referenced files.

Finalized production voices are Priya as Aaira, Ivy as Laura, and Officer Spassky as Maksim. Nick's selected voice is Ivan Energetic, and the radio announcer's selected voice is Roger; their complete production sets have not yet been rendered. Full casting and delivery decisions are in `../voice_guide/`. David's auditions were reference-only because the canonical game normally uses the player's microphone for his dialogue.

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
- `nick/play_nick_line_02_two_years.py`
- `nick/play_nick_final_decider.py`

Candidates: 1 Ivan, 2 Denis, 3 Ivan Energetic, 4 Alexei, 5 Oleg, 6 Guy, 7 Alex Bell, 8 Escobar.

Final selection: **Ivan Energetic** (`JKtNvDNrWu33P1xzttP2`), Eleven V3 with Natural stability. He was candidate 3 in the original rounds and finalist 1 in the Ivan-Energetic-versus-Alexei decider. Production rendering is pending; see `../voice_guide/Nick.md`.

### Aaron

- `aaron/play_aaron_line_01_body.py`
- `aaron/play_aaron_line_02_deflection.py`
- `aaron/play_aaron_line_03_command.py`
- `aaron/play_aaron_jock_line_01_body.py`
- `aaron/play_aaron_jock_line_02_deflection.py`
- `aaron/play_aaron_jock_line_03_command.py`
- `aaron/play_aaron_jock_line_04_two_years.py`

Candidates: 1 Brian, 2 Daniel, 3 George, 4 Eric, 5 Liam, 6 Will, 7 Callum, 8 Roger.

The four `play_aaron_jock_*` entry points form the revised grounded-jock round.
They use Eleven V3 Natural settings and cover `AARON-001`, `AARON-002`,
`AARON-003`, and `AARON-005`.

Final selection: **Liam** (`TX3LPaxmHKxFdv7VOQHJ`), Eleven V3 with Natural
stability. He was candidate 5 in the revised grounded-jock round. Production
rendering is pending; see `../voice_guide/Aaron.md`.

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
- `radio-announcer/play_radio_announcer_v3_natural_line_01_storm_warning.py`
- `radio-announcer/play_radio_announcer_roger_line_03_stay_indoors.py`

Candidates: 1 Roger, 2 Sarah, 3 Daniel, 4 Matilda, 5 George, 6 Jessica, 7 Chris, 8 Alice.

The `v3_natural` entry point is the current clean casting round. It uses Eleven
V3 Natural settings with no synthesized static, filtering, labels, or candidate
numbers. The older entry point preserves the historical V2 audition metadata.

Final selection: **Roger** (`CwhRBWXzGAHq8TQ4Fs17`). The `RADIO-001` and
`RADIO-003` audition takes are approved; `RADIO-002` and `RADIO-004` still need
production renders. See `../voice_guide/RadioAnnouncer.md`.

## Generation reference

`generate_all_auditions.py` records every candidate's exact ElevenLabs voice ID and the shared synthesis settings. It reads `ELEVENLABS_API_KEY` from the process environment and never stores the key. Existing WAVs are skipped, so rerunning it only fills missing files.
