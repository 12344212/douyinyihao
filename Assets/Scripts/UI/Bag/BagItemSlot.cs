using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包格子：图标、数量。选中后才显示 Select。点击通知面板刷新 ItemData。
/// Icon load is deferred safely: Bind may run while a parent is inactive (e.g. Equit listRoot);
/// OnEnable retries the pending YooAsset icon so the first open is never blank.
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
    private string _pendingIconLocation;
    private int _iconLoadGen;
    private Coroutine _iconLoad;

    public string ItemId => _itemId;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }

        SetSelected(false);
    }

    private void OnEnable()
    {
        // Bind often runs under an inactive parent (listRoot). Retry pending load when hierarchy activates.
        if (!string.IsNullOrEmpty(_pendingIconLocation))
        {
            BeginIconLoad(_pendingIconLocation);
        }
    }

    private void OnDisable()
    {
        if (_iconLoad != null)
        {
            StopCoroutine(_iconLoad);
            _iconLoad = null;
        }
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
        _pendingIconLocation = iconLocation;
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
            BeginIconLoad(iconLocation);
        }
        else
        {
            // Invalidate in-flight loads; OnEnable will start fresh when active.
            _iconLoadGen++;
            if (_iconLoad != null)
            {
                StopCoroutine(_iconLoad);
                _iconLoad = null;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectVisual != null)
        {
            selectVisual.SetActive(selected);
        }
    }

    private void BeginIconLoad(string location)
    {
        _iconLoadGen++;
        if (_iconLoad != null)
        {
            StopCoroutine(_iconLoad);
            _iconLoad = null;
        }

        _iconLoad = StartCoroutine(LoadIcon(location, _iconLoadGen));
    }

    private IEnumerator LoadIcon(string location, int gen)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (iconImage == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            if (gen == _iconLoadGen)
            {
                _iconLoad = null;
            }

            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (gen != _iconLoadGen)
        {
            yield break;
        }

        if (sprite != null && iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
            if (_setNativeSize)
            {
                iconImage.SetNativeSize();
            }
        }

        _iconLoad = null;
    }

    private void HandleClick()
    {
        if (!string.IsNullOrEmpty(_itemId))
        {
            _onClick?.Invoke(_itemId);
        }
    }
}
