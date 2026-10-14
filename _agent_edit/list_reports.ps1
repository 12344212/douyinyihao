Get-ChildItem "D:\UnitProject\26-10-8" -Recurse -Include "*.ok","*report*","*battle_tutorial*" -File -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch '\\Library\\|\\Temp\\' } |
  Select-Object -ExpandProperty FullName
