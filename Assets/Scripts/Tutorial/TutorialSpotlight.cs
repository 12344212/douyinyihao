using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 教程聚光：遮罩挖洞 + 文案。战斗拖拽目标改为 RectTransform，不依赖 Combat/Enemy。
/// 挂在教程 Canvas 上，节点需在 Editor 中拖好。
/// </summary>
public class TutorialSpotlight : MonoBehaviour
{
    [SerializeField] RectTransform canvasRect;
    [SerializeField] RectTransform maskTop;
    [SerializeField] RectTransform maskBottom;
    [SerializeField] RectTransform maskLeft;
    [SerializeField] RectTransform maskRight;
    [SerializeField] RectTransform hight;
    [SerializeField] RectTransform textBg;
    [SerializeField] RectTransform textFrame;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] float screenMargin = 24f;
    [SerializeField] float breatheScale = 1.12f;
    [SerializeField] float breatheHalfDuration = 0.55f;
    [SerializeField] float dragDuration = 1.1f;

    Canvas rootCanvas;
    Vector2 textBgHomeSize = new Vector2(640f, 180f);
    Vector2 textFrameHomeSize = new Vector2(600f, 140f);
    Vector2 textFrameHomePos;
    bool textHomeReady;
    bool dragMode;
    RectTransform dragFrom;
    RectTransform dragTo;
    Vector2 dragLocalFrom;
    Vector2 dragLocalTo;
    float dragTime;
    Tween breatheTween;

    void Awake()
    {
        EnsureCanvas();
        CacheTextHome();
        Hide();
    }

    void OnDisable()
    {
        KillBreathe();
    }

    void Update()
    {
        if (!dragMode)
        {
            return;
        }

        RefreshDragHole();
        AdvanceDragMarker();
    }

    public void Hide()
    {
        dragMode = false;
        dragFrom = null;
        dragTo = null;
        KillBreathe();
        SetActive(maskTop, false);
        SetActive(maskBottom, false);
        SetActive(maskLeft, false);
        SetActive(maskRight, false);
        SetActive(hight, false);
        SetActive(textBg, false);
        SetActive(textFrame, false);
        if (bodyText != null)
        {
            bodyText.text = string.Empty;
        }

        gameObject.SetActive(false);
    }

    public void ShowClick(RectTransform target, string body)
    {
        EnsureCanvas();
        CacheTextHome();
        dragMode = false;
        dragFrom = null;
        dragTo = null;
        KillBreathe();
        gameObject.SetActive(true);
        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;
        }

        if (!TryLocalRect(target, out Rect hole))
        {
            hole = Box(0f, 0f, 200f, 200f);
        }

        PlaceMasks(hole);
        Place(hight, ClampHighlight(hole));
        PlaceText(hole);
        StartBreathe();
    }

    /// <summary>拖拽指引：from / to 均为 UI Rect；无 Combat 依赖。</summary>
    public void ShowDrag(RectTransform from, RectTransform to, string body)
    {
        EnsureCanvas();
        CacheTextHome();
        dragMode = true;
        dragFrom = from;
        dragTo = to;
        dragTime = 0f;
        KillBreathe();
        gameObject.SetActive(true);
        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;
        }

        RefreshDragHole();
        AdvanceDragMarker();
    }

    void RefreshDragHole()
    {
        bool hasFrom = TryLocalRect(dragFrom, out Rect fromRect);
        bool hasTo = TryLocalRect(dragTo, out Rect toRect);
        if (!hasFrom && !hasTo)
        {
            return;
        }

        Rect hole = hasFrom && hasTo ? Union(fromRect, toRect) : (hasFrom ? fromRect : toRect);
        PlaceMasks(hole);
        Place(hight, ClampHighlight(hasFrom ? fromRect : hole));
        PlaceText(hole);
        if (hasFrom)
        {
            dragLocalFrom = fromRect.center;
        }

        if (hasTo)
        {
            dragLocalTo = toRect.center;
        }
    }

    void PlaceMasks(Rect hole)
    {
        EnsureCanvas();
        if (canvasRect == null)
        {
            return;
        }

        Rect canvas = canvasRect.rect;
        float left = canvas.xMin;
        float right = canvas.xMax;
        float bottom = canvas.yMin;
        float top = canvas.yMax;

        Place(maskBottom, Rect.MinMaxRect(left, bottom, right, hole.yMin));
        Place(maskTop, Rect.MinMaxRect(left, hole.yMax, right, top));
        Place(maskLeft, Rect.MinMaxRect(left, hole.yMin, hole.xMin, hole.yMax));
        Place(maskRight, Rect.MinMaxRect(hole.xMax, hole.yMin, right, hole.yMax));
    }

    void PlaceText(Rect hole)
    {
        EnsureCanvas();
        CacheTextHome();
        if (textBg == null || canvasRect == null)
        {
            return;
        }

        Rect canvas = canvasRect.rect;
        Rect bounds = canvas;
        bounds.xMin += screenMargin;
        bounds.xMax -= screenMargin;
        bounds.yMin += screenMargin;
        bounds.yMax -= screenMargin;

        Rect bg = PlaceTextRect(hole, textBgHomeSize.x, textBgHomeSize.y, bounds);
        textBg.anchorMin = textBg.anchorMax = new Vector2(0.5f, 0.5f);
        textBg.pivot = new Vector2(0.5f, 0.5f);
        textBg.sizeDelta = textBgHomeSize;
        textBg.anchoredPosition = bg.center - canvas.center;
        textBg.gameObject.SetActive(true);

        if (textFrame != null)
        {
            textFrame.anchorMin = textFrame.anchorMax = new Vector2(0.5f, 0.5f);
            textFrame.pivot = new Vector2(0.5f, 0.5f);
            textFrame.sizeDelta = textFrameHomeSize;
            textFrame.anchoredPosition = textFrameHomePos + (bg.center - canvas.center);
            textFrame.gameObject.SetActive(true);
        }
    }

    void CacheTextHome()
    {
        if (textHomeReady)
        {
            return;
        }

        if (textBg != null)
        {
            Vector2 size = textBg.sizeDelta;
            if (size.x > 8f && size.y > 8f)
            {
                textBgHomeSize = size;
            }
        }

        if (textFrame != null)
        {
            Vector2 size = textFrame.sizeDelta;
            if (size.x > 8f && size.y > 8f)
            {
                textFrameHomeSize = size;
            }

            textFrameHomePos = textFrame.anchoredPosition;
        }

        textHomeReady = true;
    }

    Rect PlaceTextRect(Rect hole, float width, float height, Rect bounds)
    {
        const float gap = 28f;
        Rect below = Box(hole.center.x, hole.yMin - gap - height * 0.5f, width, height);
        Rect above = Box(hole.center.x, hole.yMax + gap + height * 0.5f, width, height);
        below = KeepInsideX(below, bounds);
        above = KeepInsideX(above, bounds);

        bool belowFits = below.yMin >= bounds.yMin && !below.Overlaps(hole);
        bool aboveFits = above.yMax <= bounds.yMax && !above.Overlaps(hole);
        if (belowFits)
        {
            return below;
        }

        if (aboveFits)
        {
            return above;
        }

        float spaceBelow = hole.yMin - bounds.yMin;
        float spaceAbove = bounds.yMax - hole.yMax;
        Rect chosen = spaceAbove >= spaceBelow ? above : below;
        chosen = KeepInsideX(chosen, bounds);
        if (chosen.center.y >= hole.center.y)
        {
            chosen.y = Mathf.Min(hole.yMax + gap, bounds.yMax - chosen.height);
        }
        else
        {
            chosen.y = Mathf.Max(bounds.yMin, hole.yMin - gap - chosen.height);
        }

        return chosen;
    }

    static Rect KeepInsideX(Rect box, Rect bounds)
    {
        if (box.width > bounds.width)
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

    void AdvanceDragMarker()
    {
        if (hight == null)
        {
            return;
        }

        dragTime += Time.unscaledDeltaTime;
        float dur = Mathf.Max(0.2f, dragDuration);
        float t = Mathf.Repeat(dragTime, dur) / dur;
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector2 pos = Vector2.Lerp(dragLocalFrom, dragLocalTo, eased);
        hight.gameObject.SetActive(true);
        hight.anchorMin = hight.anchorMax = new Vector2(0.5f, 0.5f);
        hight.pivot = new Vector2(0.5f, 0.5f);
        hight.sizeDelta = new Vector2(180f, 180f);
        hight.anchoredPosition = pos - canvasRect.rect.center;
    }

    void StartBreathe()
    {
        KillBreathe();
        if (hight == null)
        {
            return;
        }

        hight.localScale = Vector3.one;
        breatheTween = hight
            .DOScale(breatheScale, breatheHalfDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    void KillBreathe()
    {
        if (breatheTween != null && breatheTween.IsActive())
        {
            breatheTween.Kill();
        }

        breatheTween = null;
        if (hight != null)
        {
            hight.DOKill(false);
        }
    }

    bool TryLocalRect(RectTransform target, out Rect local)
    {
        local = default;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Canvas host = target.GetComponentInParent<Canvas>();
        Camera worldCam = null;
        if (host != null && host.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            worldCam = host.worldCamera;
        }

        return CornersToLocal(corners, worldCam, out local);
    }

    bool CornersToLocal(Vector3[] worldCorners, Camera worldCam, out Rect local)
    {
        local = default;
        EnsureCanvas();
        if (canvasRect == null)
        {
            return false;
        }

        Camera eventCam = EventCamera();
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCam, worldCorners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCam, out Vector2 point))
            {
                return false;
            }

            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        local = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        if (local.width < 8f)
        {
            float cx = local.center.x;
            local.xMin = cx - 80f;
            local.xMax = cx + 80f;
        }

        if (local.height < 8f)
        {
            float cy = local.center.y;
            local.yMin = cy - 80f;
            local.yMax = cy + 80f;
        }

        return true;
    }

    Camera EventCamera()
    {
        EnsureCanvas();
        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (rootCanvas.worldCamera != null)
        {
            return rootCanvas.worldCamera;
        }

        return Camera.main;
    }

    void EnsureCanvas()
    {
        if (canvasRect == null)
        {
            canvasRect = transform as RectTransform;
        }

        if (rootCanvas == null && canvasRect != null)
        {
            Canvas c = canvasRect.GetComponentInParent<Canvas>();
            rootCanvas = c != null ? c.rootCanvas : null;
        }
    }

    static Rect ClampHighlight(Rect hole)
    {
        float w = Mathf.Clamp(hole.width, 120f, 320f);
        float h = Mathf.Clamp(hole.height, 120f, 320f);
        return Box(hole.center.x, hole.center.y, w, h);
    }

    void Place(RectTransform rt, Rect area)
    {
        if (rt == null)
        {
            return;
        }

        if (area.width < 1f || area.height < 1f)
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

    static Rect Box(float centerX, float centerY, float width, float height)
    {
        return new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
    }

    static Rect Union(Rect a, Rect b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin),
            Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax),
            Mathf.Max(a.yMax, b.yMax));
    }

    static void SetActive(RectTransform rt, bool on)
    {
        if (rt != null)
        {
            rt.gameObject.SetActive(on);
        }
    }
}
