using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop list + Buy confirm (folded from BuyPanel). Slot BUY opens Buy panel:
/// icon, description, +/- qty, total price, currency icon (ZJM-JB / ZJM-ZS).
/// Equipment: purchasable once; owned items (persisted itemStacks) are omitted from the list.
/// Purchase count drives progress 0..5; chests at 1/3/5 claim 50/150/300 gold.
/// </summary>
public class ShopPanelController : MonoBehaviour
{
    public const int BuyAbsoluteMax = 99;
    private const string GoldIconAddress = "ZJM-JB";
    private const string GemsIconAddress = "ZJM-ZS";

    [Header("List")]
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private ShopItemSlot slotPrefab;
    [SerializeField] private ItemConfig itemConfig;

    [Header("ItemData (optional detail)")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text itemDescription;
    [SerializeField] private TMP_Text itemPrice;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    [Header("ItemBUY (optional legacy)")]
    [SerializeField] private Button itemBuyButton;

    [Header("Buy panel (ShopPanel/Buy)")]
    [SerializeField] private UIPanel buyPanel;
    [SerializeField] private GameObject buyRoot;
    [SerializeField] private Image buyIconImage;
    [SerializeField] private TMP_Text buyDescLabel;
    [SerializeField] private TMP_Text buyQtyLabel;
    [SerializeField] private TMP_Text buyTotalLabel;
    [SerializeField] private Image buyCurrencyIcon;
    [SerializeField] private Button buyMinusButton;
    [SerializeField] private Button buyPlusButton;
    [SerializeField] private Button buyConfirmButton;
    [SerializeField] private Button buyCancelButton;

    [Header("Progress")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text refreshTimerLabel;

    [Header("Chests")]
    [SerializeField] private Button[] chestButtons;
    [SerializeField] private Image[] chestImages;

    private readonly List<ShopItemSlot> _slots = new List<ShopItemSlot>(8);
    private GameItemDefs.Def _selected;
    private GameItemDefs.Def _buyDef;
    private Coroutine _iconLoad;
    private Coroutine _buyIconLoad;
    private Coroutine _currencyIconLoad;
    private int _previewQty = 1;
    private int _buyQty = 1;
    private bool _buyOpen;
    private bool _buyUiBound;
    private float _refreshTick;

    private static readonly Color ChestReady = Color.white;
    private static readonly Color ChestLocked = new Color(0.65f, 0.65f, 0.65f, 0.85f);
    private static readonly Color ChestClaimed = new Color(0.45f, 0.45f, 0.45f, 0.5f);

    private void Awake()
    {
        BindItemBuyButton(true);
        BindBuyUi(true);
    }

    private void OnDestroy()
    {
        BindItemBuyButton(false);
        BindBuyUi(false);
        UnbindChestButtons();
        LocalSaveManager.OnCurrencyChanged -= OnCurrencyChanged;
    }

    private void BindItemBuyButton(bool bind)
    {
        if (itemBuyButton == null)
        {
            return;
        }

        itemBuyButton.onClick.RemoveListener(OnItemBuyClicked);
        if (bind)
        {
            itemBuyButton.onClick.AddListener(OnItemBuyClicked);
        }
    }

    private void BindBuyUi(bool bind)
    {
        if (_buyUiBound == bind)
        {
            return;
        }

        if (buyMinusButton != null)
        {
            buyMinusButton.onClick.RemoveListener(OnBuyMinus);
            if (bind) buyMinusButton.onClick.AddListener(OnBuyMinus);
        }

        if (buyPlusButton != null)
        {
            buyPlusButton.onClick.RemoveListener(OnBuyPlus);
            if (bind) buyPlusButton.onClick.AddListener(OnBuyPlus);
        }

        if (buyConfirmButton != null)
        {
            buyConfirmButton.onClick.RemoveListener(OnBuyConfirm);
            if (bind) buyConfirmButton.onClick.AddListener(OnBuyConfirm);
        }

        if (buyCancelButton != null)
        {
            buyCancelButton.onClick.RemoveListener(HideBuy);
            if (bind) buyCancelButton.onClick.AddListener(HideBuy);
        }

        if (buyPanel != null)
        {
            buyPanel.Hidden -= OnBuyPanelHidden;
            if (bind) buyPanel.Hidden += OnBuyPanelHidden;
        }

        _buyUiBound = bind;
    }

    private void OnEnable()
    {
        BindItemBuyButton(true);
        BindBuyUi(true);
        EnsureItemDataActive();
        UnbindChestButtons();
        BindChestButtons();
        LocalSaveManager.OnCurrencyChanged -= OnCurrencyChanged;
        LocalSaveManager.OnCurrencyChanged += OnCurrencyChanged;
        EnsureBuyHidden();

        _refreshTick = 0f;
        UpdateRefreshTimerLabel();

        _previewQty = 1;
        Rebuild();
        RefreshProgressAndChests();
    }

    private void OnDisable()
    {
        BindItemBuyButton(false);
        LocalSaveManager.OnCurrencyChanged -= OnCurrencyChanged;
        UnbindChestButtons();
    }

    private void Update()
    {
        if (refreshTimerLabel == null)
        {
            return;
        }

        _refreshTick -= Time.unscaledDeltaTime;
        if (_refreshTick > 0f)
        {
            return;
        }

        _refreshTick = 1f;
        UpdateRefreshTimerLabel();
    }

    /// <summary>
    /// ItemBUY lives under ItemData; keep that root active so the Buy button receives clicks.
    /// </summary>
    private void EnsureItemDataActive()
    {
        if (itemBuyButton == null)
        {
            return;
        }

        Transform parent = itemBuyButton.transform.parent;
        if (parent != null && !parent.gameObject.activeSelf)
        {
            parent.gameObject.SetActive(true);
        }

        if (!itemBuyButton.gameObject.activeSelf)
        {
            itemBuyButton.gameObject.SetActive(true);
        }
    }

    /// <summary>Countdown to next local midnight as Refresh in HH:MM:SS.</summary>
    private void UpdateRefreshTimerLabel()
    {
        if (refreshTimerLabel == null)
        {
            return;
        }

        DateTime now = DateTime.Now;
        DateTime nextMidnight = now.Date.AddDays(1);
        TimeSpan remain = nextMidnight - now;
        if (remain.TotalSeconds < 0)
        {
            remain = TimeSpan.Zero;
        }

        int hours = (int)remain.TotalHours;
        refreshTimerLabel.text = string.Format(
            "Refresh in {0:00}:{1:00}:{2:00}",
            hours,
            remain.Minutes,
            remain.Seconds);
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

        LocalSaveManager save = LocalSaveManager.Instance;
        IReadOnlyList<GameItemDefs.Def> defs = GameItemDefs.All;
        for (int i = 0; i < defs.Count; i++)
        {
            GameItemDefs.Def def = defs[i];
            // Equipment: once owned (persisted via itemStacks), hide from shop.
            if (def != null && def.IsEquipment && save != null && save.HasItem(def.id))
            {
                continue;
            }

            ShopItemSlot slot = Instantiate(slotPrefab, listRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(def, SelectItem, OpenBuy);
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
        if (buyPanel == null && buyRoot == null)
        {
            Debug.LogWarning("[ShopPanelController] Buy panel is not assigned.");
            TipPanelUI.ShowTip("Buy panel missing.");
            return;
        }

        if (def == null)
        {
            TipPanelUI.ShowTip("Select an item.");
            return;
        }

        if (def.IsEquipment)
        {
            LocalSaveManager save = LocalSaveManager.Instance;
            if (save != null && save.HasItem(def.id))
            {
                TipPanelUI.ShowTip("Already owned.");
                Rebuild();
                return;
            }
        }

        SelectItem(def);
        OpenBuyInternal(def);
    }

    private void OnItemBuyClicked()
    {
        if (_selected == null)
        {
            TipPanelUI.ShowTip("Select an item.");
            return;
        }

        OpenBuy(_selected);
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
            itemDescription.text = _selected.FormatDetailText();
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
        if (_buyOpen)
        {
            qty = _buyQty;
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

    // ---------- Buy panel (was BuyPanelController) ----------

    private void OpenBuyInternal(GameItemDefs.Def def)
    {
        _buyDef = def;
        _buyQty = 1;
        _buyOpen = true;
        _previewQty = 1;

        GameObject root = buyRoot != null ? buyRoot : (buyPanel != null ? buyPanel.gameObject : null);
        if (root != null && !root.activeSelf)
        {
            root.SetActive(true);
        }

        if (root != null)
        {
            root.transform.SetAsLastSibling();
        }

        if (buyPanel != null)
        {
            buyPanel.Show();
        }

        CanvasGroup group = root != null ? root.GetComponent<CanvasGroup>() : null;
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        if (buyDescLabel != null)
        {
            buyDescLabel.text = def.FormatDetailText();
            buyDescLabel.enableWordWrapping = true;
        }

        if (buyIconImage != null)
        {
            buyIconImage.sprite = null;
            buyIconImage.color = placeholderColor;
            if (_buyIconLoad != null)
            {
                StopCoroutine(_buyIconLoad);
            }

            _buyIconLoad = StartCoroutine(LoadBuyIcon(def.iconLocation));
        }

        if (_currencyIconLoad != null)
        {
            StopCoroutine(_currencyIconLoad);
        }

        _currencyIconLoad = StartCoroutine(LoadBuyCurrencyIcon(def.currency));
        ApplyBuyQty();
    }

    public void HideBuy()
    {
        GameObject root = buyRoot != null ? buyRoot : (buyPanel != null ? buyPanel.gameObject : null);
        if (buyPanel != null)
        {
            if (root != null && !root.activeSelf)
            {
                FinishBuyClose();
                return;
            }

            buyPanel.Hide();
            return;
        }

        if (root != null && root.activeSelf)
        {
            root.SetActive(false);
        }

        FinishBuyClose();
    }

    /// <summary>Shop open: force Buy closed without relying on inactive UIPanel.Hide early-out.</summary>
    public void EnsureBuyHidden()
    {
        _buyOpen = false;
        if (buyPanel != null)
        {
            buyPanel.HideImmediate();
        }

        GameObject root = buyRoot != null ? buyRoot : (buyPanel != null ? buyPanel.gameObject : null);
        if (root != null && root.activeSelf)
        {
            root.SetActive(false);
        }
    }

    private void OnBuyPanelHidden(UIPanel _)
    {
        FinishBuyClose();
    }

    private void FinishBuyClose()
    {
        if (!_buyOpen)
        {
            return;
        }

        _buyOpen = false;
        _previewQty = 1;
        RefreshItemPrice();
    }

    private void OnCurrencyChanged()
    {
        if (_buyOpen)
        {
            ApplyBuyQty();
        }
    }

    private void OnBuyMinus()
    {
        if (_buyQty > 1)
        {
            _buyQty--;
            ApplyBuyQty();
        }
    }

    private void OnBuyPlus()
    {
        if (_buyDef == null)
        {
            return;
        }

        int next = _buyQty + 1;
        if (next > BuyAbsoluteMax)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (_buyDef.price > 0 && save != null)
        {
            int total = _buyDef.price * next;
            if (save.GetCurrency(_buyDef.currency) < total)
            {
                bool gems = _buyDef.currency == CurrencyType.Gems;
                TipPanelUI.ShowTip(gems ? "Not enough gems." : "Not enough gold.");
                return;
            }
        }

        _buyQty = next;
        ApplyBuyQty();
    }

    private void OnBuyConfirm()
    {
        if (_buyDef == null)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        // Equipment is purchasable once; qty locked to 1.
        int bought = _buyDef.IsEquipment ? 1 : _buyQty;
        if (_buyDef.IsEquipment && save != null && save.HasItem(_buyDef.id))
        {
            TipPanelUI.ShowTip("Already owned.");
            HideBuy();
            Rebuild();
            return;
        }

        int total = _buyDef.price * bought;
        bool gems = _buyDef.currency == CurrencyType.Gems;
        if (save == null || !save.TrySpend(_buyDef.currency, total))
        {
            TipPanelUI.ShowTip(gems ? "Not enough gems." : "Not enough gold.");
            return;
        }

        // Currency pack: spend price currency, grant grantCurrency * qty (no bag stack).
        if (_buyDef.IsCurrencyPack)
        {
            int grantTotal = _buyDef.grantAmount * bought;
            save.AddCurrency(_buyDef.grantCurrency, grantTotal);
            GetPanelController.Show(new GetPanelController.Reward
            {
                useCurrency = true,
                currency = _buyDef.grantCurrency,
                iconLocation = _buyDef.iconLocation,
                displayName = _buyDef.grantCurrency == CurrencyType.Gems ? "Gems" : "Gold",
                count = grantTotal
            });
            save.AddShopPurchaseCount(bought);
            RefreshProgressAndChests();
            HideBuy();
            return;
        }

        if (!save.AddItem(_buyDef.id, bought))
        {
            save.AddCurrency(_buyDef.currency, total);
            Debug.LogWarning("[ShopPanelController] AddItem failed, currency refunded.");
            return;
        }

        GetPanelController.Show(new GetPanelController.Reward
        {
            iconLocation = _buyDef.iconLocation,
            displayName = _buyDef.displayName,
            count = bought
        });

        save.AddShopPurchaseCount(bought);
        RefreshProgressAndChests();
        HideBuy();

        // Owned equipment leaves the shop list (persisted via itemStacks / HasItem).
        if (_buyDef.IsEquipment)
        {
            Rebuild();
        }
    }

    private int BuyMaxAllowed()
    {
        if (_buyDef != null && _buyDef.IsEquipment)
        {
            return 1;
        }

        int cap = BuyAbsoluteMax;
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save != null && _buyDef != null && _buyDef.price > 0)
        {
            int afford = save.GetCurrency(_buyDef.currency) / _buyDef.price;
            if (afford <= 0)
            {
                return 1;
            }

            cap = Mathf.Min(BuyAbsoluteMax, afford);
        }

        return Mathf.Max(1, cap);
    }

    private void ApplyBuyQty()
    {
        int max = BuyMaxAllowed();
        _buyQty = Mathf.Clamp(_buyQty, 1, max);
        _previewQty = _buyQty;

        if (buyQtyLabel != null)
        {
            buyQtyLabel.text = _buyQty.ToString();
        }

        if (buyTotalLabel != null && _buyDef != null)
        {
            // Number only; currency shown via buyCurrencyIcon (ZJM-JB / ZJM-ZS).
            buyTotalLabel.text = (_buyDef.price * _buyQty).ToString();
        }

        bool equipOnce = _buyDef != null && _buyDef.IsEquipment;
        if (buyMinusButton != null) buyMinusButton.interactable = !equipOnce && _buyQty > 1;
        // Stay clickable at afford cap so OnBuyPlus can show Not enough gold/gems tip.
        // Equipment once-buy: +/- locked (qty always 1).
        if (buyPlusButton != null) buyPlusButton.interactable = !equipOnce && _buyQty < BuyAbsoluteMax;

        RefreshItemPrice();
    }

    private IEnumerator LoadBuyIcon(string location)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (buyIconImage == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            _buyIconLoad = null;
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && buyIconImage != null)
        {
            buyIconImage.sprite = sprite;
            buyIconImage.color = Color.white;
        }

        _buyIconLoad = null;
    }

    private IEnumerator LoadBuyCurrencyIcon(CurrencyType currency)
    {
        if (buyCurrencyIcon == null)
        {
            _currencyIconLoad = null;
            yield break;
        }

        string location = currency == CurrencyType.Gems ? GemsIconAddress : GoldIconAddress;
        YooAssetManager yoo = YooAssetManager.Instance;
        if (yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            _currencyIconLoad = null;
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && buyCurrencyIcon != null)
        {
            buyCurrencyIcon.sprite = sprite;
            buyCurrencyIcon.color = Color.white;
            buyCurrencyIcon.preserveAspect = true;
        }

        _currencyIconLoad = null;
    }
}
