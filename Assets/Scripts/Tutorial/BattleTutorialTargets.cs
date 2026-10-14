using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleScene tutorial targets / conditions for raft combat (not dice).
/// World: battle.resource.inRange, battle.meat, battle.enemy.first
/// UI: battle.ui.* (shop, weapons, HUD, level card, home)
/// Conditions may run side-effect setup (spawn guide resource / enemy / force level-up).
/// </summary>
public class BattleTutorialTargets : TutorialTargetProvider
{
    [SerializeField] private BattleDirector director;
    [SerializeField] private BattleBuildShop buildShop;
    [SerializeField] private BattleHudView hud;
    [SerializeField] private BattleResultPanel resultPanel;

    [Header("UI refs (wired by editor)")]
    [SerializeField] private RectTransform shopOpenButton;
    [SerializeField] private RectTransform plankBuyButton;
    [SerializeField] private RectTransform captureBuyButton;
    [SerializeField] private RectTransform attackBuyButton;
    [SerializeField] private RectTransform spearButton;
    [SerializeField] private RectTransform netButton;
    [SerializeField] private RectTransform hpRoot;
    [SerializeField] private RectTransform woodText;
    [SerializeField] private RectTransform leafText;
    [SerializeField] private RectTransform xpLabel;
    [SerializeField] private RectTransform levelCard0;
    [SerializeField] private RectTransform winHomeButton;

    public override Transform Resolve(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        switch (key)
        {
            case "battle.resource.inRange":
                return director != null ? director.TutorialFindResourceInRange() : null;
            case "battle.net.player":
                return director != null ? director.TutorialFindPlayerNet() : null;
            case "battle.meat":
                return director != null ? director.TutorialFindMeat() : null;
            case "battle.enemy.first":
                return director != null ? director.TutorialFindFirstEnemy() : null;
            case "battle.build.ghost":
                return director != null ? director.TutorialFindBuildGhost() : null;
            case "battle.build.emptyPlank":
                return director != null ? director.TutorialFindEmptyPlank() : null;
            case "battle.resource.harvest":
                return director != null ? director.TutorialFindHarvestResource() : null;
            case "battle.ui.shopOpen":
                return ActiveUi(shopOpenButton);
            case "battle.ui.plankBuy":
                EnsureShopOpen();
                return ActiveUi(plankBuyButton);
            case "battle.ui.captureBuy":
                EnsureShopOpen();
                return ActiveUi(captureBuyButton);
            case "battle.ui.attackBuy":
                EnsureShopOpen();
                return ActiveUi(attackBuyButton);
            case "battle.ui.spear":
                return ActiveUi(spearButton);
            case "battle.ui.net":
                return ActiveUi(netButton);
            case "battle.ui.hp":
                return ActiveUi(hpRoot);
            case "battle.ui.wood":
                return ActiveUi(woodText);
            case "battle.ui.iron":
                return ActiveUi(leafText);
            case "battle.ui.xp":
                return ActiveUi(xpLabel);
            case "battle.ui.levelCard0":
                return ActiveUi(levelCard0);
            case "battle.ui.home":
                return ActiveUi(winHomeButton);
            default:
                return null;
        }
    }

    public override bool CheckCondition(string key)
    {
        if (string.IsNullOrEmpty(key) || director == null)
        {
            return true;
        }

        switch (key)
        {
            case "battle.ready":
                return director.TutorialReady;
            case "battle.guideResourceReady":
                director.TutorialEnsureGuideResource();
                return director.TutorialFindResourceInRange() != null;
            case "battle.resourceCaptured":
                return TutorialBattleHooks.PlayerCaptured;
            case "battle.resourceDelivered":
                return TutorialBattleHooks.PlayerDelivered;
            case "battle.economyReady":
                director.TutorialPrepareEconomy();
                return true;
            case "battle.shopOpen":
                return buildShop != null && buildShop.gameObject.activeInHierarchy;
            case "battle.harvestReady":
                director.TutorialEnsureHarvestResource();
                return director.TutorialFindHarvestResource() != null;
            case "battle.hasCaptureTower":
                return director.TutorialHasBuilding(BattleBuildingKind.Capture);
            case "battle.hasAttackTower":
                return director.TutorialHasBuilding(BattleBuildingKind.Attack);
            case "battle.captureHarvested":
                return TutorialBattleHooks.CaptureHarvested;
            case "battle.forceLevelUp":
                director.TutorialForceLevelUp();
                return director.TutorialIsLevelUpOpen;
            case "battle.levelUpOpen":
                return director.TutorialIsLevelUpOpen;
            case "battle.spawnEnemy1":
                director.TutorialSpawnEnemyNearPlayer();
                return director.TutorialFindFirstEnemy() != null;
            case "battle.spawnEnemy2":
                director.TutorialSpawnEnemyNearTower();
                return director.TutorialAliveEnemyCount() > 0;
            case "battle.enemyAlive":
                return director.TutorialAliveEnemyCount() > 0;
            case "battle.enemyDead":
                return TutorialBattleHooks.KillCount >= 1;
            case "battle.enemy2Dead":
                return TutorialBattleHooks.KillCount >= 2;
            case "battle.readyForMeat":
                director.TutorialSelectNet();
                return director.TutorialFindMeat() != null;
            case "battle.meatPresent":
                return director.TutorialFindMeat() != null;
            case "battle.forceVictory":
                director.TutorialRequestVictory();
                return director.TutorialIsVictory;
            case "battle.victory":
                return director.TutorialIsVictory;
            case "battle.resultOpen":
                return resultPanel != null && resultPanel.gameObject.activeInHierarchy;
            default:
                return true;
        }
    }

    private void EnsureShopOpen()
    {
        if (buildShop == null)
        {
            return;
        }

        GameObject panel = buildShop.gameObject;
        if (panel != null && !panel.activeSelf)
        {
            buildShop.Toggle();
        }
    }

    private static Transform ActiveUi(RectTransform rt)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy)
        {
            return null;
        }

        return rt;
    }
}
