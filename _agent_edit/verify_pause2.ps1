$lines = Get-Content "D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs"
for ($j = 565; $j -le 580; $j++) { "{0,4}|{1}" -f ($j+1), $lines[$j] }
Write-Output "===="
for ($j = 630; $j -le 675; $j++) { "{0,4}|{1}" -f ($j+1), $lines[$j] }
Write-Output "==== complete free ===="
for ($j = 420; $j -le 445; $j++) { "{0,4}|{1}" -f ($j+1), $lines[$j] }
