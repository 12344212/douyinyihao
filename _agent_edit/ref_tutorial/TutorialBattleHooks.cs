using System;
using UnityEngine;

/// <summary>
/// 新手教程 ↔ 战斗逻辑的唯一接口（静态）。只在“教程战斗”（首次进入 1-1 且 battle 教程未完成）时生效，
/// 普通战斗下所有方法都原样返回，不改变任何数值。
/// - 教程波次：两波（第 1 波两只小怪，第 2 波 Boss），让“选怪 → 击杀 → 刷新 → 击杀 → 选 Buff → Boss”连贯
/// - 脚本阶段（Scripted，Boss 前）：开局骰子 5/6/6/6/6，刷新必出 6；攻击必杀当前目标
/// - 事件：BattleController 在关键操作后 Raise，TutorialManager 监听用来推进步骤
/// </summary>
public static class TutorialBattleHooks
{
    /// <summary>战斗教程的存档键。</summary>
    public const string BattleKey = "battle";

    public const string EvEnemySelected = "battle.enemySelected";
    public const string EvAttackStart = "battle.attackStart";
    public const string EvDiceRerolled = "battle.diceRerolled";

    /// <summary>freePlay 条件：教程战斗通关（Boss 被击败），由 BattleFlow.Finish 通知。</summary>
    public const string CondBossDefeated = "boss_defeated";
    /// <summary>freePlay 条件：教程战斗失败，由 BattleFlow.Finish 通知。</summary>
    public const string CondBattleLost = "battle_lost";

    /// <summary>本场是否为教程战斗（BattleController.Awake 时判定，整场不变）。</summary>
    public static bool Active { get; private set; }

    /// <summary>脚本阶段：固定骰子 + 必杀。教程走到 Boss 前的释放步骤时关闭。</summary>
    public static bool Scripted { get; private set; }

    /// <summary>战斗事件（仅教程战斗时派发）。</summary>
    public static event Action<string> Raised;

    // 教程波次：沿用 1-1 第 2、3 波的怪物数值；Boss 生命与 StagePlan.Stage1BossHp 一致（10，保证必胜）
    static readonly WaveSpawn[][] Waves =
    {
        new[]
        {
            new WaveSpawn { kind = EnemyKind.A, hp = 34, attack = 5, boss = false },
            new WaveSpawn { kind = EnemyKind.B, hp = 20, attack = 4, boss = false }
        },
        new[]
        {
            new WaveSpawn { kind = EnemyKind.C, hp = StagePlan.Stage1BossHp, attack = 7, boss = true }
        }
    };

    /// <summary>进入战斗时调用：只有首次 1-1（关卡下标 0）且 battle 教程未完成才进入教程模式。</summary>
    public static void BeginBattle(int stageIndex)
    {
        Active = ShouldBeActive(stageIndex);
        Scripted = Active;
        if (Active)
        {
            Debug.Log("[TutorialBattleHooks] tutorial battle active (stage 1-1)");
        }
    }

    /// <summary>纯判定（不改状态）：该关是否会进入教程战斗。TutorialManager.Awake 用它提前打开全屏拦截。</summary>
    public static bool ShouldBeActive(int stageIndex)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        // 直接从战斗场景开局（编辑器调试）时也保证先做一次会话重置
        TutorialSession.EnsureSessionStart();
        return save != null && stageIndex == 0 && !save.IsTutorialDone(BattleKey);
    }

    /// <summary>结束脚本阶段（之后骰子随机、伤害正常）。</summary>
    public static void EndScripted()
    {
        Scripted = false;
    }

    public static int WaveCount(int stageIndex)
    {
        return Active ? Waves.Length : StagePlan.WaveCount(stageIndex);
    }

    public static WaveSpawn[] WaveAt(int stageIndex, int waveIndex)
    {
        if (!Active)
        {
            return StagePlan.WaveAt(stageIndex, waveIndex);
        }

        WaveSpawn[] src = Waves[Mathf.Clamp(waveIndex, 0, Waves.Length - 1)];
        WaveSpawn[] scaled = new WaveSpawn[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            scaled[i] = src[i];
            scaled[i].hp = StagePlan.ScaleBase(src[i].hp);
            scaled[i].attack = StagePlan.ScaleBase(src[i].attack);
        }

        return scaled;
    }

    /// <summary>骰子点数：脚本阶段开局第 1 颗 5、其余 6；刷新必出 6。否则返回传入的随机值。</summary>
    public static int ScriptedRoll(int dieIndex, bool reroll, int randomValue)
    {
        if (!Scripted)
        {
            return randomValue;
        }

        if (reroll)
        {
            return 6;
        }

        return dieIndex == 0 ? 5 : 6;
    }

    /// <summary>脚本阶段必杀：伤害至少等于目标剩余生命。</summary>
    public static int AdjustDamage(int damage, int targetHp)
    {
        return Scripted ? Mathf.Max(damage, targetHp) : damage;
    }

    public static void Raise(string key)
    {
        if (!Active)
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
