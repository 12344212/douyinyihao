using System;
using UnityEngine;

/// <summary>
/// Tutorial session flow: main -> battle -> role.
/// Incomplete runs clear progress once per Play session and restart from main step 0.
/// Role complete = whole newbie tutorial done.
/// </summary>
public static class TutorialSession
{
    public const string FinalKey = "role";

    public const string MainKey = "main";
    public const string BattleKey = "battle";
    public const string RoleKey = "role";

    /// <summary>Legacy alias used by earlier main-scene wire; same as RoleKey.</summary>
    public const string UpgradeKey = RoleKey;

    private static readonly string[] FlowKeys = { MainKey, BattleKey, RoleKey };

    private static bool _checked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _checked = false;
    }

    public static bool AllDone
    {
        get { return LocalSaveManager.Instance != null && LocalSaveManager.Instance.IsTutorialDone(FinalKey); }
    }

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
        // Also clear legacy "upgrade" key if present from earlier wire.
        removed += data.tutorialProgress.RemoveAll(e => e != null && e.key == "upgrade");
        if (removed > 0)
        {
            save.Save();
        }

        Debug.Log("[TutorialSession] tutorial not finished last time -> restart from main step 0 (cleared " + removed + " entries)");
    }
}
