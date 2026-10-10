using UnityEngine;

/// <summary>
/// 战斗占位数值。标注「可调」的都是临时手感，不是正式配表。
/// </summary>
public static class BattleTuning
{
    // 可调：正交相机半高，木筏放在画面中心
    public const float CameraSize = 8f;

    // 可调：木板格子边长（世界单位）
    public const float CellSize = 1.8f;

    // 可调：离木板中心多近算踩上这块板
    public const float CellTouchFactor = 0.46f;

    // 可调
    public const float PlankHp = 30f;

    // 可调
    public const float EnemyHp = 20f;

    // 可调：螃蟹移速，偏慢
    public const float EnemyMoveSpeed = 0.8f;

    // 可调：踩住木板时的每次伤害
    public const float EnemyAttackDamage = 5f;

    // 可调
    public const float EnemyAttackInterval = 1f;

    // 可调
    public const float SpearDamage = 10f;

    // 可调：长矛落点半径内，打最近的一只螃蟹
    public const float SpearRadius = 0.7f;

    // 可调
    public const float SpearSpeed = 12f;

    // 可调
    public const float TowerDamage = 8f;

    // 可调
    public const float TowerAttackInterval = 0.8f;

    // 可调
    public const float TowerRange = 3f;

    // 可调
    public const float CaptureRange = 3f;

    public const float CharacterRange = 4f;

    public const float CharacterRangeStep = 1.5f;

    public const int NetCapacity = 4;

    public const int NetCapacityStep = 4;

    // 升级卡数值步进
    public const float HeroDamageStep = 3f;
    public const float NetDiameterStep = 0.25f;
    public const float NetSpeedMulStep = 0.2f;
    public const float CaptureRangeStep = 0.75f;
    public const int CaptureNetCapacity = 4;
    public const int CaptureCapacityStep = 4;
    public const float CaptureDiameterStep = 0.25f;
    public const float CaptureCooldownStep = 0.25f;
    public const float CaptureCooldownMin = 0.6f;
    public const float AttackRangeStep = 0.75f;
    public const float AttackDamageStep = 3f;

    public const float AttractorRange = 3f;
    public const float AttractorInterval = 5f;
    public const float AttractorRangeStep = 0.75f;
    public const float AttractorIntervalStep = 0.5f;
    public const float AttractorIntervalMin = 2f;
    public const float AttractorPullDuration = 0.35f;

    // 可调：捕获台两次撒网的间隔
    public const float CaptureCooldown = 2f;

    // 可调：网落点能罩住资源的半径
    public const float CaptureRadius = 0.75f;

    // 可调
    public const float NetSpeed = 18f;

    // 可调：按住超过这个时间也收网；短按松开同样收
    public const float ReelHoldSeconds = 0.35f;

    public const float NetDiameter = 1.5f;

    public const float ReelStep = 1.2f;

    public const float ReelHoldSpeed = 3f;

    public const float ReelStepTier1 = 1.0f;

    public const float ReelHoldSpeedTier1 = 2.4f;

    public const float ReelStepTier2 = 0.8f;

    public const float ReelHoldSpeedTier2 = 1.8f;

    public const float ReelStepTier3 = 0.55f;

    public const float ReelHoldSpeedTier3 = 1.2f;

    public const float ReelStepTier4 = 0.35f;

    public const float ReelHoldSpeedTier4 = 0.7f;

    public static int ReelLoadTier(int heldCount)
    {
        if (heldCount <= 0)
        {
            return 0;
        }

        if (heldCount <= 4)
        {
            return 1;
        }

        if (heldCount <= 8)
        {
            return 2;
        }

        if (heldCount <= 12)
        {
            return 3;
        }

        return 4;
    }

    public static float ReelStepFor(int heldCount)
    {
        switch (ReelLoadTier(heldCount))
        {
            case 1: return ReelStepTier1;
            case 2: return ReelStepTier2;
            case 3: return ReelStepTier3;
            case 4: return ReelStepTier4;
            default: return ReelStep;
        }
    }

    public static float ReelHoldSpeedFor(int heldCount)
    {
        switch (ReelLoadTier(heldCount))
        {
            case 1: return ReelHoldSpeedTier1;
            case 2: return ReelHoldSpeedTier2;
            case 3: return ReelHoldSpeedTier3;
            case 4: return ReelHoldSpeedTier4;
            default: return ReelHoldSpeed;
        }
    }

    // 可调
    public const float DriftInterval = 0.6f;

    // 可调：资源从右上往左下漂的速度
    public const float DriftSpeed = 2.4f;

    // 开战时水面预填的漂流木叶数量
    public const int DriftPrefillCount = 10;

    // 每次升级成功后额外刷出的漂流木叶数量（基础值）
    public const int DriftLevelUpBurstCount = 4;

    // 可调：同一波里螃蟹的出生间隔
    public const float CrabInterval = 1.5f;

    // 可调：两波之间的间隔
    public const float WaveGap = 30f;

    // 可调：波数上限
    public const int WaveCap = 6;

    // 可调：每收一个资源给的经验
    public const int XpPerResource = 1;

    public static readonly Color WaterColor = new Color(0.10f, 0.42f, 0.72f, 1f);
    public static readonly Color WoodPlankColor = new Color(0.62f, 0.42f, 0.22f, 1f);
    public static readonly Color WoodColor = new Color(0.55f, 0.32f, 0.12f, 1f);
    public static readonly Color LeafColor = new Color(0.20f, 0.72f, 0.28f, 1f);
    public static readonly Color MeatColor = new Color(0.95f, 0.45f, 0.58f, 1f);
    public static readonly Color CharacterColor = new Color(0.25f, 0.55f, 0.95f, 1f);
    public static readonly Color FaceColor = new Color(1f, 0.86f, 0.45f, 1f);
    public static readonly Color CrabColor = new Color(0.86f, 0.24f, 0.16f, 1f);
    public static readonly Color NetColor = new Color(0.35f, 0.82f, 0.95f, 0.92f);
    public static readonly Color SpearColor = new Color(0.95f, 0.82f, 0.25f, 1f);
    public static readonly Color TowerSpearColor = new Color(0.95f, 0.45f, 0.20f, 1f);
    public static readonly Color CaptureTowerColor = new Color(0.20f, 0.75f, 0.72f, 1f);
    public static readonly Color AttackTowerColor = new Color(0.75f, 0.22f, 0.22f, 1f);
    public static readonly Color AttractorTowerColor = new Color(0.75f, 0.35f, 0.95f, 1f);

    /// <summary>整条战役从 0 起算：1-1 = 0，1-2 = 1，2-1 = 10。</summary>
    public static int StageIndex(int chapter, int stage)
    {
        return (chapter - 1) * StageDefs.StagesPerChapter + (stage - 1);
    }

    /// <summary>可调：波数 = 2 + 关卡序号，最多 WaveCap。</summary>
    public static int WaveCountFor(int stageIndex)
    {
        if (stageIndex < 0)
        {
            stageIndex = 0;
        }

        int count = 2 + stageIndex;
        if (count > WaveCap)
        {
            count = WaveCap;
        }

        return count;
    }

    /// <summary>可调：5，然后 8，然后 12，之后每级 +4。</summary>
    public static int XpToNext(int level)
    {
        if (level <= 1)
        {
            return 5;
        }

        if (level == 2)
        {
            return 8;
        }

        return 12 + (level - 3) * 4;
    }
}

public enum BattleResourceKind
{
    Wood,
    Leaf,
    Meat
}

public enum BattleBuildingKind
{
    None,
    Capture,
    Attack,
    Attractor
}

public enum BattleWeapon
{
    Net,
    Spear
}

public enum BattleUpgradeKind
{
    Plank,
    PlaceCapture,
    PlaceAttack,
    HeroRange,
    HeroCapacity,
    HeroDamage,
    NetSize,
    NetSpeed,
    CaptureRange,
    CaptureCapacity,
    CaptureSize,
    CaptureSpeed,
    AttackRange,
    AttackDamage,
    PlaceAttractor,
    AttractorRange,
    AttractorFrequency
}
