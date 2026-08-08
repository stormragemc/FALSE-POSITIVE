param(
    [Parameter(Mandatory = $true)]
    [string]$Handoff
)

$workspace = "D:\SUTD\Hack_Garena'26"
$resolvedHandoff = (Resolve-Path -LiteralPath $Handoff).Path

Set-Location -LiteralPath $workspace

codex `
    --model gpt-5.6-sol `
    --config 'model_reasoning_effort="high"' `
    --cd $workspace `
    "Read $resolvedHandoff completely, then carry out its assigned voice-generation task."
