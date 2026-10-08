using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 短时提示条（Toast）。挂在 TipPrefab 上，Awake 时把所在 Canvas 根节点移到 DontDestroyOnLoad，全局只有一个。
/// 调用 TipPanelUI.ShowTip("...")：顶部居中淡入并滑入，约 2 秒后自动消失，点击立即关闭。
/// 连续调用会替换当前文字并重新计时。
/// </summary>
[DisallowMultipleComponent]
public class TipPanelUI : MonoBehaviour, IPointerClickHandler
{
    public static TipPanelUI Instance { get; private set; }

    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float autoHideSeconds = 2f;
    [SerializeField] private float hideDuration = 0.2f;
    [SerializeField] private float slideDistance = 40f;

    private RectTransform _rt;
    private Vector2 _basePos;
    private bool _captured;
    private Sequence _seq;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 再次进入 MainScene 时会再生成一份，保留先创建的常驻实例
            GameObject root = transform.root.gameObject;
            if (Instance.transform.root.gameObject != root)
            {
                Destroy(root);
            }
            else
            {
                Destroy(gameObject);
            }

            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        _rt = transform as RectTransform;
        CaptureBase();
        SetHidden();
    }

    private void OnDestroy()
    {
        Kill();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>显示一条英文提示。没有实例时只打警告（不要在这里查找或创建）。</summary>
    public static void ShowTip(string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[TipPanelUI] No instance. Open MainScene (or enter it from LoadScene) first. message=" + message);
            return;
        }

        Instance.Show(message);
    }

    public void Show(string message)
    {
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }

        gameObject.SetActive(true);
        CaptureBase();
        Play(autoHide: true);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        Play(autoHide: false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Hide();
    }

    private void Play(bool autoHide)
    {
        Kill();
        if (_rt != null)
        {
            _rt.anchoredPosition = _basePos + new Vector2(0f, slideDistance);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        float show = Mathf.Max(0.05f, showDuration);
        _seq = DOTween.Sequence().SetUpdate(true);
        if (canvasGroup != null)
        {
            _seq.Join(DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 1f, show).SetEase(Ease.OutQuad));
        }

        if (_rt != null)
        {
            Vector2 target = _basePos;
            _seq.Join(DOTween.To(() => _rt.anchoredPosition, v => _rt.anchoredPosition = v, target, show).SetEase(Ease.OutBack));
        }

        if (autoHide)
        {
            _seq.AppendInterval(Mathf.Max(0.2f, autoHideSeconds));
        }

        AppendHide(_seq);
        _seq.OnComplete(SetHidden);
    }

    private void AppendHide(Sequence seq)
    {
        float hide = Mathf.Max(0.05f, hideDuration);
        bool joined = false;
        if (canvasGroup != null)
        {
            seq.Append(DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 0f, hide).SetEase(Ease.InQuad));
            joined = true;
        }

        if (_rt != null)
        {
            Vector2 target = _basePos + new Vector2(0f, slideDistance * 0.6f);
            Tween move = DOTween.To(() => _rt.anchoredPosition, v => _rt.anchoredPosition = v, target, hide).SetEase(Ease.InQuad);
            if (joined)
            {
                seq.Join(move);
            }
            else
            {
                seq.Append(move);
            }
        }
    }

    private void SetHidden()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (_rt != null && _captured)
        {
            _rt.anchoredPosition = _basePos;
        }

        gameObject.SetActive(false);
    }

    private void CaptureBase()
    {
        if (_captured || _rt == null)
        {
            return;
        }

        _basePos = _rt.anchoredPosition;
        _captured = true;
    }

    private void Kill()
    {
        if (_seq != null && _seq.IsActive())
        {
            _seq.Kill();
        }

        _seq = null;
    }
}