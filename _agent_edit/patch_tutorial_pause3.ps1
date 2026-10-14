$ErrorActionPreference = "Stop"
$path = "D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs"
$bakDir = "D:\UnitProject\26-10-8\_backup_tutorial_pause_20261014_181800"
New-Item -ItemType Directory -Force -Path $bakDir | Out-Null
Copy-Item -LiteralPath $path -Destination (Join-Path $bakDir "TutorialManager.cs") -Force
$text = [IO.File]::ReadAllText($path)
$nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }

function Rep([ref]$t, $old, $new, $tag) {
  $i = $t.Value.IndexOf($old)
  if ($i -lt 0) { throw "missing $tag" }
  $t.Value = $t.Value.Remove($i, $old.Length).Insert($i, $new)
}

Rep ([ref]$text) ("    private string[] _freePlayKeys;" + $nl + $nl + "    // NotifyCondition") ("    private string[] _freePlayKeys;" + $nl + "    private bool _battleHeld;" + $nl + $nl + "    // NotifyCondition") "field"

Rep ([ref]$text) ("        Instances.Remove(this);" + $nl + "        SetBlock(false);" + $nl + "    }") ("        Instances.Remove(this);" + $nl + "        SetBlock(false);" + $nl + "        SyncBattlePause(true);" + $nl + "    }") "destroy"

$fpMarker = "Debug.Log(""[TutorialManager] free play step """
$fpIdx = $text.IndexOf($fpMarker)
if ($fpIdx -lt 0) { throw "missing freeplay log" }
$before = $text.LastIndexOf("SetBlock(false);", $fpIdx)
if ($before -lt 0) { throw "missing SetBlock before freeplay" }
$insertAt = $before + "SetBlock(false);".Length
$ins = $nl + "        SyncBattlePause();"
$chunk = $text.Substring($insertAt, [Math]::Min(40, $text.Length - $insertAt))
if ($chunk -notmatch "SyncBattlePause") {
  $text = $text.Insert($insertAt, $ins)
}

$adv = "        SetBlock(true);" + $nl + "        Advance();"
$advNew = "        SetBlock(true);" + $nl + "        SyncBattlePause();" + $nl + "        Advance();"
Rep ([ref]$text) $adv $advNew "completeFree"

$showOld = "            overlay.Show(target, step.holePadding, step.text, step.portrait, _tapMode);" + $nl + "        }" + $nl + $nl + "        _flow = null;"
$showNew = "            overlay.Show(target, step.holePadding, step.text, step.portrait, _tapMode);" + $nl + "        }" + $nl + $nl + "        SyncBattlePause();" + $nl + "        _flow = null;"
Rep ([ref]$text) $showOld $showNew "show"

$hideOld = "                overlay.Hide();" + $nl + "            }" + $nl + "        }" + $nl + "    }" + $nl + $nl + "    private void ClearMode()"
$hideNew = "                overlay.Hide();" + $nl + "            }" + $nl + "        }" + $nl + $nl + "        SyncBattlePause();" + $nl + "    }" + $nl + $nl + "    private void ClearMode()"
Rep ([ref]$text) $hideOld $hideNew "hide"

$clearOld = "        _currentRequired = false;" + $nl + "    }" + $nl + $nl + "    private void StopListening()"
$clearNew = "        _currentRequired = false;" + $nl + "        SyncBattlePause();" + $nl + "    }" + $nl + $nl + "    private void StopListening()"
Rep ([ref]$text) $clearOld $clearNew "clear"

$finAnchor = "        if (overlay != null && wasRunning)" + $nl + "        {" + $nl + "            overlay.Hide();" + $nl + "        }"
$finPos = $text.IndexOf($finAnchor)
if ($finPos -lt 0) { throw "missing finish hide" }
$finEnd = $finPos + $finAnchor.Length
$finIns = $nl + $nl + "        SyncBattlePause(true);"
$afterFin = $text.Substring($finEnd, [Math]::Min(40, $text.Length - $finEnd))
if ($afterFin -notmatch "SyncBattlePause") {
  $text = $text.Insert($finEnd, $finIns)
}

$helpers = $nl + $nl + "    /// <summary>" + $nl + "    /// Battle tutorial only: freeze fight (BattleTime / timeScale) while a non-Event overlay step is showing." + $nl + "    /// Resume on freePlay, Event (player must act on world), while waiting (overlay hidden), and when tutorial ends." + $nl + "    /// </summary>" + $nl + "    private void SyncBattlePause(bool forceRelease = false)" + $nl + "    {" + $nl + "        bool wantHold = !forceRelease && ShouldHoldBattle();" + $nl + "        if (wantHold == _battleHeld)" + $nl + "        {" + $nl + "            return;" + $nl + "        }" + $nl + $nl + "        if (wantHold)" + $nl + "        {" + $nl + "            BattleTime.Hold();" + $nl + "            _battleHeld = true;" + $nl + "        }" + $nl + "        else" + $nl + "        {" + $nl + "            BattleTime.Release();" + $nl + "            _battleHeld = false;" + $nl + "        }" + $nl + "    }" + $nl + $nl + "    private bool ShouldHoldBattle()" + $nl + "    {" + $nl + "        if (!battleTutorialOnly || _index < 0 || _freePlay)" + $nl + "        {" + $nl + "            return false;" + $nl + "        }" + $nl + $nl + "        // Event steps need nets/enemies/towers to tick (and player world input)." + $nl + "        if (!string.IsNullOrEmpty(_eventKey))" + $nl + "        {" + $nl + "            return false;" + $nl + "        }" + $nl + $nl + "        return _showing;" + $nl + "    }" + $nl

$rt = "    private Transform ResolveTarget(TutorialStep step)"
if ($text.Contains("private void SyncBattlePause")) { throw "helpers already present" }
Rep ([ref]$text) $rt ($helpers + $rt) "helpers"

$enc = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText($path, $text, $enc)
Write-Output "PATCHED OK"
Write-Output ("_battleHeld=" + ([regex]::Matches($text, "_battleHeld")).Count)
Write-Output ("SyncBattlePause=" + ([regex]::Matches($text, "SyncBattlePause")).Count)
Write-Output ("ShouldHoldBattle=" + ([regex]::Matches($text, "ShouldHoldBattle")).Count)
