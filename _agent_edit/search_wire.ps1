# Look for battle tutorial step config in scene or wire scripts
Get-ChildItem "D:\UnitProject\26-10-8\Assets\Scripts\Editor" -Filter "*Tutorial*" | Select-Object Name
Select-String -Path "D:\UnitProject\26-10-8\Assets\Scripts\Editor\TutorialBattleSceneWire.cs" -Pattern "freePlay|conditionKey|eventKey|advance|note" | Select-Object -First 60 LineNumber,Line
