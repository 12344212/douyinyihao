using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TopPrefab 单行：图标、英文名、钻石数、价格、Buy。引用全部在预制体上序列化。
/// </summary>
public class TopUpItemSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text gemText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Color placeholderColor = new Color(0.25f, 0.55f, 0.72f, 1f);

    private GemPackDefs.Pack _pack;
    private Action<GemPackDefs.Pack> _onBuy;
    private Coroutine _iconLoad;

    private void Awake()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnDestroy()
    {
        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnClicked);
        }
    }

    public void Bind(GemPackDefs.Pack pack, Action<GemPackDefs.Pack> onBuy)
    {
        _pack = pack;
        _onBuy = onBuy;
        if (nameText != null)
        {
            nameText.text = pack.displayName;
        }

        if (gemText != null)
        {
            gemText.text = pack.GemLabel;
        }

        if (priceText != null)
        {
            priceText.text = pack.PriceLabel;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
        }

        if (_iconLoad != null)
        {
            StopCoroutine(_iconLoad);
        }

        _iconLoad = StartCoroutine(LoadIcon(pack.iconLocation));
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

    private void OnClicked()
    {
        if (_onBuy != null && _pack.gems > 0)
        {
            _onBuy(_pack);
        }
    }
}
