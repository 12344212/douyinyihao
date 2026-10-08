using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店：列出 GameItemDefs 里的全部道具（目前只有治疗药水）。
/// 挂在 ShopPanel 上，商品格子放进 Content 下的列表。不含装备。
/// </summary>
public class ShopPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private ShopItemSlot slotPrefab;
    [SerializeField] private BuyPanelController buyPanel;

    private readonly List<ShopItemSlot> _slots = new List<ShopItemSlot>(4);

    private void OnEnable()
    {
        if (buyPanel != null)
        {
            buyPanel.Hide();
        }

        Rebuild();
    }

    public void Rebuild()
    {
        Clear();
        if (listRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("[ShopPanelController] List or slot prefab is not assigned.");
            return;
        }

        IReadOnlyList<GameItemDefs.Def> defs = GameItemDefs.All;
        for (int i = 0; i < defs.Count; i++)
        {
            ShopItemSlot slot = Instantiate(slotPrefab, listRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(defs[i], OpenBuy);
            _slots.Add(slot);
        }
    }

    public void OpenBuy(GameItemDefs.Def def)
    {
        if (buyPanel == null)
        {
            Debug.LogWarning("[ShopPanelController] Buy panel is not assigned.");
            return;
        }

        buyPanel.Open(def);
    }

    private void Clear()
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