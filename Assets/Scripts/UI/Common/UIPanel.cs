using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用面板组件：挂在面板根节点（全屏，子节点 Dim 暗色背景 + Window 窗口）。
/// Show / Hide 带 DOTween 弹出动画（窗口缩放 + 整体淡入淡出），之后所有功能面板都复用它。
/// Close 按钮只发出 CloseRequested，由 MainUIController 统一关闭（保证同一时间只开一个面板）；无人监听时自己 Hide。
/// </summary>
[DisallowMultipleComponent]
public class UIPanel : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform content;

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float hideDuration = 0.15f;
    [SerializeField] private float hiddenScale = 0.85f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    /// <summary>点击 Close 按钮（或调用 RequestClose）时触发。</summary>
    public event Action<UIPanel> CloseRequested;
    /// <summary>弹出动画结束后触发。</summary>
    public event Action<UIPanel> Shown;
    /// <summary>面板隐藏（动画结束或立即隐藏）后触发。</summary>
    public event Action<UIPanel> Hidden;

    public bool IsOpen { get; private set; }
    public RectTransform Window => window;
    /// <summary>窗口内容区（后续模块往这里放具体 UI）。</summary>
    public RectTransform Content => content;
    public TMP_Text TitleText => titleText;

    private Sequence _tween;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(RequestClose);
        }
    }

    private void OnDestroy()
    {
        KillTween();
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(RequestClose);
        }
    }

    private void OnDisable()
    {
        KillTween();
    }

    /// <summary>显示面板（淡入 + 窗口从 hiddenScale 弹到 1）。</summary>
    public void Show()
    {
        KillTween();
        IsOpen = true;
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (window != null)
        {
            window.localScale = Vector3.one * hiddenScale;
        }

        if (canvasGroup == null && window == null)
        {
            Shown?.Invoke(this);
            return;
        }

        float d = Mathf.Max(0.01f, showDuration);
        _tween = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
        {
            _tween.Insert(0f, DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 1f, d).SetEase(Ease.OutQuad));
        }

        if (window != null)
        {
            _tween.Insert(0f, window.DOScale(1f, d).SetEase(showEase));
        }

        _tween.OnComplete(() =>
        {
            _tween = null;
            Shown?.Invoke(this);
        });
    }

    /// <summary>隐藏面板（淡出 + 缩小），结束后 SetActive(false)。</summary>
    public void Hide(Action onHidden = null)
    {
        if (!gameObject.activeSelf)
        {
            IsOpen = false;
            Hidden?.Invoke(this);
            onHidden?.Invoke();
            return;
        }

        KillTween();
        IsOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
        }

        if (canvasGroup == null && window == null)
        {
            FinishHide();
            onHidden?.Invoke();
            return;
        }

        float d = Mathf.Max(0.01f, hideDuration);
        _tween = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
        {
            _tween.Insert(0f, DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 0f, d).SetEase(Ease.InQuad));
        }

        if (window != null)
        {
            _tween.Insert(0f, window.DOScale(hiddenScale, d).SetEase(hideEase));
        }

        _tween.OnComplete(() =>
        {
            _tween = null;
            FinishHide();
            onHidden?.Invoke();
        });
    }

    /// <summary>立即隐藏（无动画），用于切换面板或初始化。</summary>
    public void HideImmediate()
    {
        KillTween();
        IsOpen = false;
        if (gameObject.activeSelf)
        {
            FinishHide();
        }
    }

    /// <summary>请求关闭：有监听者（MainUIController）时交给它处理，否则自己 Hide。</summary>
    public void RequestClose()
    {
        if (CloseRequested != null)
        {
            CloseRequested.Invoke(this);
        }
        else
        {
            Hide();
        }
    }

    private void FinishHide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
        }

        if (window != null)
        {
            window.localScale = Vector3.one;
        }

        gameObject.SetActive(false);
        Hidden?.Invoke(this);
    }

    private void KillTween()
    {
        if (_tween != null && _tween.IsActive())
        {
            _tween.Kill();
        }

        _tween = null;
    }
}