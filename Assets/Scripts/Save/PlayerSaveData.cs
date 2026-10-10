using System;
using System.Collections.Generic;

/// <summary>
/// 本地存档数据（JsonUtility 序列化，JSON 字段名保持英文）。
/// 新增字段请给默认值：旧存档缺少该字段时会保留这里的默认值，保证向后兼容。
/// 注意：JsonUtility 对缺失的 int 会读成 0，需在 LocalSaveManager.NormalizeData 里把 characterLevel 钳到 1..50。
/// </summary>
[Serializable]
public class PlayerSaveData
{
    /// <summary>存档结构版本，以后做数据迁移时使用。</summary>
    public int saveVersion = 1;

    public int gold;
    public int gems;

    /// <summary>已解锁的最高章节（从 1 开始）。</summary>
    public int highestUnlockedChapter = 1;
    /// <summary>最高章节内已解锁的最高关卡（从 1 开始）。</summary>
    public int highestUnlockedStage = 1;
    /// <summary>最后一章最后一关也已通关。</summary>
    public bool allStagesCleared;

    /// <summary>背包道具堆叠（itemId + count）。</summary>
    public List<ItemStackEntry> itemStacks = new List<ItemStackEntry>();

    /// <summary>角色升级等级（1..50）。旧档缺失时 Normalize 为 1。</summary>
    public int characterLevel = 1;

    /// <summary>新手教程是否已全部完成。</summary>
    public bool tutorialCompleted;
    /// <summary>新手教程当前步骤（0 = 未开始），留给以后的教程模块使用。</summary>
    public int tutorialStep;
}

[Serializable]
public class ItemStackEntry
{
    public string itemId;
    public int count;
}