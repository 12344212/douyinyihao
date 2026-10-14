$p = "D:\UnitProject\26-10-8\Assets\Scripts\Battle\BattleDirector.cs"
$lines = Get-Content $p
for ($i = 294; $i -le 470; $i++) {
  "{0,4}|{1}" -f ($i + 1), $lines[$i]
}
