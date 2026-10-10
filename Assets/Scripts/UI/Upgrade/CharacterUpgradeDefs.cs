using UnityEngine;

/// <summary>
/// 角色升级数值：等级 1..50；三维属性同公式；升级消耗只花金币。
/// Stat(L) = 10 + (L-1)*2；Cost(L→L+1) = 100 + (L-1)*40。
/// </summary>
public static class CharacterUpgradeDefs
{
    public const int MinLevel = 1;
    public const int MaxLevel = 50;
    public const int BaseStat = 10;
    public const int StatPerLevel = 2;
    public const int BaseCost = 100;
    public const int CostPerLevel = 40;

    /// <summary>等级 L 时 Attack/Range/Defense 共用值。</summary>
    public static int StatAtLevel(int level)
    {
        level = ClampLevel(level);
        return BaseStat + (level - 1) * StatPerLevel;
    }

    /// <summary>满级（50）时的属性上限，用于进度条分母。</summary>
    public static int MaxStat => StatAtLevel(MaxLevel);

    /// <summary>从当前等级 L 升到 L+1 的金币消耗；已满级返回 0。</summary>
    public static int CostToNext(int currentLevel)
    {
        currentLevel = ClampLevel(currentLevel);
        if (currentLevel >= MaxLevel)
        {
            return 0;
        }

        return BaseCost + (currentLevel - 1) * CostPerLevel;
    }

    public static int ClampLevel(int level)
    {
        return Mathf.Clamp(level, MinLevel, MaxLevel);
    }

    public static float FillAmount(int level)
    {
        int max = MaxStat;
        if (max <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)StatAtLevel(level) / max);
    }
}