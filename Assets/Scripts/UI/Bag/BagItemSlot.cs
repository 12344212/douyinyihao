using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包一行：图标、英文名、数量 xN。点击通知外面打开详情。
/// 图标按地址走 YooAsset，没有资源时用色块。
/// </summary>
public class BagItemSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private Button button;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private string _itemId;
    private Action<string> _onClick;

    public string ItemId => _itemId;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Bind(string itemId, string displayName, int count, string iconLocation, Action<string> onClick)
    {
        _itemId = itemId;
        _onClick = onClick;
        if (nameLabel != null)
        {
            nameLabel.text = string.IsNullOrEmpty(displayName) ? itemId : displayName;
        }

        if (countLabel != null)
        {
            countLabel.text = "x" + Mathf.Max(0, count);
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
        }

        if (isActiveAndEnabled)
        {
            StartCoroutine(LoadIcon(iconLocation));
        }
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

    private void HandleClick()
    {
        if (!string.IsNullOrEmpty(_itemId))
        {
            _onClick?.Invoke(_itemId);
        }
    }
}