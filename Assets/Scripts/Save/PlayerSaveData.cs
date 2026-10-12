using System;
using System.Collections.Generic;

/// <summary>
/// 本地存档数据（JsonUtility 序列化，JSON 字段名保持英文）。
/// 新增字段请给默认值；缺失的 int 会被 JsonUtility 读成 0，需在 NormalizeData 里修正。
/// </summary>
[Serializable]
public class PlayerSaveData
{
    public int saveVersion = 1;

    public int gold;
    public int gems;

    public int highestUnlockedChapter = 1;
    public int highestUnlockedStage = 1;
    public bool allStagesCleared;

    public List<ItemStackEntry> itemStacks = new List<ItemStackEntry>();

    /// <summary>角色升级等级（1..50）。</summary>
    public int characterLevel = 1;

    /// <summary>商店累计成功购买的道具件数（用于进度条 0..5）。</summary>
    public int shopPurchaseCount;
    public bool shopChest1Claimed;
    public bool shopChest2Claimed;
    public bool shopChest3Claimed;

    /// <summary>TopUp daily free gold last claim local date yyyy-MM-dd.</summary>
    public string topUpDailyGoldClaimDate;

    public bool tutorialCompleted;
    public int tutorialStep;
}

[Serializable]
public class ItemStackEntry
{
    public string itemId;
    public int count;
}