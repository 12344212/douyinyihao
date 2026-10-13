using System.Globalization;
using UnityEngine;

/// <summary>
/// 角色升级数值：等级 1..50；三维属性各自公式；升级消耗只花金币。
/// Attack: 10+(L-1)*2（整数，满级 108）
/// Range: 2.0+(L-1)*0.1（一位小数，满级 6.9）
/// Net Speed: 1.0+(L-1)*0.2 /S（一位小数，满级 10.8）
/// Hp: 50+(L-1)*20（整数，满级 1030）
/// Cost(L→L+1) = 100+(L-1)*40
/// </summary>
public static class CharacterUpgradeDefs
{
    public const int MinLevel = 1;
    public const int MaxLevel = 50;

    public const int AttackBase = 10;
    public const int AttackPerLevel = 2;

    public const float RangeBase = 2.0f;
    public const float RangePerLevel = 0.1f;

    public const float NetSpeedBase = 1.0f;
    public const float NetSpeedPerLevel = 0.2f;

    /// <summary>Hp: base 50, +20 per upgrade level (aligned with characterLevel like other attrs).</summary>
    public const int HpBase = 50;
    public const int HpPerLevel = 20;

    public const int BaseCost = 100;
    public const int CostPerLevel = 40;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int ClampLevel(int level)
    {
        return Mathf.Clamp(level, MinLevel, MaxLevel);
    }

    public static int AttackAtLevel(int level)
    {
        level = ClampLevel(level);
        return AttackBase + (level - 1) * AttackPerLevel;
    }

    public static int AttackMax => AttackAtLevel(MaxLevel);

    public static float RangeAtLevel(int level)
    {
        level = ClampLevel(level);
        return RangeBase + (level - 1) * RangePerLevel;
    }

    public static float RangeMax => RangeAtLevel(MaxLevel);

    public static float NetSpeedAtLevel(int level)
    {
        level = ClampLevel(level);
        return NetSpeedBase + (level - 1) * NetSpeedPerLevel;
    }

    public static float NetSpeedMax => NetSpeedAtLevel(MaxLevel);

    public static int HpAtLevel(int level)
    {
        level = ClampLevel(level);
        return HpBase + (level - 1) * HpPerLevel;
    }

    public static int HpMax => HpAtLevel(MaxLevel);

    public static int CostToNext(int currentLevel)
    {
        currentLevel = ClampLevel(currentLevel);
        if (currentLevel >= MaxLevel)
        {
            return 0;
        }

        return BaseCost + (currentLevel - 1) * CostPerLevel;
    }

    public static float AttackFill(int level)
    {
        return Fill(AttackAtLevel(level), AttackMax);
    }

    public static float RangeFill(int level)
    {
        return Fill(RangeAtLevel(level), RangeMax);
    }

    public static float NetSpeedFill(int level)
    {
        return Fill(NetSpeedAtLevel(level), NetSpeedMax);
    }

    public static float HpFill(int level)
    {
        return Fill(HpAtLevel(level), HpMax);
    }

    public static string FormatAttack(int level)
    {
        return AttackAtLevel(level).ToString(Invariant) + "/" + AttackMax.ToString(Invariant);
    }

    public static string FormatRange(int level)
    {
        return RangeAtLevel(level).ToString("0.0", Invariant) + "/" + RangeMax.ToString("0.0", Invariant);
    }

    public static string FormatNetSpeed(int level)
    {
        return NetSpeedAtLevel(level).ToString("0.0", Invariant) + "S/" + NetSpeedMax.ToString("0.0", Invariant) + "S";
    }

    public static string FormatHp(int level)
    {
        return HpAtLevel(level).ToString(Invariant) + "/" + HpMax.ToString(Invariant);
    }

    /// <summary>当前网速展示，如 1.0/S（不含满级分母时也可拼 Value）。</summary>
    public static string FormatNetSpeedCurrent(int level)
    {
        return NetSpeedAtLevel(level).ToString("0.0", Invariant) + "S";
    }

    public static string FormatNetSpeedMax()
    {
        return NetSpeedMax.ToString("0.0", Invariant) + "S";
    }

    private static float Fill(float current, float max)
    {
        if (max <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(current / max);
    }
}