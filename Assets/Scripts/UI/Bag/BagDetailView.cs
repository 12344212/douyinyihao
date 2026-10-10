using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包 ItemData 区块：名字、介绍、数量。非整屏弹层，占布局中一块。
/// </summary>
public class BagDetailView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descLabel;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private Button useButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private string _itemId;
    private string _iconLocation;
    private Coroutine _iconLoad;

    private void Awake()
    {
        EnsureDescWrap();
        if (useButton != null)
        {
            useButton.onClick.AddListener(OnUse);
        }
    }

    private void EnsureDescWrap()
    {
        if (descLabel == null)
        {
            return;
        }

        descLabel.enableWordWrapping = true;
        descLabel.overflowMode = TextOverflowModes.Overflow;
    }

    private void OnDestroy()
    {
        if (useButton != null)
        {
            useButton.onClick.RemoveListener(OnUse);
        }
    }

    public void Show(string itemId, string displayName, string description, int count, string iconLocation = null)
    {
        _itemId = itemId;
        _iconLocation = iconLocation;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (nameLabel != null)
        {
            nameLabel.text = displayName ?? string.Empty;
        }

        if (descLabel != null)
        {
            descLabel.text = description ?? string.Empty;
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

        if (_iconLoad != null)
        {
            StopCoroutine(_iconLoad);
            _iconLoad = null;
        }

        if (isActiveAndEnabled)
        {
            _iconLoad = StartCoroutine(LoadIcon(iconLocation));
        }
    }

    public void Clear()
    {
        _itemId = null;
        _iconLocation = null;
        if (nameLabel != null)
        {
            nameLabel.text = string.Empty;
        }

        if (descLabel != null)
        {
            descLabel.text = string.Empty;
        }

        if (countLabel != null)
        {
            countLabel.text = string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
        }
    }

    /// <summary>兼容旧调用：侧栏不清空节点，只清内容。</summary>
    public void Hide()
    {
        Clear();
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
        if (sprite != null && iconImage != null && _iconLocation == location)
        {
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
        }

        _iconLoad = null;
    }

    private void OnUse()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            return;
        }

        if (!ItemEffects.TryUse(_itemId))
        {
            TipPanelUI.ShowTip("Can't use this yet.");
        }
    }
}