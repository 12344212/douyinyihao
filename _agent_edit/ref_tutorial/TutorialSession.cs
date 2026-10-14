using System;
using UnityEngine;

/// <summary>
/// 教程会话规则：整套新手教程（main → battle → role）没有全部完成时，每次启动游戏都从头开始。
/// 本次启动第一次用到教程时（第一个 TutorialManager / 教程战斗判定）清掉 main、battle、role 三段的进度；
/// 同一次会话内进度照常跨场景推进。role 完成 = 整套完成，之后永不重置、永不再触发。
/// </summary>
public static class TutorialSession
{
    /// <summary>最后一段；它完成才算整套教程完成。</summary>
    public const string FinalKey = "role";

    private static readonly string[] FlowKeys = { "main", TutorialBattleHooks.BattleKey, FinalKey };

    private static bool _checked;

    // 关闭域重载（Enter Play Mode Options）时静态字段不会自动清零，这里手动复位
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _checked = false;
    }

    /// <summary>整套教程是否已完成。</summary>
    public static bool AllDone
    {
        get { return LocalSaveManager.Instance != null && LocalSaveManager.Instance.IsTutorialDone(FinalKey); }
    }

    /// <summary>本次启动只执行一次：未全部完成则清空三段进度（从 main 第 0 步重新开始）。</summary>
    public static void EnsureSessionStart()
    {
        if (_checked)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            // 存档还没就绪：下次调用再判定
            return;
        }

        _checked = true;
        if (save.IsTutorialDone(FinalKey))
        {
            return;
        }

        PlayerSaveData data = save.Data;
        if (data == null || data.tutorialProgress == null)
        {
            return;
        }

        int removed = data.tutorialProgress.RemoveAll(e => e != null && Array.IndexOf(FlowKeys, e.key) >= 0);
        if (removed > 0)
        {
            save.Save();
        }

        Debug.Log("[TutorialSession] tutorial not finished last time -> restart from main step 0 (cleared " + removed + " entries)");
    }
}
