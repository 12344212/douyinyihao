using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店主面板：列表选中 → ItemData 展示描述与购买按钮 → 打开 BuyPanel。
/// 左侧宝箱 / 进度 / 刷新计时仅为占位展示。
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

    [Header("Left placeholders")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text refreshTimerLabel;

    private readonly List<ShopItemSlot> _slots = new List<ShopItemSlot>(8);
    private GameItemDefs.Def _selected;
    private Coroutine _iconLoad;

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
    }

    private void OnEnable()
    {
        if (buyPanel != null)
        {
            buyPanel.Hide();
        }

        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 1f;
        }

        if (progressLabel != null)
        {
            progressLabel.text = "5/5";
        }

        if (refreshTimerLabel != null)
        {
            refreshTimerLabel.text = "Refresh in 24:00:00";
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

        if (itemPrice != null)
        {
            itemPrice.text = ShopItemSlot.FormatPrice(_selected.price, _selected.currency);
        }

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