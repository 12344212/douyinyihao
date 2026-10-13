using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包格子：图标、数量。选中后才显示 Select。点击通知面板刷新 ItemData。
/// </summary>
public class BagItemSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectVisual;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private string _itemId;
    private Action<string> _onClick;
    private bool _setNativeSize;

    public string ItemId => _itemId;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }

        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Bind(string itemId, string displayName, int count, string iconLocation, Action<string> onClick, bool showCount = true, bool setNativeSize = false)
    {
        _itemId = itemId;
        _onClick = onClick;
        _setNativeSize = setNativeSize;
        if (nameLabel != null)
        {
            nameLabel.text = string.IsNullOrEmpty(displayName) ? itemId : displayName;
        }

        if (countLabel != null)
        {
            countLabel.gameObject.SetActive(showCount);
            if (showCount)
            {
                countLabel.text = "x" + Mathf.Max(0, count);
            }
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
        }

        SetSelected(false);
        if (isActiveAndEnabled)
        {
            StartCoroutine(LoadIcon(iconLocation));
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectVisual != null)
        {
            selectVisual.SetActive(selected);
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
            if (_setNativeSize)
            {
                iconImage.SetNativeSize();
            }
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