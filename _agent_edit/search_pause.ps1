Get-ChildItem "D:\UnitProject\26-10-8\Assets\Scripts" -Recurse -Filter "*.cs" | Select-String -Pattern "BattleTime|timeScale|IsHeld|Hold\(|Release\(" | Select-Object -First 80 Path,LineNumber,Line
