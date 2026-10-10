using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店列表行：图标、英文名、价格。整行点击回调（选中），不含直接购买。
/// 图标按 GameItemDefs 地址走 YooAsset。
/// </summary>
public class ShopItemSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button clickButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.75f, 1f);

    private GameItemDefs.Def _def;
    private Action<GameItemDefs.Def> _onClick;
    private Image _bg;

    private void Awake()
    {
        _bg = GetComponent<Image>();
        if (clickButton != null)
        {
            clickButton.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (clickButton != null)
        {
            clickButton.onClick.RemoveListener(OnClick);
        }
    }

    public void Bind(GameItemDefs.Def def, Action<GameItemDefs.Def> onClick)
    {
        _def = def;
        _onClick = onClick;
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

    private void OnClick()
    {
        if (_def != null)
        {
            _onClick?.Invoke(_def);
        }
    }
}
// empty-refs cleanup: dropped unused selectedHighlight
