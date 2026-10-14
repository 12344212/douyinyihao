using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 教程表现层：四向遮罩（上/下/左/右四条暗色 Image 围出镂空）+ 呼吸高亮框 + 对话框（立绘 + 英文文本）。
/// 节点全部预先摆在场景里（Editor 工具生成纯色占位），这里只负责按目标 Rect 摆放、缩放与显隐。
/// 支持目标与教程在不同 Canvas（Overlay / Screen Space Camera 混用），每帧跟随目标位置。
/// 目标也可以是世界物体（敌人、3D 骰子）：按 Collider / Renderer 包围盒经 Camera.main 投影到屏幕。
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    [Header("根节点")]
    [Tooltip("教程画布区域（全屏拉伸），遮罩坐标以它为参照")]
    [SerializeField] private RectTransform canvasRect;
    [Tooltip("遮罩/高亮/对话框的父节点，隐藏时整体关闭")]
    [SerializeField] private GameObject overlayRoot;

    [Header("全屏拦截")]
    [Tooltip("透明全屏 Image（挡射线），教程进行中且没有显示步骤时打开，防止玩家点开其它面板")]
    [SerializeField] private GameObject blocker;

    [Header("四向遮罩")]
    [SerializeField] private RectTransform maskTop;
    [SerializeField] private RectTransform maskBottom;
    [SerializeField] private RectTransform maskLeft;
    [SerializeField] private RectTransform maskRight;

    [Header("高亮框")]
    [SerializeField] private RectTransform ring;
    [SerializeField] private float ringExtra = 12f;
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseHalfDuration = 0.55f;

    [Header("对话框")]
    [SerializeField] private RectTransform dialog;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("“Tap to continue” 提示，仅可点继续的步骤显示")]
    [SerializeField] private GameObject tapHint;
    [SerializeField] private float dialogGap = 32f;
    [SerializeField] private float screenMargin = 24f;

    /// <summary>点击遮罩（非镂空区域）时触发。</summary>
    public event Action MaskTapped;

    private Transform _target;
    private float _padding;
    private bool _showing;
    private Vector2 _dialogSize = new Vector2(920f, 280f);
    private bool _dialogSizeReady;
    private Sprite _placeholderPortrait;
    private Color _placeholderColor = Color.white;
    private bool _placeholderReady;
    private Tween _pulse;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly System.Collections.Generic.HashSet<UnityEngine.Object> _blockRequests = new System.Collections.Generic.HashSet<UnityEngine.Object>();
    private readonly Vector2[] _screen = new Vector2[4];

    public bool IsShowing => _showing;

    private void Awake()
    {
        if (canvasRect == null)
        {
            canvasRect = transform as RectTransform;
        }

        CacheDefaults();
        RefreshBlocker();
    }

    /// <summary>
    /// 请求 / 取消全屏拦截（按请求者计数，同一面板上的多个 TutorialManager 共用）。
    /// 有请求时：步骤未显示 → 拦截层打开；步骤显示中 → 由四向遮罩拦截（拦截层关闭，否则会挡住镂空）。
    /// </summary>
    public void RequestBlock(UnityEngine.Object owner, bool on)
    {
        if (owner == null)
        {
            return;
        }

        if (on)
        {
            _blockRequests.Add(owner);
        }
        else
        {
            _blockRequests.Remove(owner);
        }

        RefreshBlocker();
    }

    public bool IsBlocking => blocker != null && blocker.activeSelf;

    private void RefreshBlocker()
    {
        if (blocker == null)
        {
            return;
        }

        bool on = _blockRequests.Count > 0 && !_showing;
        if (blocker.activeSelf != on)
        {
            blocker.SetActive(on);
        }
    }

    private void OnDisable()
    {
        KillPulse();
    }

    private void LateUpdate()
    {
        if (_showing)
        {
            Layout();
        }
    }

    /// <summary>显示一步。target 为空时全屏遮罩（纯文本）；可以是 UI（RectTransform）或世界物体。</summary>
    public void Show(Transform target, float padding, string text, Sprite portrait, bool showTapHint)
    {
        CacheDefaults();
        _target = target;
        _padding = padding;
        _showing = true;
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
        }

        if (bodyText != null)
        {
            bodyText.text = text ?? string.Empty;
        }

        if (portraitImage != null)
        {
            if (portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.color = Color.white;
                portraitImage.preserveAspect = true;
            }
            else
            {
                portraitImage.sprite = _placeholderPortrait;
                portraitImage.color = _placeholderColor;
            }
        }

        if (tapHint != null)
        {
            tapHint.SetActive(showTapHint);
        }

        Layout();
        StartPulse(target != null);
        RefreshBlocker();
    }

    public void Hide()
    {
        _showing = false;
        _target = null;
        KillPulse();
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        RefreshBlocker();
    }

    /// <summary>由 TutorialMaskStrip 调用。</summary>
    public void NotifyMaskTapped()
    {
        if (_showing)
        {
            MaskTapped?.Invoke();
        }
    }

    /// <summary>屏幕坐标是否落在当前目标（含 padding）内，用于没有 Button 的目标。</summary>
    public bool IsScreenPointInHole(Vector2 screenPoint)
    {
        if (!_showing || _target == null || !TryGetHole(_target, _padding, out Rect hole))
        {
            return false;
        }

        Camera cam = CanvasCamera(canvasRect);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 local))
        {
            return false;
        }

        return hole.Contains(local);
    }

    private void CacheDefaults()
    {
        if (!_dialogSizeReady && dialog != null)
        {
            Vector2 size = dialog.sizeDelta;
            if (size.x > 8f && size.y > 8f)
            {
                _dialogSize = size;
            }

            _dialogSizeReady = true;
        }

        if (!_placeholderReady && portraitImage != null)
        {
            _placeholderPortrait = portraitImage.sprite;
            _placeholderColor = portraitImage.color;
            _placeholderReady = true;
        }
    }

    private void Layout()
    {
        if (canvasRect == null)
        {
            return;
        }

        Rect canvas = canvasRect.rect;
        Rect hole = default;
        bool hasHole = _target != null && _target.gameObject.activeInHierarchy && TryGetHole(_target, _padding, out hole);
        if (!hasHole)
        {
            // 纯文本 / 目标暂不可见：整屏遮住
            Place(maskTop, canvas);
            SetActive(maskBottom, false);
            SetActive(maskLeft, false);
            SetActive(maskRight, false);
            SetActive(ring, false);
            PlaceDialog(Box(canvas.center.x, canvas.center.y - canvas.height * 0.18f, _dialogSize.x, _dialogSize.y), canvas);
            return;
        }

        // 洞限制在画布内，避免出现负尺寸遮罩
        hole = Rect.MinMaxRect(
            Mathf.Clamp(hole.xMin, canvas.xMin, canvas.xMax),
            Mathf.Clamp(hole.yMin, canvas.yMin, canvas.yMax),
            Mathf.Clamp(hole.xMax, canvas.xMin, canvas.xMax),
            Mathf.Clamp(hole.yMax, canvas.yMin, canvas.yMax));

        Place(maskTop, Rect.MinMaxRect(canvas.xMin, hole.yMax, canvas.xMax, canvas.yMax));
        Place(maskBottom, Rect.MinMaxRect(canvas.xMin, canvas.yMin, canvas.xMax, hole.yMin));
        Place(maskLeft, Rect.MinMaxRect(canvas.xMin, hole.yMin, hole.xMin, hole.yMax));
        Place(maskRight, Rect.MinMaxRect(hole.xMax, hole.yMin, canvas.xMax, hole.yMax));

        if (ring != null)
        {
            Rect r = Box(hole.center.x, hole.center.y, hole.width + ringExtra * 2f, hole.height + ringExtra * 2f);
            // 只改位置和尺寸，缩放交给呼吸动画
            ring.gameObject.SetActive(true);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.pivot = new Vector2(0.5f, 0.5f);
            ring.sizeDelta = r.size;
            ring.anchoredPosition = r.center - canvas.center;
        }

        PlaceDialog(ChooseDialogRect(hole, canvas), canvas);
    }

    /// <summary>目标（UI 四角 / 世界包围盒）→ 屏幕 → 教程画布本地坐标，得到镂空矩形。</summary>
    private bool TryGetHole(Transform target, float padding, out Rect local)
    {
        local = default;
        if (target == null || canvasRect == null)
        {
            return false;
        }

        Vector2 sMin;
        Vector2 sMax;
        RectTransform rt = target as RectTransform;
        bool ok = rt != null ? TryUiScreenRect(rt, out sMin, out sMax) : TryWorldScreenRect(target, out sMin, out sMax);
        if (!ok)
        {
            return false;
        }

        Camera selfCam = CanvasCamera(canvasRect);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        _screen[0] = sMin;
        _screen[1] = new Vector2(sMin.x, sMax.y);
        _screen[2] = sMax;
        _screen[3] = new Vector2(sMax.x, sMin.y);
        for (int i = 0; i < 4; i++)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, _screen[i], selfCam, out Vector2 p))
            {
                return false;
            }

            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        local = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
        return local.width > 1f && local.height > 1f;
    }

    /// <summary>UI 目标：四个世界角经所在根 Canvas 相机转屏幕坐标。</summary>
    private bool TryUiScreenRect(RectTransform rt, out Vector2 min, out Vector2 max)
    {
        rt.GetWorldCorners(_corners);
        Camera cam = CanvasCamera(rt);
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, _corners[i]);
            min = Vector2.Min(min, s);
            max = Vector2.Max(max, s);
        }

        return true;
    }

    /// <summary>世界目标：优先 Collider 包围盒（与点击区域一致），其次 Renderer；经 Camera.main 投影。</summary>
    private static bool TryWorldScreenRect(Transform t, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Bounds b;
        Collider c3 = t.GetComponentInChildren<Collider>();
        Collider2D c2 = c3 == null ? t.GetComponentInChildren<Collider2D>() : null;
        Renderer r = (c3 == null && c2 == null) ? t.GetComponentInChildren<Renderer>() : null;
        if (c3 != null)
        {
            b = c3.bounds;
        }
        else if (c2 != null)
        {
            b = c2.bounds;
        }
        else if (r != null)
        {
            b = r.bounds;
        }
        else
        {
            return false;
        }

        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        Vector3 c = b.center;
        Vector3 e = b.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            Vector3 s = cam.WorldToScreenPoint(corner);
            if (s.z < 0f)
            {
                return false;
            }

            min = Vector2.Min(min, s);
            max = Vector2.Max(max, s);
        }

        return true;
    }

    /// <summary>取 RectTransform 所在根 Canvas 的渲染相机；Overlay 返回 null。每次现取（PanelMotion 可能运行时切换渲染模式）。</summary>
    private static Camera CanvasCamera(RectTransform rt)
    {
        if (rt == null)
        {
            return null;
        }

        Canvas c = rt.GetComponentInParent<Canvas>();
        if (c == null)
        {
            return null;
        }

        Canvas root = c.rootCanvas;
        if (root.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return root.worldCamera != null ? root.worldCamera : Camera.main;
    }

    private Rect ChooseDialogRect(Rect hole, Rect canvas)
    {
        Rect bounds = Rect.MinMaxRect(canvas.xMin + screenMargin, canvas.yMin + screenMargin, canvas.xMax - screenMargin, canvas.yMax - screenMargin);
        float w = _dialogSize.x;
        float h = _dialogSize.y;
        Rect below = KeepInsideX(Box(hole.center.x, hole.yMin - dialogGap - h * 0.5f, w, h), bounds);
        Rect above = KeepInsideX(Box(hole.center.x, hole.yMax + dialogGap + h * 0.5f, w, h), bounds);
        if (below.yMin >= bounds.yMin)
        {
            return below;
        }

        if (above.yMax <= bounds.yMax)
        {
            return above;
        }

        // 上下都放不下：放在空间更大的一侧并贴边
        bool useAbove = bounds.yMax - hole.yMax >= hole.yMin - bounds.yMin;
        Rect chosen = useAbove ? above : below;
        chosen.y = useAbove ? bounds.yMax - h : bounds.yMin;
        return chosen;
    }

    private void PlaceDialog(Rect area, Rect canvas)
    {
        if (dialog == null)
        {
            return;
        }

        dialog.gameObject.SetActive(true);
        dialog.anchorMin = dialog.anchorMax = new Vector2(0.5f, 0.5f);
        dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.sizeDelta = _dialogSize;
        dialog.anchoredPosition = area.center - canvas.center;
    }

    private void Place(RectTransform rt, Rect area)
    {
        if (rt == null)
        {
            return;
        }

        if (area.width < 0.5f || area.height < 0.5f)
        {
            rt.gameObject.SetActive(false);
            return;
        }

        rt.gameObject.SetActive(true);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = area.size;
        rt.anchoredPosition = area.center - canvasRect.rect.center;
    }

    private void StartPulse(bool on)
    {
        KillPulse();
        if (ring == null || !on)
        {
            return;
        }

        ring.localScale = Vector3.one;
        _pulse = ring.DOScale(pulseScale, pulseHalfDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(ring.gameObject);
    }

    private void KillPulse()
    {
        if (_pulse != null && _pulse.IsActive())
        {
            _pulse.Kill();
        }

        _pulse = null;
        if (ring != null)
        {
            ring.localScale = Vector3.one;
        }
    }

    private static Rect KeepInsideX(Rect box, Rect bounds)
    {
        if (box.width >= bounds.width)
        {
            box.x = bounds.center.x - box.width * 0.5f;
            return box;
        }

        if (box.xMin < bounds.xMin)
        {
            box.x += bounds.xMin - box.xMin;
        }

        if (box.xMax > bounds.xMax)
        {
            box.x -= box.xMax - bounds.xMax;
        }

        return box;
    }

    private static Rect Box(float cx, float cy, float w, float h)
    {
        return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
    }

    private static void SetActive(Component c, bool on)
    {
        if (c != null)
        {
            c.gameObject.SetActive(on);
        }
    }
}
