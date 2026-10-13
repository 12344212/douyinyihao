using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop list row: icon, English name, price. Row click selects; BUY opens Buy panel.
/// Icon from GameItemDefs address via YooAsset.
/// Price label is digits only; BUY/Image shows currency (ZJM-ZS for gems, keep gold for gold).
/// </summary>
public class ShopItemSlot : MonoBehaviour
{
    private const string GemsIconAddress = "ZJM-ZS";

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button clickButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.75f, 1f);

    private GameItemDefs.Def _def;
    private Action<GameItemDefs.Def> _onClick;
    private Action<GameItemDefs.Def> _onBuy;
    private Image _bg;
    private Image _currencyIcon;
    private Sprite _defaultCurrencySprite;
    private bool _currencyIconResolved;
    private Coroutine _currencyIconLoad;

    private void Awake()
    {
        _bg = GetComponent<Image>();
        ResolveCurrencyIcon();
        if (clickButton != null)
        {
            clickButton.onClick.AddListener(OnClick);
        }

        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyClick);
        }
    }

    private void OnDestroy()
    {
        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(OnClick);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnBuyClick);
        }
    }

    public void Bind(GameItemDefs.Def def, Action<GameItemDefs.Def> onClick, Action<GameItemDefs.Def> onBuy = null)
    {
        _def = def;
        _onClick = onClick;
        _onBuy = onBuy;
        if (nameLabel != null)
        {
            nameLabel.text = def != null ? def.displayName : string.Empty;
        }

        if (priceLabel != null)
        {
            priceLabel.text = def != null ? FormatPrice(def.price, def.currency) : string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
        }

        if (buyButton != null)
        {
            buyButton.interactable = def != null;
        }

        ApplyCurrencyIcon(def);
        SetSelected(false);
        if (isActiveAndEnabled && def != null)
        {
            StartCoroutine(LoadIcon(def.iconLocation));
        }
    }

    public void SetSelected(bool selected)
    {
        if (_bg != null)
        {
            _bg.color = selected ? selectedColor : normalColor;
        }
    }

    public GameItemDefs.Def Def => _def;

    /// <summary>Digits only; currency shown via BUY/Image (ZJM-JB / ZJM-ZS).</summary>
    public static string FormatPrice(int price, CurrencyType currency)
    {
        return price.ToString();
    }

    private void ResolveCurrencyIcon()
    {
        if (_currencyIconResolved)
        {
            return;
        }

        _currencyIconResolved = true;
        if (buyButton == null)
        {
            return;
        }

        Transform imageT = buyButton.transform.Find("Image");
        if (imageT == null)
        {
            return;
        }

        _currencyIcon = imageT.GetComponent<Image>();
        if (_currencyIcon != null)
        {
            _defaultCurrencySprite = _currencyIcon.sprite;
        }
    }

    private void ApplyCurrencyIcon(GameItemDefs.Def def)
    {
        ResolveCurrencyIcon();
        if (_currencyIconLoad != null)
        {
            StopCoroutine(_currencyIconLoad);
            _currencyIconLoad = null;
        }

        if (_currencyIcon == null || def == null)
        {
            return;
        }

        if (def.currency == CurrencyType.Gems)
        {
            if (isActiveAndEnabled)
            {
                _currencyIconLoad = StartCoroutine(LoadCurrencyIcon(GemsIconAddress));
            }
            return;
        }

        // Gold: keep prefab gold icon if any.
        if (_defaultCurrencySprite != null)
        {
            _currencyIcon.sprite = _defaultCurrencySprite;
            _currencyIcon.color = Color.white;
            _currencyIcon.preserveAspect = true;
        }
    }

    private IEnumerator LoadCurrencyIcon(string location)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (_currencyIcon == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            _currencyIconLoad = null;
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && _currencyIcon != null)
        {
            _currencyIcon.sprite = sprite;
            _currencyIcon.color = Color.white;
            _currencyIcon.preserveAspect = true;
        }

        _currencyIconLoad = null;
    }

    private IEnumerator LoadIcon(string location)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (iconImage == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
        }
    }

    private void OnClick()
    {
        if (_def != null)
        {
            _onClick?.Invoke(_def);
        }
    }

    private void OnBuyClick()
    {
        if (_def == null)
        {
            return;
        }

        if (_onBuy != null)
        {
            _onBuy.Invoke(_def);
            return;
        }

        _onClick?.Invoke(_def);
    }
}
