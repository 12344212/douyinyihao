using System;
using UnityEngine;

/// <summary>
/// 教程会话：主场景新手教程（main → upgrade）。无战斗教程。
/// 每次进入 Play 且尚未全部完成时，清掉流程进度并从 main 第 0 步重开。
/// 同一次会话内跨场景（进战斗再回 Main）不重复清进度。
/// </summary>
public static class TutorialSession
{
    /// <summary>最后一段：升级角色 + 装备长矛完成后标记整套新手完成。</summary>
    public const string FinalKey = "upgrade";

    public const string MainKey = "main";
    public const string UpgradeKey = "upgrade";

    private static readonly string[] FlowKeys = { MainKey, UpgradeKey };

    private static bool _checked;

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

    /// <summary>本会话只执行一次：未全完成则清流程进度，从 main 第 0 步重新开始。</summary>
    public static void EnsureSessionStart()
    {
        if (_checked)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
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