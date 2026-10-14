using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 本地存档管理器：JSON 文件保存在 Application.persistentDataPath/player_save.json。
/// - 挂在 LoadScene 的 LocalSaveRuntime 节点上，DontDestroyOnLoad 单例；Awake 时读取，数据变化后立即写盘。
/// - 安全写盘：先写 .tmp 临时文件，再替换正式文件，并保留一份 .bak；正式文件损坏时自动尝试从 .bak 恢复。
/// - 同时管理货币（金币 / 钻石）、关卡进度、背包道具数量与新手教程标记；变化时触发静态事件通知 UI。
/// </summary>
[DisallowMultipleComponent]
public class LocalSaveManager : MonoBehaviour
{
    public const string FileName = "player_save.json";
    public const int CurrentSaveVersion = 1;

    /// <summary>新存档的初始金币 / 钻石（沿用参考项目的新手数值 200 / 50）。</summary>
    public const int StartingGold = 200;
    public const int StartingGems = 50;

    /// <summary>关卡结构：3 章 × 每章 10 关（章节、关卡编号都从 1 开始）。</summary>
    public const int ChapterCount = 3;
    public const int StagesPerChapter = 10;

    /// <summary>金币 / 钻石变化后触发，供 UI 刷新。</summary>
    public static event Action OnCurrencyChanged;

    /// <summary>背包道具数量变化后触发，供背包 / 商店 UI 刷新。</summary>
    public static event Action OnItemsChanged;

    /// <summary>藏品拥有 / 领奖状态变化后触发，供 Collection 面板刷新。</summary>
    public static event Action OnCollectiblesChanged;

    /// <summary>Equipped gear changed; UpgradePanel / battle UI refresh.</summary>
    public static event Action OnEquipmentChanged;

    public static LocalSaveManager Instance { get; private set; }

    [SerializeField] private string fileName = FileName;

    private PlayerSaveData _data;
    private string _filePath;

    /// <summary>原始存档数据（只读用途；修改请走本类的 API，否则不会写盘也不会触发事件）。</summary>
    public PlayerSaveData Data => _data;
    public string FilePath => _filePath;
    public bool IsLoaded { get; private set; }

    public int Gold => _data != null ? _data.gold : 0;
    public int Gems => _data != null ? _data.gems : 0;
    public int HighestUnlockedChapter => _data != null ? _data.highestUnlockedChapter : 1;
    public int HighestUnlockedStage => _data != null ? _data.highestUnlockedStage : 1;
    public bool AllStagesCleared => _data != null && _data.allStagesCleared;
    public bool TutorialCompleted => _data != null && _data.tutorialCompleted;
    public int TutorialStep => _data != null ? _data.tutorialStep : 0;
    public int CharacterLevel => _data != null ? CharacterUpgradeDefs.ClampLevel(_data.characterLevel) : CharacterUpgradeDefs.MinLevel;

    private string TempPath => _filePath + ".tmp";
    private string BackupPath => _filePath + ".bak";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadOrCreate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        // 每次改动已立即写盘，这里只是切后台时的兜底
        if (paused && IsLoaded)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (IsLoaded)
        {
            Save();
        }
    }

    // ---------- 读写 ----------

    /// <summary>
    /// 读取存档：正式文件 → .bak 备份 → 新建默认存档（初始金币 / 钻石）。
    /// </summary>
    public void LoadOrCreate()
    {
        _filePath = Path.Combine(Application.persistentDataPath, fileName);
        bool needSave = false;
        PlayerSaveData loaded = null;

        try
        {
            EnsureDirectory();
            loaded = TryReadFile(_filePath);
            if (loaded == null && File.Exists(BackupPath))
            {
                loaded = TryReadFile(BackupPath);
                if (loaded != null)
                {
                    Debug.LogWarning("[LocalSaveManager] Main save missing or corrupted, restored from backup.");
                    needSave = true;
                }
            }

            if (loaded == null && File.Exists(_filePath))
            {
                // 正式文件和备份都无法解析：先留一份 .corrupt 再重建，避免直接覆盖丢失数据
                File.Copy(_filePath, _filePath + ".corrupt", true);
                Debug.LogError("[LocalSaveManager] Save file corrupted, a copy was kept as .corrupt and a new save was created.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] Load failed: {e.Message}");
        }

        if (loaded == null)
        {
            loaded = CreateDefault();
            needSave = true;
        }

        _data = loaded;
        if (NormalizeData())
        {
            needSave = true;
        }

        IsLoaded = true;
        if (needSave)
        {
            Save();
        }

        Debug.Log($"[LocalSaveManager] Loaded: {_filePath}");
        NotifyCurrencyChanged();
        NotifyItemsChanged();
        NotifyCollectiblesChanged();
        NotifyEquipmentChanged();
    }

    /// <summary>立即写盘（临时文件 + 替换）。成功返回 true。</summary>
    public bool Save()
    {
        if (_data == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(_filePath))
        {
            _filePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        try
        {
            EnsureDirectory();
            _data.saveVersion = CurrentSaveVersion;
            string json = JsonUtility.ToJson(_data, true);

            using (FileStream fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(true);
            }

            ReplaceFile(TempPath, _filePath, BackupPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] Save failed: {e.Message}");
            try
            {
                if (File.Exists(TempPath))
                {
                    File.Delete(TempPath);
                }
            }
            catch (Exception)
            {
                // 忽略临时文件清理失败
            }

            return false;
        }
    }

    /// <summary>重置为全新存档（调试用，Inspector 组件右键菜单可调用）。</summary>
    [ContextMenu("Reset Save To Default")]
    public void ResetToDefault()
    {
        _data = CreateDefault();
        NormalizeData();
        Save();
        NotifyCurrencyChanged();
        NotifyItemsChanged();
        NotifyCollectiblesChanged();
        NotifyEquipmentChanged();
    }

    // ---------- 货币 ----------

    public int GetCurrency(CurrencyType type)
    {
        return type == CurrencyType.Gems ? Gems : Gold;
    }

    public bool CanAfford(CurrencyType type, int amount)
    {
        return amount <= 0 || GetCurrency(type) >= amount;
    }

    public void AddGold(int amount)
    {
        AddCurrency(CurrencyType.Gold, amount);
    }

    public void AddGems(int amount)
    {
        AddCurrency(CurrencyType.Gems, amount);
    }

    /// <summary>增加货币；amount 小于等于 0 时忽略（扣除请用 TrySpend）。</summary>
    public void AddCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        EnsureData();
        long sum = (long)GetCurrency(type) + amount;
        SetCurrencyValue(type, (int)Math.Min(sum, int.MaxValue));
        Save();
        NotifyCurrencyChanged();
    }

    public bool TrySpendGold(int amount)
    {
        return TrySpend(CurrencyType.Gold, amount);
    }

    public bool TrySpendGems(int amount)
    {
        return TrySpend(CurrencyType.Gems, amount);
    }

    /// <summary>扣除货币；余额不足返回 false 且不扣。amount 为 0 视为成功，负数视为失败。</summary>
    public bool TrySpend(CurrencyType type, int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning($"[LocalSaveManager] TrySpend with negative amount: {amount}");
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        EnsureData();
        int current = GetCurrency(type);
        if (current < amount)
        {
            return false;
        }

        SetCurrencyValue(type, current - amount);
        Save();
        NotifyCurrencyChanged();
        return true;
    }

    private void SetCurrencyValue(CurrencyType type, int value)
    {
        value = Mathf.Max(0, value);
        if (type == CurrencyType.Gems)
        {
            _data.gems = value;
        }
        else
        {
            _data.gold = value;
        }
    }

    // ---------- 关卡进度（章节 / 关卡从 1 开始） ----------

    public static bool IsValidStage(int chapter, int stage)
    {
        return chapter >= 1 && chapter <= ChapterCount && stage >= 1 && stage <= StagesPerChapter;
    }

    public bool IsStageUnlocked(int chapter, int stage)
    {
        if (!IsValidStage(chapter, stage))
        {
            return false;
        }

        return CompareStage(chapter, stage, HighestUnlockedChapter, HighestUnlockedStage) <= 0;
    }

    public bool IsStageCleared(int chapter, int stage)
    {
        if (!IsValidStage(chapter, stage))
        {
            return false;
        }

        int cmp = CompareStage(chapter, stage, HighestUnlockedChapter, HighestUnlockedStage);
        return cmp < 0 || (cmp == 0 && AllStagesCleared);
    }

    /// <summary>
    /// 战斗胜利后调用：通关当前最高解锁关卡时解锁下一关（跨章自动进入下一章），进度只前进不回退。
    /// 进度有推进时返回 true。
    /// </summary>
    public bool RecordStageCleared(int chapter, int stage)
    {
        if (!IsValidStage(chapter, stage))
        {
            return false;
        }

        EnsureData();
        if (_data.allStagesCleared || chapter != _data.highestUnlockedChapter || stage != _data.highestUnlockedStage)
        {
            return false;
        }

        AdvanceHighestStage();
        Save();
        return true;
    }

    private void AdvanceHighestStage()
    {
        if (_data.highestUnlockedStage < StagesPerChapter)
        {
            _data.highestUnlockedStage++;
        }
        else if (_data.highestUnlockedChapter < ChapterCount)
        {
            _data.highestUnlockedChapter++;
            _data.highestUnlockedStage = 1;
        }
        else
        {
            _data.allStagesCleared = true;
        }
    }

    private static int CompareStage(int chapterA, int stageA, int chapterB, int stageB)
    {
        if (chapterA != chapterB)
        {
            return chapterA < chapterB ? -1 : 1;
        }

        return stageA == stageB ? 0 : (stageA < stageB ? -1 : 1);
    }

    // ---------- 新手教程（教程本身暂未实现，只保存标记） ----------

    public void SetTutorialStep(int step)
    {
        EnsureData();
        step = Mathf.Max(0, step);
        if (_data.tutorialCompleted || _data.tutorialStep == step)
        {
            return;
        }

        _data.tutorialStep = step;
        Save();
    }

    public void CompleteTutorial()
    {
        EnsureData();
        if (_data.tutorialCompleted)
        {
            return;
        }

        _data.tutorialCompleted = true;
        Save();
    }

    // ---------- Keyed tutorial progress (main / upgrade) ----------

    public bool IsTutorialDone(string key)
    {
        TutorialProgressEntry e = FindTutorial(key, false);
        return e != null && e.done;
    }

    public int GetTutorialStep(string key)
    {
        TutorialProgressEntry e = FindTutorial(key, false);
        return e != null ? Mathf.Max(0, e.step) : 0;
    }

    public void SetTutorialStep(string key, int step)
    {
        TutorialProgressEntry e = FindTutorial(key, true);
        if (e == null || e.done)
        {
            return;
        }

        e.step = Mathf.Max(0, step);
        // Mirror legacy single-key fields for the active main flow when advancing main.
        if (key == TutorialSession.MainKey)
        {
            _data.tutorialStep = e.step;
        }

        Save();
    }

    public void CompleteTutorial(string key)
    {
        TutorialProgressEntry e = FindTutorial(key, true);
        if (e == null || e.done)
        {
            return;
        }

        e.done = true;
        if (key == TutorialSession.FinalKey)
        {
            _data.tutorialCompleted = true;
        }

        Save();
    }

    private TutorialProgressEntry FindTutorial(string key, bool create)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        EnsureData();
        if (_data.tutorialProgress == null)
        {
            _data.tutorialProgress = new List<TutorialProgressEntry>();
        }

        for (int i = 0; i < _data.tutorialProgress.Count; i++)
        {
            TutorialProgressEntry e = _data.tutorialProgress[i];
            if (e != null && e.key == key)
            {
                return e;
            }
        }

        if (!create)
        {
            return null;
        }

        TutorialProgressEntry created = new TutorialProgressEntry { key = key };
        _data.tutorialProgress.Add(created);
        return created;
    }


    // ---------- 角色升级 ----------

    public int GetCharacterLevel()
    {
        return CharacterLevel;
    }

    /// <summary>设置角色等级（钳制到 1..50）并写盘。</summary>
    public void SetCharacterLevel(int level)
    {
        EnsureData();
        int clamped = CharacterUpgradeDefs.ClampLevel(level);
        if (_data.characterLevel == clamped)
        {
            return;
        }

        _data.characterLevel = clamped;
        Save();
    }

    // ---------- 商店进度 / 宝箱 ----------

    public const int ShopProgressMax = 5;
    public static readonly int[] ShopChestThresholds = { 1, 3, 5 };
    public static readonly int[] ShopChestGoldRewards = { 50, 150, 300 };

    public int GetShopPurchaseCount()
    {
        EnsureData();
        return Mathf.Max(0, _data.shopPurchaseCount);
    }

    /// <summary>成功购买后累加件数（写入存档）。</summary>
    public void AddShopPurchaseCount(int qty)
    {
        if (qty <= 0)
        {
            return;
        }

        EnsureData();
        long sum = (long)_data.shopPurchaseCount + qty;
        _data.shopPurchaseCount = (int)Math.Min(sum, int.MaxValue);
        Save();
    }

    public bool IsShopChestClaimed(int chestIndex)
    {
        EnsureData();
        if (chestIndex == 0) return _data.shopChest1Claimed;
        if (chestIndex == 1) return _data.shopChest2Claimed;
        if (chestIndex == 2) return _data.shopChest3Claimed;
        return true;
    }

    public bool IsShopChestReady(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= ShopChestThresholds.Length)
        {
            return false;
        }

        return GetShopPurchaseCount() >= ShopChestThresholds[chestIndex] && !IsShopChestClaimed(chestIndex);
    }

    /// <summary>领取宝箱金币；未解锁或已领返回 false。</summary>
    public bool TryClaimShopChest(int chestIndex, out int goldReward)
    {
        goldReward = 0;
        if (chestIndex < 0 || chestIndex >= ShopChestGoldRewards.Length)
        {
            return false;
        }

        EnsureData();
        if (!IsShopChestReady(chestIndex))
        {
            return false;
        }

        goldReward = ShopChestGoldRewards[chestIndex];
        if (chestIndex == 0) _data.shopChest1Claimed = true;
        else if (chestIndex == 1) _data.shopChest2Claimed = true;
        else _data.shopChest3Claimed = true;

        // AddGold 会再 Save；此处先标记再加金币
        Save();
        AddGold(goldReward);
        return true;
    }

    // ---------- TopUp daily free gold ----------

    public const int TopUpDailyGoldReward = 200;

    public static string TodayLocalDateString()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    public bool IsTopUpDailyGoldClaimedToday()
    {
        EnsureData();
        if (string.IsNullOrEmpty(_data.topUpDailyGoldClaimDate))
        {
            return false;
        }

        return _data.topUpDailyGoldClaimDate == TodayLocalDateString();
    }

    /// <summary>Claim 200 gold once per local calendar day. Returns false if already claimed today.</summary>
    public bool TryClaimTopUpDailyGold(out int goldReward)
    {
        goldReward = 0;
        EnsureData();
        if (IsTopUpDailyGoldClaimedToday())
        {
            return false;
        }

        _data.topUpDailyGoldClaimDate = TodayLocalDateString();
        goldReward = TopUpDailyGoldReward;
        Save();
        AddGold(goldReward);
        return true;
    }
    // ---------- 藏品 CP1–CP15 ----------

    public const int CollectibleCount = 15;
    public const int CollectibleClickGold = CollectionDefs.ClickGoldReward;

    public bool IsCollectibleOwned(int index)
    {
        EnsureData();
        EnsureCollectibleArrays();
        if (!CollectionDefs.IsValidIndex(index))
        {
            return false;
        }

        return _data.collectibleOwned[index];
    }

    public bool IsCollectibleRewardClaimed(int index)
    {
        EnsureData();
        EnsureCollectibleArrays();
        if (!CollectionDefs.IsValidIndex(index))
        {
            return false;
        }

        return _data.collectibleRewardClaimed[index];
    }

    public int GetOwnedCollectibleCount()
    {
        EnsureData();
        EnsureCollectibleArrays();
        int count = 0;
        for (int i = 0; i < CollectibleCount; i++)
        {
            if (_data.collectibleOwned[i])
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 设置藏品拥有状态（战斗捕捉 / 调试用）。owned=false 时同时清除领奖标记。
    /// 有变化时写盘并触发 OnCollectiblesChanged，返回 true。
    /// </summary>
    public bool SetCollectibleOwned(int index, bool owned)
    {
        if (!CollectionDefs.IsValidIndex(index))
        {
            return false;
        }

        EnsureData();
        EnsureCollectibleArrays();
        bool changed = false;
        if (_data.collectibleOwned[index] != owned)
        {
            _data.collectibleOwned[index] = owned;
            changed = true;
        }

        if (!owned && _data.collectibleRewardClaimed[index])
        {
            _data.collectibleRewardClaimed[index] = false;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        Save();
        NotifyCollectiblesChanged();
        return true;
    }

    /// <summary>
    /// 拥有且未领奖时领取一次 500 金币；成功返回 true 并写盘。
    /// </summary>
    public bool TryClaimCollectibleReward(int index, out int goldReward)
    {
        goldReward = 0;
        if (!CollectionDefs.IsValidIndex(index))
        {
            return false;
        }

        EnsureData();
        EnsureCollectibleArrays();
        if (!_data.collectibleOwned[index] || _data.collectibleRewardClaimed[index])
        {
            return false;
        }

        _data.collectibleRewardClaimed[index] = true;
        goldReward = CollectibleClickGold;
        Save();
        NotifyCollectiblesChanged();
        AddGold(goldReward);
        return true;
    }

    [ContextMenu("Debug Own All Collectibles (CP1-15)")]
    public void DebugOwnAllCollectibles()
    {
        EnsureData();
        EnsureCollectibleArrays();
        for (int i = 0; i < CollectibleCount; i++)
        {
            _data.collectibleOwned[i] = true;
        }

        Save();
        NotifyCollectiblesChanged();
        Debug.Log("[LocalSaveManager] Debug: owned all collectibles.");
    }

    [ContextMenu("Debug Clear All Collectibles")]
    public void DebugClearAllCollectibles()
    {
        EnsureData();
        EnsureCollectibleArrays();
        for (int i = 0; i < CollectibleCount; i++)
        {
            _data.collectibleOwned[i] = false;
            _data.collectibleRewardClaimed[i] = false;
        }

        Save();
        NotifyCollectiblesChanged();
        Debug.Log("[LocalSaveManager] Debug: cleared all collectibles.");
    }

    private void EnsureCollectibleArrays()
    {
        if (_data.collectibleOwned == null || _data.collectibleOwned.Length != CollectibleCount)
        {
            bool[] next = new bool[CollectibleCount];
            if (_data.collectibleOwned != null)
            {
                int copy = Mathf.Min(_data.collectibleOwned.Length, CollectibleCount);
                for (int i = 0; i < copy; i++)
                {
                    next[i] = _data.collectibleOwned[i];
                }
            }

            _data.collectibleOwned = next;
        }

        if (_data.collectibleRewardClaimed == null || _data.collectibleRewardClaimed.Length != CollectibleCount)
        {
            bool[] next = new bool[CollectibleCount];
            if (_data.collectibleRewardClaimed != null)
            {
                int copy = Mathf.Min(_data.collectibleRewardClaimed.Length, CollectibleCount);
                for (int i = 0; i < copy; i++)
                {
                    next[i] = _data.collectibleRewardClaimed[i];
                }
            }

            _data.collectibleRewardClaimed = next;
        }

        // 未拥有则领奖标记无效
        for (int i = 0; i < CollectibleCount; i++)
        {
            if (!_data.collectibleOwned[i] && _data.collectibleRewardClaimed[i])
            {
                _data.collectibleRewardClaimed[i] = false;
            }
        }
    }

    private static void NotifyCollectiblesChanged()
    {
        OnCollectiblesChanged?.Invoke();
    }

    // ---------- 背包道具 ----------

    public int GetItemCount(string itemId)
    {
        EnsureData();
        ItemStackEntry stack = FindStack(itemId);
        return stack != null ? Mathf.Max(0, stack.count) : 0;
    }

    public bool HasItem(string itemId, int count = 1)
    {
        return GetItemCount(itemId) >= Mathf.Max(1, count);
    }

    /// <summary>增加道具；只接受 GameItemDefs 中定义过的 id，count 必须大于 0。</summary>
    public bool AddItem(string itemId, int count)
    {
        if (count <= 0)
        {
            return false;
        }

        if (!GameItemDefs.Exists(itemId))
        {
            Debug.LogWarning($"[LocalSaveManager] AddItem: unknown item id '{itemId}'");
            return false;
        }

        EnsureData();
        ItemStackEntry stack = FindStack(itemId);
        if (stack == null)
        {
            _data.itemStacks.Add(new ItemStackEntry { itemId = itemId, count = count });
        }
        else
        {
            long sum = (long)Mathf.Max(0, stack.count) + count;
            stack.count = (int)Math.Min(sum, int.MaxValue);
        }

        Save();
        NotifyItemsChanged();
        return true;
    }

    /// <summary>消耗道具；数量不足返回 false 且不扣。count 为 0 视为成功，负数视为失败。</summary>
    public bool TryConsumeItem(string itemId, int count)
    {
        if (count < 0)
        {
            return false;
        }

        if (count == 0)
        {
            return true;
        }

        EnsureData();
        ItemStackEntry stack = FindStack(itemId);
        if (stack == null || stack.count < count)
        {
            return false;
        }

        stack.count -= count;
        if (stack.count <= 0)
        {
            _data.itemStacks.Remove(stack);
        }

        Save();
        if (NormalizeEquippedSlots())
        {
            NotifyEquipmentChanged();
        }

        NotifyItemsChanged();
        return true;
    }


    // ---------- Equipment (equipped item ids by EquipItemType) ----------

    /// <summary>Equipped gear changed; UpgradePanel refresh.</summary>
    private static void NotifyEquipmentChanged()
    {
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>UI slot index 1..4 maps to Shoes / Armor / Spear / Hood.</summary>
    public static EquipItemType EquipTypeForSlotIndex(int slotIndex)
    {
        switch (slotIndex)
        {
            case 1: return EquipItemType.Shoes;
            case 2: return EquipItemType.Armor;
            case 3: return EquipItemType.Spear;
            case 4: return EquipItemType.Hood;
            default: return EquipItemType.None;
        }
    }

    public string GetEquippedItemId(EquipItemType type)
    {
        EnsureData();
        switch (type)
        {
            case EquipItemType.Spear: return _data.equippedSpearId ?? string.Empty;
            case EquipItemType.Hood: return _data.equippedHoodId ?? string.Empty;
            case EquipItemType.Shoes: return _data.equippedShoesId ?? string.Empty;
            case EquipItemType.Armor: return _data.equippedArmorId ?? string.Empty;
            default: return string.Empty;
        }
    }

    public string GetEquippedItemIdForSlot(int slotIndex)
    {
        return GetEquippedItemId(EquipTypeForSlotIndex(slotIndex));
    }

    public bool IsItemEquipped(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        return GetEquippedItemId(EquipItemType.Spear) == itemId
            || GetEquippedItemId(EquipItemType.Hood) == itemId
            || GetEquippedItemId(EquipItemType.Shoes) == itemId
            || GetEquippedItemId(EquipItemType.Armor) == itemId;
    }

    /// <summary>Equip owned item into its type slot. Replaces previous of same type. Returns false if invalid.</summary>
    public bool TryEquipItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        if (def == null || def.equipType == EquipItemType.None)
        {
            Debug.LogWarning("[LocalSaveManager] TryEquipItem: not equipment '" + itemId + "'");
            return false;
        }

        EnsureData();
        if (GetItemCount(itemId) <= 0)
        {
            return false;
        }

        string current = GetEquippedItemId(def.equipType);
        if (current == itemId)
        {
            return true;
        }

        SetEquippedField(def.equipType, itemId);
        Save();
        NotifyEquipmentChanged();
        return true;
    }

    /// <summary>Clear equipped item for a type slot.</summary>
    public bool UnequipType(EquipItemType type)
    {
        if (type == EquipItemType.None)
        {
            return false;
        }

        EnsureData();
        string current = GetEquippedItemId(type);
        if (string.IsNullOrEmpty(current))
        {
            return true;
        }

        SetEquippedField(type, string.Empty);
        Save();
        NotifyEquipmentChanged();
        return true;
    }

    /// <summary>Unequip by item id if currently equipped.</summary>
    public bool UnequipItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        if (def == null || def.equipType == EquipItemType.None)
        {
            return false;
        }

        if (GetEquippedItemId(def.equipType) != itemId)
        {
            return false;
        }

        return UnequipType(def.equipType);
    }

    private void SetEquippedField(EquipItemType type, string itemId)
    {
        string value = itemId ?? string.Empty;
        switch (type)
        {
            case EquipItemType.Spear: _data.equippedSpearId = value; break;
            case EquipItemType.Hood: _data.equippedHoodId = value; break;
            case EquipItemType.Shoes: _data.equippedShoesId = value; break;
            case EquipItemType.Armor: _data.equippedArmorId = value; break;
        }
    }

    /// <summary>Drop equipped ids that are missing from inventory or wrong type. Returns true if changed.</summary>
    private bool NormalizeEquippedSlots()
    {
        bool changed = false;
        changed |= NormalizeOneEquipped(EquipItemType.Spear, ref _data.equippedSpearId);
        changed |= NormalizeOneEquipped(EquipItemType.Hood, ref _data.equippedHoodId);
        changed |= NormalizeOneEquipped(EquipItemType.Shoes, ref _data.equippedShoesId);
        changed |= NormalizeOneEquipped(EquipItemType.Armor, ref _data.equippedArmorId);
        return changed;
    }

    private bool NormalizeOneEquipped(EquipItemType expectedType, ref string equippedId)
    {
        if (string.IsNullOrEmpty(equippedId))
        {
            if (equippedId != null && equippedId.Length == 0)
            {
                return false;
            }

            if (equippedId == null)
            {
                equippedId = string.Empty;
                return true;
            }

            return false;
        }

        if (GetItemCount(equippedId) <= 0)
        {
            equippedId = string.Empty;
            return true;
        }

        GameItemDefs.Def def = GameItemDefs.Get(equippedId);
        if (def == null || def.equipType != expectedType)
        {
            // Config may not be bound yet at first load; keep id if GameItemDefs empty.
            if (GameItemDefs.Current == null)
            {
                return false;
            }

            equippedId = string.Empty;
            return true;
        }

        return false;
    }

    /// <summary>返回所有数量大于 0 的道具（副本，修改不会影响存档）。</summary>
    public List<ItemStackEntry> GetAllItems()
    {
        EnsureData();
        List<ItemStackEntry> result = new List<ItemStackEntry>(_data.itemStacks.Count);
        for (int i = 0; i < _data.itemStacks.Count; i++)
        {
            ItemStackEntry stack = _data.itemStacks[i];
            if (stack != null && stack.count > 0)
            {
                result.Add(new ItemStackEntry { itemId = stack.itemId, count = stack.count });
            }
        }

        return result;
    }

    private ItemStackEntry FindStack(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || _data == null || _data.itemStacks == null)
        {
            return null;
        }

        for (int i = 0; i < _data.itemStacks.Count; i++)
        {
            ItemStackEntry stack = _data.itemStacks[i];
            if (stack != null && stack.itemId == itemId)
            {
                return stack;
            }
        }

        return null;
    }

    // ---------- 内部 ----------

    private static void NotifyCurrencyChanged()
    {
        OnCurrencyChanged?.Invoke();
    }

    private static void NotifyItemsChanged()
    {
        OnItemsChanged?.Invoke();
    }

    private void EnsureData()
    {
        if (_data == null)
        {
            LoadOrCreate();
        }
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(Application.persistentDataPath))
        {
            Directory.CreateDirectory(Application.persistentDataPath);
        }
    }

    private static PlayerSaveData TryReadFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonUtility.FromJson<PlayerSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalSaveManager] Failed to parse {path}: {e.Message}");
            return null;
        }
    }

    private static void ReplaceFile(string tempPath, string targetPath, string backupPath)
    {
        if (!File.Exists(targetPath))
        {
            File.Move(tempPath, targetPath);
            return;
        }

        try
        {
            File.Replace(tempPath, targetPath, backupPath, true);
        }
        catch (Exception)
        {
            // 部分平台 / 文件系统不支持 File.Replace：退化为 备份 → 删除 → 移动
            File.Copy(targetPath, backupPath, true);
            File.Delete(targetPath);
            File.Move(tempPath, targetPath);
        }
    }

    /// <summary>修正空列表、非法数值、重复道具等；有修改时返回 true。</summary>
    private bool NormalizeData()
    {
        bool changed = false;

        int ownedLen = _data.collectibleOwned != null ? _data.collectibleOwned.Length : 0;
        int claimedLen = _data.collectibleRewardClaimed != null ? _data.collectibleRewardClaimed.Length : 0;
        if (ownedLen != CollectibleCount || claimedLen != CollectibleCount)
        {
            changed = true;
        }

        EnsureCollectibleArrays();

        if (_data.itemStacks == null)
        {
            _data.itemStacks = new List<ItemStackEntry>();
            changed = true;
        }

        if (_data.gold < 0) { _data.gold = 0; changed = true; }
        if (_data.gems < 0) { _data.gems = 0; changed = true; }
        if (_data.tutorialStep < 0) { _data.tutorialStep = 0; changed = true; }
        if (_data.tutorialProgress == null) { _data.tutorialProgress = new List<TutorialProgressEntry>(); changed = true; }

        // 旧档缺少 characterLevel 时 JsonUtility 读成 0 → 升到 1；并钳到 MaxLevel
        if (_data.shopPurchaseCount < 0) { _data.shopPurchaseCount = 0; changed = true; }

        int charLevel = CharacterUpgradeDefs.ClampLevel(_data.characterLevel <= 0 ? CharacterUpgradeDefs.MinLevel : _data.characterLevel);
        if (charLevel != _data.characterLevel)
        {
            _data.characterLevel = charLevel;
            changed = true;
        }

        int chapter = Mathf.Clamp(_data.highestUnlockedChapter, 1, ChapterCount);
        int stage = Mathf.Clamp(_data.highestUnlockedStage, 1, StagesPerChapter);
        if (chapter != _data.highestUnlockedChapter || stage != _data.highestUnlockedStage)
        {
            _data.highestUnlockedChapter = chapter;
            _data.highestUnlockedStage = stage;
            changed = true;
        }

        // 以后增加章节 / 关卡时，原先“全部通关”的存档自动解锁新增的下一关
        if (_data.allStagesCleared && (chapter != ChapterCount || stage != StagesPerChapter))
        {
            _data.allStagesCleared = false;
            AdvanceHighestStage();
            changed = true;
        }

        // 清理无效堆叠并合并重复 id（未知 id 保留，避免删掉以后版本的道具）
        Dictionary<string, ItemStackEntry> seen = new Dictionary<string, ItemStackEntry>(StringComparer.Ordinal);
        for (int i = _data.itemStacks.Count - 1; i >= 0; i--)
        {
            ItemStackEntry stack = _data.itemStacks[i];
            if (stack == null || string.IsNullOrEmpty(stack.itemId) || stack.count <= 0)
            {
                _data.itemStacks.RemoveAt(i);
                changed = true;
                continue;
            }

            // Removed catalog item: strip leftover heal potions from older player saves.
            if (stack.itemId == GameItemDefs.HealPotionId)
            {
                _data.itemStacks.RemoveAt(i);
                changed = true;
                continue;
            }

            ItemStackEntry first;
            if (seen.TryGetValue(stack.itemId, out first))
            {
                first.count = (int)Math.Min((long)first.count + stack.count, int.MaxValue);
                _data.itemStacks.RemoveAt(i);
                changed = true;
            }
            else
            {
                seen.Add(stack.itemId, stack);
            }
        }
        if (NormalizeEquippedSlots())
        {
            changed = true;
        }



        return changed;
    }

    private static PlayerSaveData CreateDefault()
    {
        return new PlayerSaveData
        {
            saveVersion = CurrentSaveVersion,
            gold = StartingGold,
            gems = StartingGems,
            highestUnlockedChapter = 1,
            highestUnlockedStage = 1,
            allStagesCleared = false,
            itemStacks = new List<ItemStackEntry>(),
            characterLevel = CharacterUpgradeDefs.MinLevel,
            tutorialCompleted = false,
            tutorialStep = 0,
            tutorialProgress = new List<TutorialProgressEntry>(),
            collectibleOwned = new bool[CollectibleCount],
            collectibleRewardClaimed = new bool[CollectibleCount]
        };
    }
}
