$path = "D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs"
Select-String -Path $path -Pattern "_battleHeld|SyncBattlePause|ShouldHoldBattle|BattleTime" | ForEach-Object { "$($_.LineNumber):$($_.Line.Trim())" }
Write-Output "==== helpers ===="
$lines = Get-Content $path
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match "private void SyncBattlePause") {
    for ($j = $i - 3; $j -le $i + 45 -and $j -lt $lines.Count; $j++) {
      if ($j -ge 0) { "{0,4}|{1}" -f ($j+1), $lines[$j] }
    }
    break
  }
}
Write-Output "==== show sync ===="
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match "overlay.Show\(target") {
    for ($j = $i; $j -le $i + 8 -and $j -lt $lines.Count; $j++) { "{0,4}|{1}" -f ($j+1), $lines[$j] }
    break
  }
}
Write-Output "==== freeplay sync ===="
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match "free play step" -and $lines[$i] -match "waiting for") {
    for ($j = [Math]::Max(0,$i-5); $j -le $i; $j++) { "{0,4}|{1}" -f ($j+1), $lines[$j] }
    break
  }
}
