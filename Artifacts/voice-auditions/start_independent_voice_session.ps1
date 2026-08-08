$workspace = "D:\SUTD\Hack_Garena'26"
$handoff = Join-Path $workspace "Artifacts\voice-auditions\NEXT_SESSION_HANDOFF.md"

Set-Location -LiteralPath $workspace

codex `
    --model gpt-5.6-sol `
    --config 'model_reasoning_effort="high"' `
    --cd $workspace `
    "Read $handoff completely, then carry out its preparation task. Do not play audio."
