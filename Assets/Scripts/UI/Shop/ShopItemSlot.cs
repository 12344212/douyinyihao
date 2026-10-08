using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店里的一件商品：图标、英文名、价格（如 150 Gold）、Buy 按钮。
/// 图标按 GameItemDefs 的地址走 YooAsset，没有资源时保留色块。
/// </summary>
public class ShopItemSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button buyButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private GameItemDefs.Def _def;
    private Action<GameItemDefs.Def> _onBuy;

    private void Awake()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuy);
        }
    }

    private void OnDestroy()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnBuy);
        }
    }

    public void Bind(GameItemDefs.Def def, Action<GameItemDefs.Def> onBuy)
    {
        _def = def;
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

        if (isActiveAndEnabled && def != null)
        {
            StartCoroutine(LoadIcon(def.iconLocation));
        }
    }

    public static string FormatPrice(int price, CurrencyType currency)
    {
        return price + (currency == CurrencyType.Gems ? " Gems" : " Gold");
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

    private void OnBuy()
    {
        if (_def != null)
        {
            _onBuy?.Invoke(_def);
        }
    }
}