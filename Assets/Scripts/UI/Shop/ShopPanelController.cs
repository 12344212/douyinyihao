using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店：列表选中 → ItemData（价格=数量×单价）→ BuyPanel。
/// 累计购买件数驱动进度条 0..5；宝箱在 1/3/5 件可领 50/150/300 金币。
/// </summary>
public class ShopPanelController : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private ShopItemSlot slotPrefab;
    [SerializeField] private BuyPanelController buyPanel;
    [SerializeField] private ItemConfig itemConfig;

    [Header("ItemData")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text itemDescription;
    [SerializeField] private TMP_Text itemPrice;
    [SerializeField] private Button itemBuyButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    [Header("Progress")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text refreshTimerLabel;

    [Header("Chests")]
    [SerializeField] private Button[] chestButtons;
    [SerializeField] private Image[] chestImages;

    private readonly List<ShopItemSlot> _slots = new List<ShopItemSlot>(8);
    private GameItemDefs.Def _selected;
    private Coroutine _iconLoad;
    private int _previewQty = 1;
    private bool _buyEventsBound;

    private static readonly Color ChestReady = Color.white;
    private static readonly Color ChestLocked = new Color(0.65f, 0.65f, 0.65f, 0.85f);
    private static readonly Color ChestClaimed = new Color(0.45f, 0.45f, 0.45f, 0.5f);

    private void Awake()
    {
        if (itemBuyButton != null)
        {
            itemBuyButton.onClick.AddListener(OnItemBuyClicked);
        }
    }

    private void OnDestroy()
    {
        if (itemBuyButton != null)
        {
            itemBuyButton.onClick.RemoveListener(OnItemBuyClicked);
        }

        UnbindBuyPanelEvents();
        UnbindChestButtons();
    }

    private void OnEnable()
    {
        UnbindChestButtons();
        BindChestButtons();
        BindBuyPanelEvents();
        if (buyPanel != null)
        {
            buyPanel.Hide();
        }

        if (refreshTimerLabel != null)
        {
            refreshTimerLabel.text = "Refresh in 24:00:00";
        }

        _previewQty = 1;
        Rebuild();
        RefreshProgressAndChests();
    }

    private void OnDisable()
    {
        UnbindBuyPanelEvents();
        UnbindChestButtons();
    }

    private void BindBuyPanelEvents()
    {
        if (buyPanel == null || _buyEventsBound)
        {
            return;
        }

        buyPanel.QtyChanged += OnBuyQtyChanged;
        buyPanel.PurchaseSucceeded += OnPurchaseSucceeded;
        buyPanel.Closed += OnBuyClosed;
        _buyEventsBound = true;
    }

    private void UnbindBuyPanelEvents()
    {
        if (buyPanel == null || !_buyEventsBound)
        {
            return;
        }

        buyPanel.QtyChanged -= OnBuyQtyChanged;
        buyPanel.PurchaseSucceeded -= OnPurchaseSucceeded;
        buyPanel.Closed -= OnBuyClosed;
        _buyEventsBound = false;
    }

    private void BindChestButtons()
    {
        if (chestButtons == null)
        {
            return;
        }

        for (int i = 0; i < chestButtons.Length; i++)
        {
            int index = i;
            if (chestButtons[i] != null)
            {
                chestButtons[i].onClick.AddListener(() => OnChestClicked(index));
            }
        }
    }

    private void UnbindChestButtons()
    {
        if (chestButtons == null)
        {
            return;
        }

        for (int i = 0; i < chestButtons.Length; i++)
        {
            if (chestButtons[i] != null)
            {
                chestButtons[i].onClick.RemoveAllListeners();
            }
        }
    }

    public void Rebuild()
    {
        Clear();
        if (listRoot == null || slotPrefab == null)
        {
            Debug.LogWarning("[ShopPanelController] List or slot prefab is not assigned.");
            return;
        }

        if (GameItemDefs.Current == null && itemConfig != null)
        {
            GameItemDefs.Bind(itemConfig);
        }

        IReadOnlyList<GameItemDefs.Def> defs = GameItemDefs.All;
        for (int i = 0; i < defs.Count; i++)
        {
            ShopItemSlot slot = Instantiate(slotPrefab, listRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(defs[i], SelectItem);
            _slots.Add(slot);
        }

        if (_slots.Count > 0)
        {
            SelectItem(_slots[0].Def);
        }
        else
        {
            ClearItemData();
        }
    }

    public void SelectItem(GameItemDefs.Def def)
    {
        _selected = def;
        _previewQty = 1;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
            {
                _slots[i].SetSelected(_slots[i].Def == def);
            }
        }

        RefreshItemData();
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

    private void OnItemBuyClicked()
    {
        if (_selected != null)
        {
            OpenBuy(_selected);
        }
    }

    private void OnBuyQtyChanged(int qty)
    {
        _previewQty = Mathf.Max(1, qty);
        RefreshItemPrice();
    }

    private void OnBuyClosed()
    {
        _previewQty = 1;
        RefreshItemPrice();
    }

    private void OnPurchaseSucceeded(int qty)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save != null)
        {
            save.AddShopPurchaseCount(qty);
        }

        RefreshProgressAndChests();
    }

    private void OnChestClicked(int index)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            TipPanelUI.ShowTip("Not ready.");
            return;
        }

        if (save.IsShopChestClaimed(index))
        {
            TipPanelUI.ShowTip("Claimed!");
            return;
        }

        if (!save.IsShopChestReady(index))
        {
            TipPanelUI.ShowTip("Not ready.");
            return;
        }

        int gold;
        if (!save.TryClaimShopChest(index, out gold))
        {
            TipPanelUI.ShowTip("Not ready.");
            return;
        }

        GetPanelController.Show(new GetPanelController.Reward
        {
            useCurrency = true,
            currency = CurrencyType.Gold,
            displayName = "Gold",
            count = gold
        });
        TipPanelUI.ShowTip("Claimed!");
        RefreshProgressAndChests();
    }

    private void RefreshProgressAndChests()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int count = save != null ? save.GetShopPurchaseCount() : 0;
        int shown = Mathf.Clamp(count, 0, LocalSaveManager.ShopProgressMax);

        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = (float)shown / LocalSaveManager.ShopProgressMax;
        }

        if (progressLabel != null)
        {
            progressLabel.text = shown + "/" + LocalSaveManager.ShopProgressMax;
        }

        for (int i = 0; i < 3; i++)
        {
            RefreshChestVisual(i, save);
        }
    }

    private void RefreshChestVisual(int index, LocalSaveManager save)
    {
        bool claimed = save != null && save.IsShopChestClaimed(index);
        bool ready = save != null && save.IsShopChestReady(index);

        if (chestImages != null && index < chestImages.Length && chestImages[index] != null)
        {
            if (claimed)
            {
                chestImages[index].color = ChestClaimed;
            }
            else if (ready)
            {
                chestImages[index].color = ChestReady;
            }
            else
            {
                chestImages[index].color = ChestLocked;
            }
        }

        if (chestButtons != null && index < chestButtons.Length && chestButtons[index] != null)
        {
            chestButtons[index].interactable = ready;
        }
    }

    private void RefreshItemData()
    {
        if (_selected == null)
        {
            ClearItemData();
            return;
        }

        if (itemName != null)
        {
            itemName.text = _selected.displayName;
        }

        if (itemDescription != null)
        {
            itemDescription.text = _selected.description;
            itemDescription.enableWordWrapping = true;
        }

        RefreshItemPrice();

        if (itemBuyButton != null)
        {
            itemBuyButton.interactable = true;
        }

        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.color = placeholderColor;
            if (_iconLoad != null)
            {
                StopCoroutine(_iconLoad);
            }

            _iconLoad = StartCoroutine(LoadItemIcon(_selected.iconLocation));
        }
    }

    private void RefreshItemPrice()
    {
        if (itemPrice == null || _selected == null)
        {
            return;
        }

        int qty = _previewQty;
        if (buyPanel != null && buyPanel.IsOpen)
        {
            qty = buyPanel.CurrentQty;
        }

        itemPrice.text = ShopItemSlot.FormatPrice(_selected.price * Mathf.Max(1, qty), _selected.currency);
    }

    private void ClearItemData()
    {
        if (itemName != null) itemName.text = string.Empty;
        if (itemDescription != null) itemDescription.text = string.Empty;
        if (itemPrice != null) itemPrice.text = string.Empty;
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.color = placeholderColor;
        }

        if (itemBuyButton != null)
        {
            itemBuyButton.interactable = false;
        }
    }

    private IEnumerator LoadItemIcon(string location)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (itemIcon == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            _iconLoad = null;
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && itemIcon != null)
        {
            itemIcon.sprite = sprite;
            itemIcon.color = Color.white;
        }

        _iconLoad = null;
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
        _selected = null;
    }
}