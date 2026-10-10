using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 背包：ItemList 填格子，ItemData 显示选中项。打开时默认选中第一项。
/// </summary>
public class BagPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private BagItemSlot slotPrefab;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private BagDetailView itemData;
    [SerializeField] private ItemConfig itemConfig;

    private readonly List<BagItemSlot> _slots = new List<BagItemSlot>(8);
    private string _selectedId;
    private bool _warnedMissingSave;

    private void OnEnable()
    {
        LocalSaveManager.OnItemsChanged += OnItemsChanged;
        Rebuild();
    }

    private void OnDisable()
    {
        LocalSaveManager.OnItemsChanged -= OnItemsChanged;
    }

    private void OnDestroy()
    {
        LocalSaveManager.OnItemsChanged -= OnItemsChanged;
    }

    public void Rebuild()
    {
        string keepId = _selectedId;
        ClearSlots();
        _selectedId = null;

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            if (!_warnedMissingSave)
            {
                _warnedMissingSave = true;
                Debug.LogWarning("[BagPanelController] LocalSaveManager not found. Start from LoadScene.");
            }

            SetEmpty(true);
            if (itemData != null)
            {
                itemData.Clear();
            }

            return;
        }

        if (GameItemDefs.Current == null && itemConfig != null)
        {
            GameItemDefs.Bind(itemConfig);
        }

        if (listRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("[BagPanelController] List or slot prefab is not assigned.");
            return;
        }

        List<ItemStackEntry> items = save.GetAllItems();
        int shown = 0;
        for (int i = 0; i < items.Count; i++)
        {
            ItemStackEntry stack = items[i];
            if (stack == null || stack.count <= 0 || string.IsNullOrEmpty(stack.itemId))
            {
                continue;
            }

            GameItemDefs.Def def = GameItemDefs.Get(stack.itemId);
            BagItemSlot slot = Instantiate(slotPrefab, listRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(
                stack.itemId,
                def != null ? def.displayName : stack.itemId,
                stack.count,
                def != null ? def.iconLocation : null,
                OnSlotClicked);
            _slots.Add(slot);
            shown++;
        }

        SetEmpty(shown == 0);
        if (shown == 0)
        {
            if (itemData != null)
            {
                itemData.Clear();
            }

            return;
        }

        string pick = keepId;
        if (string.IsNullOrEmpty(pick) || FindSlot(pick) == null)
        {
            pick = _slots[0].ItemId;
        }

        SelectItem(pick);
    }

    private void OnSlotClicked(string itemId)
    {
        SelectItem(itemId);
    }

    private void SelectItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return;
        }

        _selectedId = itemId;
        for (int i = 0; i < _slots.Count; i++)
        {
            BagItemSlot slot = _slots[i];
            if (slot != null)
            {
                slot.SetSelected(slot.ItemId == itemId);
            }
        }

        ShowItemData(itemId);
    }

    private void ShowItemData(string itemId)
    {
        if (itemData == null || string.IsNullOrEmpty(itemId))
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        int count = save != null ? save.GetItemCount(itemId) : 0;
        if (count <= 0)
        {
            itemData.Clear();
            return;
        }

        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        itemData.Show(
            itemId,
            def != null ? def.displayName : itemId,
            def != null ? def.description : string.Empty,
            count,
            def != null ? def.iconLocation : null);
    }

    private BagItemSlot FindSlot(string itemId)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].ItemId == itemId)
            {
                return _slots[i];
            }
        }

        return null;
    }

    private void OnItemsChanged()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        Rebuild();
    }

    private void SetEmpty(bool empty)
    {
        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(empty);
            if (empty)
            {
                emptyLabel.text = "Your bag is empty.";
            }
        }
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