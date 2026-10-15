using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reward popup on MainScene CanvasReward (above feature panels, below top bar).
/// Display only — caller grants gold / gems / items via LocalSaveManager before Show.
/// Not gated by MainUIController single-panel rule; can overlay other panels.
/// Reward slots reuse bag BagPrefab (BagItemSlot): icon, count; equipment/gems SetNativeSize.
/// </summary>
public class GetPanelController : MonoBehaviour
{
    /// <summary>One reward. iconLocation is a YooAsset address; empty uses a color placeholder.</summary>
    [System.Serializable]
    public struct Reward
    {
        /// <summary>Optional item id. When set, icon / equipment come from GameItemDefs.</summary>
        public string itemId;
        public string iconLocation;
        public string displayName;
        public int count;
        /// <summary>When true, treat as currency; empty icon uses ZJM-JB / ZJM-ZS via YooAsset.</summary>
        public bool useCurrency;
        public CurrencyType currency;
    }

    public static GetPanelController Instance { get; private set; }

    [SerializeField] private UIPanel panel;
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private BagItemSlot bagPrefab;

    private readonly List<BagItemSlot> _slots = new List<BagItemSlot>(4);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Show reward list. Warns only when no instance.</summary>
    public static void Show(params Reward[] rewards)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[GetPanelController] No instance. Enter MainScene first.");
            return;
        }

        Instance.ShowRewards(rewards);
    }

    /// <summary>Show rewards. Does not modify save data.</summary>
    public void ShowRewards(params Reward[] rewards)
    {
        if (rewards == null || rewards.Length == 0 || panel == null || rowRoot == null || bagPrefab == null)
        {
            Debug.LogWarning("[GetPanelController] Missing rewards or references.");
            return;
        }

        // Show first so BagItemSlot.isActiveAndEnabled and icon load coroutines can start.
        panel.Show();
        if (!rowRoot.gameObject.activeSelf)
        {
            rowRoot.gameObject.SetActive(true);
        }

        RebuildRows(rewards);
    }

    private void RebuildRows(Reward[] rewards)
    {
        ClearSlots();
        for (int i = 0; i < rewards.Length; i++)
        {
            Reward reward = rewards[i];
            if (reward.count <= 0)
            {
                continue;
            }

            GameItemDefs.Def def = null;
            if (!reward.useCurrency)
            {
                if (!string.IsNullOrEmpty(reward.itemId))
                {
                    def = GameItemDefs.Get(reward.itemId);
                }
                // Fallback: callers may only set iconLocation (often same as item id).
                if (def == null && !string.IsNullOrEmpty(reward.iconLocation))
                {
                    def = GameItemDefs.Get(reward.iconLocation);
                }
            }

            string displayName = reward.displayName;
            if (string.IsNullOrEmpty(displayName) && def != null)
            {
                displayName = def.displayName;
            }

            string iconLocation = ResolveIcon(reward, def);
            // Gems (and equipment) use native icon size; gold Result path already SetNativeSize separately.
            bool isGemsIcon = (reward.useCurrency && reward.currency == CurrencyType.Gems)
                || iconLocation == GameItemDefs.CurrencyIconLocation(CurrencyType.Gems);
            bool setNativeSize = (def != null && def.IsEquipment) || isGemsIcon;
            string bindId = !string.IsNullOrEmpty(reward.itemId)
                ? reward.itemId
                : (def != null && !string.IsNullOrEmpty(def.id)
                    ? def.id
                    : (reward.useCurrency ? ("currency_" + reward.currency) : string.Empty));

            BagItemSlot slot = Instantiate(bagPrefab, rowRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(
                bindId,
                displayName,
                reward.count,
                iconLocation,
                null,
                showCount: true,
                setNativeSize: setNativeSize);
            slot.SetSelected(false);
            _slots.Add(slot);
        }
    }

    private static string ResolveIcon(Reward reward, GameItemDefs.Def def)
    {
        // Gem rewards always use YooAsset ZJM-ZS (ignore stale Icon_Gems / wrong pack icons).
        if (reward.useCurrency && reward.currency == CurrencyType.Gems)
        {
            return GameItemDefs.CurrencyIconLocation(CurrencyType.Gems);
        }

        if (!string.IsNullOrEmpty(reward.iconLocation))
        {
            if (reward.iconLocation == "Icon_Gems")
            {
                return GameItemDefs.CurrencyIconLocation(CurrencyType.Gems);
            }

            if (reward.iconLocation == "Icon_Gold")
            {
                return GameItemDefs.CurrencyIconLocation(CurrencyType.Gold);
            }

            return reward.iconLocation;
        }

        if (def != null && !string.IsNullOrEmpty(def.iconLocation))
        {
            return def.iconLocation;
        }

        if (reward.useCurrency)
        {
            return GameItemDefs.CurrencyIconLocation(reward.currency);
        }

        return null;
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
            {
                Destroy(_slots[i].gameObject);
            }
        }

        _slots.Clear();
    }
}