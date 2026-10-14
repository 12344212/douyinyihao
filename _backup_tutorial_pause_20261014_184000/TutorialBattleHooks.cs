using System;
using UnityEngine;

/// <summary>
/// Newbie tutorial <-> raft battle bridge. Active only on first stage 1-1 (stageIndex 0)
/// while battle tutorial is not done. Normal battles are untouched.
/// Scripted mode suppresses roster waves until EndScripted; tutorial drives guide spawns.
/// </summary>
public static class TutorialBattleHooks
{
    public const string BattleKey = TutorialSession.BattleKey;

    public const string EvNetLaunched = "battle.netLaunched";
    public const string EvResourceCaptured = "battle.resourceCaptured";
    public const string EvResourceDelivered = "battle.resourceDelivered";
    public const string EvPlankBuilt = "battle.plankBuilt";
    public const string EvCaptureBuilt = "battle.captureBuilt";
    public const string EvAttackBuilt = "battle.attackBuilt";
    public const string EvCaptureHarvested = "battle.captureHarvested";
    public const string EvLevelUpOpened = "battle.levelUpOpened";
    public const string EvUpgradePicked = "battle.upgradePicked";
    public const string EvWeaponSpear = "battle.weaponSpear";
    public const string EvEnemyKilled = "battle.enemyKilled";
    public const string EvMeatCollected = "battle.meatCollected";
    public const string EvVictory = "battle.victory";

    public const string CondVictory = "battle_victory";
    public const string CondBattleLost = "battle_lost";

    public static bool Active { get; private set; }
    public static bool Scripted { get; private set; }

    public static event Action<string> Raised;

    public static int KillCount { get; private set; }
    public static bool PlayerCaptured { get; private set; }
    public static bool PlayerDelivered { get; private set; }
    public static bool CaptureHarvested { get; private set; }
    public static bool GuideResourceSpawned { get; private set; }
    public static bool GuideEnemy1Spawned { get; private set; }
    public static bool GuideEnemy2Spawned { get; private set; }
    public static bool VictoryForced { get; private set; }

    public static void BeginBattle(int stageIndex)
    {
        Active = ShouldBeActive(stageIndex);
        Scripted = Active;
        KillCount = 0;
        PlayerCaptured = false;
        PlayerDelivered = false;
        CaptureHarvested = false;
        GuideResourceSpawned = false;
        GuideEnemy1Spawned = false;
        GuideEnemy2Spawned = false;
        VictoryForced = false;
        if (Active)
        {
            Debug.Log("[TutorialBattleHooks] tutorial battle active (stage 1-1)");
        }
    }

    public static bool ShouldBeActive(int stageIndex)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        TutorialSession.EnsureSessionStart();
        return save != null && stageIndex == 0 && !save.IsTutorialDone(BattleKey);
    }

    public static void EndScripted()
    {
        Scripted = false;
    }

    public static void MarkGuideResourceSpawned()
    {
        GuideResourceSpawned = true;
    }

    public static void MarkGuideEnemy1Spawned()
    {
        GuideEnemy1Spawned = true;
    }

    public static void MarkGuideEnemy2Spawned()
    {
        GuideEnemy2Spawned = true;
    }

    public static void MarkVictoryForced()
    {
        VictoryForced = true;
    }

    public static void OnPlayerCaptured()
    {
        if (!Active)
        {
            return;
        }

        PlayerCaptured = true;
        Raise(EvResourceCaptured);
    }

    public static void OnPlayerDelivered()
    {
        if (!Active)
        {
            return;
        }

        PlayerDelivered = true;
        Raise(EvResourceDelivered);
        TutorialManager.NotifyCondition(EvResourceDelivered);
    }

    public static void OnCaptureHarvested()
    {
        if (!Active)
        {
            return;
        }

        CaptureHarvested = true;
        Raise(EvCaptureHarvested);
        TutorialManager.NotifyCondition(EvCaptureHarvested);
    }

    public static void OnEnemyKilled()
    {
        if (!Active)
        {
            return;
        }

        KillCount++;
        Raise(EvEnemyKilled);
        TutorialManager.NotifyCondition(EvEnemyKilled);
        if (KillCount >= 2)
        {
            TutorialManager.NotifyCondition(CondVictory);
        }
    }

    public static void OnMeatCollected()
    {
        if (!Active)
        {
            return;
        }

        Raise(EvMeatCollected);
        TutorialManager.NotifyCondition(EvMeatCollected);
    }

    public static void OnVictory()
    {
        if (!Active)
        {
            return;
        }

        Raise(EvVictory);
        TutorialManager.NotifyCondition(CondVictory);
        TutorialManager.NotifyCondition(EvVictory);
    }

    public static void OnDefeat()
    {
        if (!Active)
        {
            return;
        }

        TutorialManager.NotifyCondition(CondBattleLost);
    }

    public static void Raise(string key)
    {
        if (!Active || string.IsNullOrEmpty(key))
        {
            return;
        }

        Action<string> handler = Raised;
        if (handler != null)
        {
            handler(key);
        }
    }
}
