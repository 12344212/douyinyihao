using UnityEngine;

/// <summary>
/// 战斗数值。地址 BattleTuningConfig（YooAsset AddressByFileName）。
/// 数值与 BattleTuning 一致，运行时只读这份通过 YooAsset 加载的配置。
/// </summary>
[CreateAssetMenu(fileName = "BattleTuningConfig", menuName = "Template/Battle Tuning")]
public class BattleTuningConfig : ScriptableObject
{
    public const string Address = "BattleTuningConfig";

    [SerializeField] private float cameraSize = 8f;
    [SerializeField] private float cellSize = 1.8f;
    [SerializeField] private float cellTouchFactor = 0.46f;
    [SerializeField] private float plankHp = 30f;
    [SerializeField] private float enemyHp = 20f;
    [SerializeField] private float enemyMoveSpeed = 0.8f;
    [SerializeField] private float enemyAttackDamage = 5f;
    [SerializeField] private float enemyAttackInterval = 1f;
    [SerializeField] private float spearDamage = 10f;
    [SerializeField] private float spearRadius = 0.7f;
    [SerializeField] private float spearSpeed = 12f;
    [SerializeField] private float towerDamage = 8f;
    [SerializeField] private float towerAttackInterval = 0.8f;
    [SerializeField] private float towerRange = 3f;
    [SerializeField] private float captureRange = 3f;
    [SerializeField] private float characterRange = 4f;
    [SerializeField] private float characterRangeStep = 1.5f;
    [SerializeField] private int netCapacity = 4;
    [SerializeField] private int netCapacityStep = 4;
    [SerializeField] private float captureCooldown = 2f;
    [SerializeField] private float captureRadius = 0.75f;
    [SerializeField] private float netSpeed = 9f;
    [SerializeField] private float reelHoldSeconds = 0.35f;
    [SerializeField] private float netDiameter = 1.5f;
    [SerializeField] private float reelStep = 1.2f;
    [SerializeField] private float reelHoldSpeed = 3f;
    [SerializeField] private float reelStepTier1 = 1.0f;
    [SerializeField] private float reelHoldSpeedTier1 = 2.4f;
    [SerializeField] private float reelStepTier2 = 0.8f;
    [SerializeField] private float reelHoldSpeedTier2 = 1.8f;
    [SerializeField] private float reelStepTier3 = 0.55f;
    [SerializeField] private float reelHoldSpeedTier3 = 1.2f;
    [SerializeField] private float reelStepTier4 = 0.35f;
    [SerializeField] private float reelHoldSpeedTier4 = 0.7f;
    [SerializeField] private float driftInterval = 1.2f;
    [SerializeField] private float driftSpeed = 2.4f;
    [SerializeField] private float crabInterval = 1.5f;
    [SerializeField] private float waveGap = 30f;
    [SerializeField] private int waveCap = 6;
    [SerializeField] private int xpPerResource = 1;
    [SerializeField] private int xpLevel1 = 5;
    [SerializeField] private int xpLevel2 = 8;
    [SerializeField] private int xpLevel3Base = 12;
    [SerializeField] private int xpStepAfterLevel3 = 4;

    [SerializeField] private Color waterColor = new Color(0.10f, 0.42f, 0.72f, 1f);
    [SerializeField] private Color woodPlankColor = new Color(0.62f, 0.42f, 0.22f, 1f);
    [SerializeField] private Color captureTowerColor = new Color(0.20f, 0.75f, 0.72f, 1f);
    [SerializeField] private Color attackTowerColor = new Color(0.75f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color towerSpearColor = new Color(0.95f, 0.45f, 0.20f, 1f);

    public float CameraSize => cameraSize;
    public float CellSize => cellSize;
    public float CellTouchFactor => cellTouchFactor;
    public float PlankHp => plankHp;
    public float EnemyHp => enemyHp;
    public float EnemyMoveSpeed => enemyMoveSpeed;
    public float EnemyAttackDamage => enemyAttackDamage;
    public float EnemyAttackInterval => enemyAttackInterval;
    public float SpearDamage => spearDamage;
    public float SpearRadius => spearRadius;
    public float SpearSpeed => spearSpeed;
    public float TowerDamage => towerDamage;
    public float TowerAttackInterval => towerAttackInterval;
    public float TowerRange => towerRange;
    public float CaptureRange => captureRange;
    public float CharacterRange => characterRange;
    public float CharacterRangeStep => characterRangeStep;
    public int NetCapacity => netCapacity;
    public int NetCapacityStep => netCapacityStep;
    public float CaptureCooldown => captureCooldown;
    public float CaptureRadius => captureRadius;
    public float NetSpeed => netSpeed;
    public float ReelHoldSeconds => reelHoldSeconds;
    public float NetDiameter => netDiameter;
    public float ReelStep => reelStep;
    public float ReelHoldSpeed => reelHoldSpeed;
    public float ReelStepTier1 => reelStepTier1;
    public float ReelHoldSpeedTier1 => reelHoldSpeedTier1;
    public float ReelStepTier2 => reelStepTier2;
    public float ReelHoldSpeedTier2 => reelHoldSpeedTier2;
    public float ReelStepTier3 => reelStepTier3;
    public float ReelHoldSpeedTier3 => reelHoldSpeedTier3;
    public float ReelStepTier4 => reelStepTier4;
    public float ReelHoldSpeedTier4 => reelHoldSpeedTier4;

    public int ReelLoadTier(int heldCount)
    {
        return BattleTuning.ReelLoadTier(heldCount);
    }

    public float ReelStepFor(int heldCount)
    {
        switch (ReelLoadTier(heldCount))
        {
            case 1: return reelStepTier1;
            case 2: return reelStepTier2;
            case 3: return reelStepTier3;
            case 4: return reelStepTier4;
            default: return reelStep;
        }
    }

    public float ReelHoldSpeedFor(int heldCount)
    {
        switch (ReelLoadTier(heldCount))
        {
            case 1: return reelHoldSpeedTier1;
            case 2: return reelHoldSpeedTier2;
            case 3: return reelHoldSpeedTier3;
            case 4: return reelHoldSpeedTier4;
            default: return reelHoldSpeed;
        }
    }
    public float DriftInterval => driftInterval;
    public float DriftSpeed => driftSpeed;
    public float CrabInterval => crabInterval;
    public float WaveGap => waveGap;
    public int WaveCap => waveCap;
    public int XpPerResource => xpPerResource;
    public Color WaterColor => waterColor;
    public Color WoodPlankColor => woodPlankColor;
    public Color CaptureTowerColor => captureTowerColor;
    public Color AttackTowerColor => attackTowerColor;
    public Color TowerSpearColor => towerSpearColor;

    public int StageIndex(int chapter, int stage)
    {
        return (chapter - 1) * StageDefs.StagesPerChapter + (stage - 1);
    }

    public int WaveCountFor(int stageIndex)
    {
        if (stageIndex < 0)
        {
            stageIndex = 0;
        }

        int count = 2 + stageIndex;
        if (count > waveCap)
        {
            count = waveCap;
        }

        return count;
    }

    public int XpToNext(int level)
    {
        if (level <= 1)
        {
            return xpLevel1;
        }

        if (level == 2)
        {
            return xpLevel2;
        }

        return xpLevel3Base + (level - 3) * xpStepAfterLevel3;
    }
}