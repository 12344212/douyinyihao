using UnityEngine;

/// <summary>
/// 战斗内暂停。商店和暂停页各持有一次，都关掉才恢复。
/// </summary>
public static class BattleTime
{
    private static int _holds;

    public static bool IsHeld => _holds > 0;

    public static void Hold()
    {
        _holds++;
        Apply();
    }

    public static void Release()
    {
        if (_holds > 0)
        {
            _holds--;
        }

        Apply();
    }

    public static void Reset()
    {
        _holds = 0;
        Time.timeScale = 1f;
    }

    private static void Apply()
    {
        Time.timeScale = _holds > 0 ? 0f : 1f;
    }
}
