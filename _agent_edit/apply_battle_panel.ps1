$ErrorActionPreference = "Stop"
$scenePath = "Assets\Scenes\BattleScene.unity"
$raw = [System.IO.File]::ReadAllText((Resolve-Path $scenePath))
$raw = $raw -replace "`r`n","`n"

function LF([string]$s) { return (($s -replace "`r`n","`n").TrimEnd() + "`n") }

# Load manager/targets from Temp extracted files if we write them separately for clarity
$managerPath = "_agent_edit\battle_manager.yml"
$targetsPath = "_agent_edit\battle_targets.yml"
if (-not (Test-Path $managerPath)) { throw "missing manager yml" }

$manager = LF ([System.IO.File]::ReadAllText($managerPath))
$targets = LF ([System.IO.File]::ReadAllText($targetsPath))

$goOld = "  m_Component:`n  - component: {fileID: 2069278280}`n  - component: {fileID: 2069278281}`n  - component: {fileID: 2069278282}`n  m_Layer: 5`n  m_Name: TutorialPanel"
$goNew = "  m_Component:`n  - component: {fileID: 2069278280}`n  - component: {fileID: 2069278281}`n  - component: {fileID: 2069278282}`n  - component: {fileID: 2069278283}`n  m_Layer: 5`n  m_Name: TutorialPanel"
if (-not $raw.Contains($goOld)) { throw "GO list not found" }
$raw = $raw.Replace($goOld, $goNew)
Write-Host "GO ok"
$mgrRe = [regex]::new('--- !u!114 &2069278282\nMonoBehaviour:.*?wiredStepsVersion: \d+\n', 'Singleline')
if (-not $mgrRe.IsMatch($raw)) { throw "manager not found" }
$raw = $mgrRe.Replace($raw, $manager, 1)
Write-Host "manager ok"
$marker = "wiredStepsVersion: 2`n--- "
if (-not $raw.Contains($marker)) { throw "insert marker missing" }
if ($raw -notmatch 'guid: 0008c5b6b51a81941ab79bb392548e42') {
  $raw = $raw.Replace($marker, ("wiredStepsVersion: 2`n" + $targets + "--- "))
  Write-Host "targets inserted"
} else { Write-Host "targets present" }
[System.IO.File]::WriteAllText((Resolve-Path $scenePath), $raw, (New-Object System.Text.UTF8Encoding $false))
Write-Host "saved"
Write-Host "battle=$($raw.Contains('tutorialKey: battle')) role=$($raw.Contains('tutorialKey: role')) B1=$($raw.Contains('note: B1 click resource')) targets=$($raw.Contains('guid: 0008c5b6b51a81941ab79bb392548e42'))"
