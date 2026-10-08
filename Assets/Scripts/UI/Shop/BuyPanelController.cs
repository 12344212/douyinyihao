using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 购买确认：数量加减（最少 1，最多 99，且不超过当前货币能买的数量）。
/// 确认时扣货币并发道具；钱不够只弹提示，不改存档。
/// 本面板不使用道具。弹出动画用 UIPanel。
/// </summary>
public class BuyPanelController : MonoBehaviour
{
    public const int AbsoluteMax = 99;

    [SerializeField] private UIPanel panel;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descLabel;
    [SerializeField] private TMP_Text qtyLabel;
    [SerializeField] private TMP_Text totalLabel;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private GameItemDefs.Def _def;
    private int _qty = 1;

    private void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
        if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
    }

    private void OnDestroy()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirm);
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
        if (descLabel != null) descLabel.text = def.description;
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
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
            // 一件都买不起时仍允许数量停在 1，确认时弹出钱不够
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
            totalLabel.text = "Total: " + ShopItemSlot.FormatPrice(_def.price * _qty, _def.currency);
        }

        if (minusButton != null) minusButton.interactable = _qty > 1;
        if (plusButton != null) plusButton.interactable = _qty < max;
    }
}