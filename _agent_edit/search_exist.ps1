# Also check if there's already any tutorial pause attempt in ref or backups
Select-String -Path "D:\UnitProject\26-10-8\_agent_edit\ref_tutorial\TutorialManager.cs","D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs" -Pattern "BattleTime|timeScale|_battleHeld|HoldBattle|SyncBattle" -ErrorAction SilentlyContinue
