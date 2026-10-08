using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 背包：按 LocalSaveManager.GetAllItems() 填列表。打开时刷新，并监听 OnItemsChanged。
/// 没有道具时显示 "Your bag is empty."。点击一行打开详情。不含装备。
/// </summary>
public class BagPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private BagItemSlot slotPrefab;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private BagDetailView detailView;
    [SerializeField] private ItemConfig itemConfig;

    private readonly List<BagItemSlot> _slots = new List<BagItemSlot>(8);
    private bool _warnedMissingSave;

    private void OnEnable()
    {
        LocalSaveManager.OnItemsChanged += OnItemsChanged;
        if (detailView != null)
        {
            detailView.Hide();
        }

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
        ClearSlots();
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            if (!_warnedMissingSave)
            {
                _warnedMissingSave = true;
                Debug.LogWarning("[BagPanelController] LocalSaveManager not found. Start from LoadScene.");
            }

            SetEmpty(true);
            return;
        }

        if (GameItemDefs.Current == null && itemConfig != null)
        {
            GameItemDefs.Bind(itemConfig);
        }

        List<ItemStackEntry> items = save.GetAllItems();
        if (listRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("[BagPanelController] List or slot prefab is not assigned.");
            return;
        }

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
            slot.Bind(stack.itemId, def != null ? def.displayName : stack.itemId, stack.count, def != null ? def.iconLocation : null, OpenDetail);
            _slots.Add(slot);
            shown++;
        }

        SetEmpty(shown == 0);
    }

    public void OpenDetail(string itemId)
    {
        if (detailView == null || string.IsNullOrEmpty(itemId))
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        int count = save != null ? save.GetItemCount(itemId) : 0;
        if (count <= 0)
        {
            detailView.Hide();
            return;
        }

        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        detailView.Show(itemId, def != null ? def.displayName : itemId, def != null ? def.description : string.Empty, count);
    }

    private void OnItemsChanged()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (detailView != null)
        {
            detailView.Hide();
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