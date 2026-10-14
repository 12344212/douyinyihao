$p = "D:\UnitProject\26-10-8\Assets\Scripts\Battle\BattleDirector.cs"
$src = [IO.File]::ReadAllText($p).Replace("`r`n", "`n")

function Ins([ref]$s, $idx, $text) { $s.Value = $s.Value.Insert($idx, $text) }

# 1 BeginBattle after ResolveStage
$old = "        ResolveStage();`n        _characterPos = CellCenter(0, 0);"
$new = @"
        ResolveStage();
        TutorialBattleHooks.BeginBattle(BattleTuning.StageIndex(_chapter, _stage));
        if (TutorialBattleHooks.Active)
        {
            _waveCount = 0;
            _wavesDone = true;
            _waveTimer = 0f;
            if (_wood < 24) { _wood = 24; }
            if (_iron < 8) { _iron = 8; }
        }

        _characterPos = CellCenter(0, 0);
"@.Replace("`r`n","`n")
if ($src -notmatch "TutorialBattleHooks.BeginBattle") {
  if (-not $src.Contains($old)) { throw "ResolveStage hook missing" }
  $src = $src.Replace($old, $new)
}

# 2 TickWaves scripted return
if ($src -notmatch "TutorialBattleHooks.Scripted") {
  $tw = $src.IndexOf("private void TickWaves(float dt)")
  if ($tw -lt 0) { throw "TickWaves missing" }
  $brace = $src.IndexOf("{", $tw)
  $ins = "`n        if (TutorialBattleHooks.Scripted)`n        {`n            return;`n        }"
  $src = $src.Insert($brace + 1, $ins)
}

# 3 SelectSpear
$ss = $src.IndexOf("public void SelectSpear()")
if ($ss -ge 0 -and $src.IndexOf("EvWeaponSpear", $ss) -lt 0) {
  $set = $src.IndexOf("hud.SetWeapon(_weapon);", $ss)
  if ($set -ge 0 -and ($set - $ss) -lt 400) {
    $src = $src.Insert($set + "hud.SetWeapon(_weapon);".Length, "`n        TutorialBattleHooks.Raise(TutorialBattleHooks.EvWeaponSpear);")
  }
}

# 4 ChooseOfferSlot
$co = $src.IndexOf("public void ChooseOfferSlot(int slot)")
if ($co -ge 0 -and $src.Substring($co, [Math]::Min(500, $src.Length - $co)) -notmatch "EvUpgradePicked") {
  $ap = $src.IndexOf("ApplyUpgrade(_levelOffers[slot]);", $co)
  if ($ap -ge 0) {
    $src = $src.Insert($ap + "ApplyUpgrade(_levelOffers[slot]);".Length, "`n        TutorialBattleHooks.Raise(TutorialBattleHooks.EvUpgradePicked);")
  }
}

# 5 Net launched
if ($src -notmatch "EvNetLaunched") {
  $src = $src.Replace("LaunchPlayerNet(land);", "LaunchPlayerNet(land);`n                    TutorialBattleHooks.Raise(TutorialBattleHooks.EvNetLaunched);")
}

# 6 Player captured
if ($src -notmatch "OnPlayerCaptured") {
  $src = $src.Replace("net.DidCapture = true;", "net.DidCapture = true;`n                        if (net.Home == null && net.Held.Count > 0)`n                        {`n                            TutorialBattleHooks.OnPlayerCaptured();`n                        }")
}

# 7 DeliverNet hooks
if ($src -notmatch "OnPlayerDelivered") {
  $dn = $src.IndexOf("private void DeliverNet(NetShot net)")
  if ($dn -lt 0) { throw "DeliverNet missing" }
  $dead = $src.IndexOf("net.Dead = true;", $dn)
  $ins = @"

        bool fromCaptureTower = net.Home != null && net.Home.Building == BattleBuildingKind.Capture;
        bool fromPlayer = net.Home == null;
        int heldCount = net.Held.Count;
        bool heldMeat = false;
        for (int hi = 0; hi < net.Held.Count; hi++)
        {
            if (net.Held[hi] != null && net.Held[hi].Kind == BattleResourceKind.Meat)
            {
                heldMeat = true;
                break;
            }
        }
"@.Replace("`r`n","`n")
  $src = $src.Insert($dead + "net.Dead = true;".Length, $ins)
  $after = $src.IndexOf("private void CancelNet", $dn)
  if ($after -lt 0) { throw "CancelNet missing" }
  $hook = @"

        if (TutorialBattleHooks.Active && heldCount > 0)
        {
            if (fromPlayer)
            {
                TutorialBattleHooks.OnPlayerDelivered();
                if (heldMeat)
                {
                    TutorialBattleHooks.OnMeatCollected();
                }
            }
            else if (fromCaptureTower)
            {
                TutorialBattleHooks.OnCaptureHarvested();
            }
        }

"@.Replace("`r`n","`n")
  $src = $src.Insert($after, $hook)
}

# 8 KillEnemy
if ($src -notmatch "OnEnemyKilled") {
  $ke = $src.IndexOf("private void KillEnemy(EnemyNode enemy)")
  $rem = $src.IndexOf("_enemies.Remove(enemy);", $ke)
  if ($rem -lt 0) { throw "Remove enemy missing" }
  $src = $src.Insert($rem + "_enemies.Remove(enemy);".Length, "`n        TutorialBattleHooks.OnEnemyKilled();")
}

# 9 Plank built
if ($src -notmatch "EvPlankBuilt") {
  $src = $src.Replace("SpawnPlank(cell.x, cell.y);", "SpawnPlank(cell.x, cell.y);`n            TutorialBattleHooks.Raise(TutorialBattleHooks.EvPlankBuilt);")
}

# 10 Towers
if ($src -notmatch "EvCaptureBuilt") {
  $bt = $src.IndexOf("BuildTower(plank, towerKind);")
  if ($bt -ge 0) {
    $ins = @"
BuildTower(plank, towerKind);
            if (towerKind == BattleBuildingKind.Capture)
            {
                TutorialBattleHooks.Raise(TutorialBattleHooks.EvCaptureBuilt);
            }
            else if (towerKind == BattleBuildingKind.Attack)
            {
                TutorialBattleHooks.Raise(TutorialBattleHooks.EvAttackBuilt);
            }
"@.Replace("`r`n","`n")
    $src = $src.Remove($bt, "BuildTower(plank, towerKind);".Length).Insert($bt, $ins)
  }
}

# 11 Win/Lose
if ($src -notmatch "OnVictory\(\)") {
  $win = $src.IndexOf("private void Win()")
  $show = $src.IndexOf("ShowResult(true);", $win)
  if ($show -ge 0) { $src = $src.Insert($show + "ShowResult(true);".Length, "`n        TutorialBattleHooks.OnVictory();") }
}
if ($src -notmatch "OnDefeat") {
  $lose = $src.IndexOf("private void Lose()")
  $show = $src.IndexOf("ShowResult(false);", $lose)
  if ($show -ge 0) { $src = $src.Insert($show + "ShowResult(false);".Length, "`n        TutorialBattleHooks.OnDefeat();") }
}

$out = $src.Replace("`n", "`r`n")
[IO.File]::WriteAllText($p, $out)
Write-Host "OK $($out.Length)"
Select-String -Path $p -Pattern "TutorialBattleHooks" | ForEach-Object { "$($_.LineNumber):$($_.Line.Trim())" }