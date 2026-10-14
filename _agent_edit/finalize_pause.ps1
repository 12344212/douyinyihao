$path = "D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs"
$text = [IO.File]::ReadAllText($path)
$old = "        return _showing;`r`n    }`r`n    private Transform ResolveTarget"
$new = "        return _showing;`r`n    }`r`n`r`n    private Transform ResolveTarget"
if ($text.Contains($old)) {
  $text = $text.Replace($old, $new)
  $enc = New-Object System.Text.UTF8Encoding $false
  [IO.File]::WriteAllText($path, $text, $enc)
  Write-Output "newline fixed CRLF"
} else {
  $old2 = "        return _showing;`n    }`n    private Transform ResolveTarget"
  $new2 = "        return _showing;`n    }`n`n    private Transform ResolveTarget"
  if ($text.Contains($old2)) {
    $text = $text.Replace($old2, $new2)
    $enc = New-Object System.Text.UTF8Encoding $false
    [IO.File]::WriteAllText($path, $text, $enc)
    Write-Output "newline fixed LF"
  } else {
    Write-Output "already spaced or different"
  }
}

$report = @"
Battle tutorial pause fix (2026-10-14 Asia/Shanghai)

BUG
During battle tutorial overlay steps, BattleDirector kept ticking (Time.deltaTime),
so drift resources moved out of range and spotlight/event steps got stuck.

FIX
Assets/Scripts/Tutorial/TutorialManager.cs
- Use existing BattleTime.Hold/Release (same as pause panel / build shop; sets timeScale 0/1).
- Only when battleTutorialOnly is true.
- Hold while a non-Event overlay step is showing (_showing && no _eventKey && not freePlay).
- Resume on:
  - freePlay (BeginFreePlay)
  - Event advance (player/world must act; nets/enemies/towers need dt)
  - HideOverlay waits (waitFor / target resolve / showDelay)
  - tutorial Finish / OnDestroy (forceRelease)

BattleDirector.Update already uses Time.deltaTime, so Hold freezes resources, waves, combat.
Tutorial UI still works (Update + WaitForSecondsRealtime + overlay DOTween unscaled).

NO CHANGE needed in BattleDirector.Tutorial.cs / BattleTime.cs API.

BACKUP
_backup_tutorial_pause_20261014_181800/TutorialManager.cs

VERIFY IN EDITOR
1. Reset battle tutorial save; play stage 1-1.
2. On Tap/Auto explanation steps: resources should freeze (timeScale 0).
3. On Event (throw net / build) and freePlay (reel / harvest / kill): timeScale 1, can act.
4. After tutorial completes: timeScale 1.
"@
[IO.File]::WriteAllText("D:\UnitProject\26-10-8\_agent_edit\battle_tutorial_pause_fix.txt", $report)
Write-Output "report written"
