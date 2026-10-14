$ErrorActionPreference = "Stop"
$scenePath = "Assets\Scenes\BattleScene.unity"
$raw = [System.IO.File]::ReadAllText((Resolve-Path $scenePath))

$manager = @"
--- !u!114 &2069278282
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2069278279}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a9a7c8b4dc29d374e89fd0368152f544, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  tutorialKey: battle
  overlay: {fileID: 2069278281}
  targetProvider: {fileID: 2069278283}
  steps:
  - note: B1 click resource
    target: {fileID: 0}
    targetKey: battle.resource.inRange
    text: Tap a floating resource inside your range to throw the net.
    portrait: {fileID: 0}
    advance: 2
    eventKey: battle.resourceCaptured
    waitFor: battle.guideResourceReady
    showDelay: 0.4
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B2 wait captured
    target: {fileID: 0}
    targetKey: 
    text: Nice! The net caught it.
    portrait: {fileID: 0}
    advance: 1
    eventKey: 
    waitFor: battle.resourceCaptured
    showDelay: 0.1
    skipOnReplay: 1
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B3 reel in
    target: {fileID: 0}
    targetKey: 
    text: Hold to reel the net back and collect your loot.
    portrait: {fileID: 0}
    advance: 0
    eventKey: 
    waitFor: 
    showDelay: 0
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 1
    conditionKey: battle.resourceDelivered
    holePadding: 16
  - note: B4 build plank
    target: {fileID: 0}
    targetKey: battle.ui.plankBuy
    text: Open the shop and build a plank to expand your raft.
    portrait: {fileID: 0}
    advance: 2
    eventKey: battle.plankBuilt
    waitFor: battle.economyReady
    showDelay: 0.2
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B5 build capture
    target: {fileID: 0}
    targetKey: battle.ui.captureBuy
    text: Build a Capture Tower on an empty plank.
    portrait: {fileID: 0}
    advance: 2
    eventKey: battle.captureBuilt
    waitFor: 
    showDelay: 0.2
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B6 wait harvest
    target: {fileID: 0}
    targetKey: 
    text: Wait for the Capture Tower to haul in resources.
    portrait: {fileID: 0}
    advance: 0
    eventKey: 
    waitFor: 
    showDelay: 0
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 1
    conditionKey: battle.captureHarvested
    holePadding: 16
  - note: B7 pick upgrade
    target: {fileID: 0}
    targetKey: battle.ui.levelCard0
    text: Level up! Pick one upgrade.
    portrait: {fileID: 0}
    advance: 2
    eventKey: battle.upgradePicked
    waitFor: battle.forceLevelUp
    showDelay: 0.2
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B8 showcase HP
    target: {fileID: 0}
    targetKey: battle.ui.hp
    text: This is your HP. Keep it above zero!
    portrait: {fileID: 0}
    advance: 1
    eventKey: 
    waitFor: 
    showDelay: 0.15
    skipOnReplay: 1
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B8b showcase resources
    target: {fileID: 0}
    targetKey: battle.ui.wood
    text: Wood and iron (leaf) are spent in the build shop.
    portrait: {fileID: 0}
    advance: 1
    eventKey: 
    waitFor: 
    showDelay: 0.1
    skipOnReplay: 1
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B8c showcase XP
    target: {fileID: 0}
    targetKey: battle.ui.xp
    text: Gather resources to earn XP and level up.
    portrait: {fileID: 0}
    advance: 1
    eventKey: 
    waitFor: 
    showDelay: 0.1
    skipOnReplay: 1
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B9 switch spear
    target: {fileID: 0}
    targetKey: battle.ui.spear
    text: An enemy is near! Switch to your spear.
    portrait: {fileID: 0}
    advance: 2
    eventKey: battle.weaponSpear
    waitFor: battle.spawnEnemy1
    showDelay: 0.2
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B10 kill enemy
    target: {fileID: 0}
    targetKey: battle.enemy.first
    text: Throw your spear to defeat the enemy.
    portrait: {fileID: 0}
    advance: 0
    eventKey: 
    waitFor: 
    showDelay: 0
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 1
    conditionKey: battle.enemyKilled
    holePadding: 16
  - note: B11 collect meat
    target: {fileID: 0}
    targetKey: battle.meat
    text: Collect the meat drop with your net.
    portrait: {fileID: 0}
    advance: 0
    eventKey: 
    waitFor: battle.readyForMeat
    showDelay: 0
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 1
    conditionKey: battle.meatCollected
    holePadding: 16
  - note: B12 build attack
    target: {fileID: 0}
    targetKey: battle.ui.attackBuy
    text: Build an Attack Tower to auto-fight.
    portrait: {fileID: 0}
    advance: 2
    eventKey: battle.attackBuilt
    waitFor: battle.economyReady
    showDelay: 0.2
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B13 spawn near tower
    target: {fileID: 0}
    targetKey: 
    text: Another enemy approaches your tower. Let it fall!
    portrait: {fileID: 0}
    advance: 0
    eventKey: 
    waitFor: battle.spawnEnemy2
    showDelay: 0
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 1
    conditionKey: battle.enemyKilled
    holePadding: 16
  - note: B14 victory
    target: {fileID: 0}
    targetKey: 
    text: Victory! Check your rewards.
    portrait: {fileID: 0}
    advance: 1
    eventKey: 
    waitFor: battle.forceVictory
    showDelay: 0.3
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  - note: B15 return home
    target: {fileID: 0}
    targetKey: battle.ui.home
    text: Tap Home to return to the main menu.
    portrait: {fileID: 0}
    advance: 0
    eventKey: 
    waitFor: battle.resultOpen
    showDelay: 0.2
    skipOnReplay: 0
    skipWhen: 
    skipIfTargetGone: 0
    freePlay: 0
    conditionKey: 
    holePadding: 16
  startDelay: 0.6
  autoStart: 1
  requiredDoneKeys:
  - main
  battleTutorialOnly: 1
  resumeFromSavedStep: 0
  hideWhileRunning:
  - {fileID: 523659413}
  releaseAtStep: -1
  keepBlockingAfterFinish: 1
  keepBlockingTimeout: 5
  wiredStepsVersion: 2
"@

$targets = @"
--- !u!114 &2069278283
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2069278279}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 0008c5b6b51a81941ab79bb392548e42, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  director: {fileID: 2100000104}
  buildShop: {fileID: 603074420}
  hud: {fileID: 223845}
  resultPanel: {fileID: 2105688042}
  shopOpenButton: {fileID: 1987754148}
  plankBuyButton: {fileID: 556563682}
  captureBuyButton: {fileID: 823039534}
  attackBuyButton: {fileID: 1941412211}
  spearButton: {fileID: 225541}
  netButton: {fileID: 225441}
  hpRoot: {fileID: 1858475691}
  woodText: {fileID: 226889}
  leafText: {fileID: 2073566096}
  xpLabel: {fileID: 225341}
  levelCard0: {fileID: 226041}
  winHomeButton: {fileID: 319757775}
"@

# Force LF
$manager = ($manager -replace "`r`n","`n").TrimEnd() + "`n"
$targets = ($targets -replace "`r`n","`n").TrimEnd() + "`n"
$raw = $raw -replace "`r`n","`n"

$goOld = "  m_Component:`n  - component: {fileID: 2069278280}`n  - component: {fileID: 2069278281}`n  - component: {fileID: 2069278282}`n  m_Layer: 5`n  m_Name: TutorialPanel"
$goNew = "  m_Component:`n  - component: {fileID: 2069278280}`n  - component: {fileID: 2069278281}`n  - component: {fileID: 2069278282}`n  - component: {fileID: 2069278283}`n  m_Layer: 5`n  m_Name: TutorialPanel"

if (-not $raw.Contains($goOld)) { throw "GO list not found" }
$raw = $raw.Replace($goOld, $goNew)
Write-Host "GO ok"

$mgrRe = [regex]::new('--- !u!114 &2069278282\nMonoBehaviour:.*?wiredStepsVersion: \d+\n', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $mgrRe.IsMatch($raw)) { throw "manager not found" }
$raw = $mgrRe.Replace($raw, $manager, 1)
Write-Host "manager ok"

$marker = "wiredStepsVersion: 2`n--- "
if (-not $raw.Contains($marker)) { throw "insert marker missing" }
if ($raw -notmatch 'guid: 0008c5b6b51a81941ab79bb392548e42') {
  $raw = $raw.Replace($marker, ("wiredStepsVersion: 2`n" + $targets + "--- "))
  Write-Host "targets inserted"
} else {
  Write-Host "targets already present"
}

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText((Resolve-Path $scenePath), $raw, $utf8)
Write-Host "saved size=$($raw.Length)"
foreach ($c in @('tutorialKey: battle','battleTutorialOnly: 1','note: B1 click resource','guid: 0008c5b6','targetProvider: {fileID: 2069278283}','wiredStepsVersion: 2')) {
  Write-Host "$(if($raw.Contains($c)){'OK'}else{'MISSING'}) $c"
}
Write-Host "role leftover: $($raw.Contains('tutorialKey: role'))"
