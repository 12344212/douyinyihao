$path = "D:\UnitProject\26-10-8\_agent_edit\patch_tutorial_pause2.ps1"
Get-Content $path | Select-Object -First 15
Write-Output "---"
Select-String -Path $path -Pattern "utf8|UTF8|WriteAllText|false" | ForEach-Object { "$($_.LineNumber):$($_.Line)" }
Write-Output "---orig---"
Select-String -Path "D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs" -Pattern "_battleHeld|SyncBattlePause" | ForEach-Object { "$($_.LineNumber):$($_.Line)" }
