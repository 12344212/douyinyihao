using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 购买确认（SD-CK2）：图标、描述、单价、数量加减、Cancel / Buy。
/// 确认时 TrySpend + AddItem + 奖励弹窗；钱不够只提示。
/// </summary>
public class BuyPanelController : MonoBehaviour
{
    public const int AbsoluteMax = 99;

    [SerializeField] private UIPanel panel;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descLabel;
    [SerializeField] private TMP_Text unitPriceLabel;
    [SerializeField] private TMP_Text qtyLabel;
    [SerializeField] private TMP_Text totalLabel;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private GameItemDefs.Def _def;
    private int _qty = 1;
    private Coroutine _iconLoad;

    private void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
        if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
    }

    private void OnDestroy()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Hide);
    }

    public void Open(GameItemDefs.Def def)
    {
        if (def == null)
        {
            return;
        }

        _def = def;
        _qty = 1;
        if (panel != null)
        {
            panel.Show();
        }
        else
        {
            gameObject.SetActive(true);
        }

        if (nameLabel != null) nameLabel.text = def.displayName;
        if (descLabel != null)
        {
            descLabel.text = def.description;
            descLabel.enableWordWrapping = true;
        }

        if (unitPriceLabel != null)
        {
            unitPriceLabel.text = ShopItemSlot.FormatPrice(def.price, def.currency);
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
            if (_iconLoad != null)
            {
                StopCoroutine(_iconLoad);
            }

            _iconLoad = StartCoroutine(LoadIcon(def.iconLocation));
        }

        ApplyQty();
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.Hide();
        }
        else if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnMinus()
    {
        if (_qty > 1)
        {
            _qty--;
            ApplyQty();
        }
    }

    private void OnPlus()
    {
        if (_qty < MaxAllowed())
        {
            _qty++;
            ApplyQty();
        }
    }

    private void OnConfirm()
    {
        if (_def == null)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        int total = _def.price * _qty;
        bool gems = _def.currency == CurrencyType.Gems;
        if (save == null || !save.TrySpend(_def.currency, total))
        {
            TipPanelUI.ShowTip(gems ? "Not enough gems." : "Not enough gold.");
            return;
        }

        if (!save.AddItem(_def.id, _qty))
        {
            save.AddCurrency(_def.currency, total);
            Debug.LogWarning("[BuyPanelController] AddItem failed, currency refunded.");
            return;
        }

        GetPanelController.Show(new GetPanelController.Reward
        {
            iconLocation = _def.iconLocation,
            displayName = _def.displayName,
            count = _qty
        });
        Hide();
    }

    private int MaxAllowed()
    {
        int cap = AbsoluteMax;
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save != null && _def != null && _def.price > 0)
        {
            int afford = save.GetCurrency(_def.currency) / _def.price;
            cap = Mathf.Min(AbsoluteMax, Mathf.Max(1, afford));
        }

        return cap;
    }

    private void ApplyQty()
    {
        int max = MaxAllowed();
        _qty = Mathf.Clamp(_qty, 1, max);
        if (qtyLabel != null)
        {
            qtyLabel.text = _qty.ToString();
        }

        if (totalLabel != null && _def != null)
        {
            totalLabel.text = ShopItemSlot.FormatPrice(_def.price * _qty, _def.currency);
        }

        if (minusButton != null) minusButton.interactable = _qty > 1;
        if (plusButton != null) plusButton.interactable = _qty < max;
    }

    private IEnumerator LoadIcon(string location)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (iconImage == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            _iconLoad = null;
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
        }

        _iconLoad = null;
    }
}