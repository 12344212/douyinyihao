using UnityEngine;

/// <summary>
/// 顶栏安全区适配（参考项目 SafeAreaTopBar 的思路，改为直接用锚点）：
/// 把 Screen.safeArea 换算成归一化锚点，顶栏左右边缘贴住安全区的左右边界（横屏刘海 / 挖孔 / 圆角），
/// 顶边贴住安全区上边界；高度和左右留白沿用编辑器里摆好的 sizeDelta / anchoredPosition。
/// 要求：父节点铺满整个屏幕（例如 Canvas 根节点，Screen Space - Camera / Overlay 均可，与 CanvasScaler 的缩放无关）。
/// 在安全区、分辨率或屏幕方向变化时自动重新计算（含 Device Simulator 切换机型）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaTopBar : MonoBehaviour
{
    [SerializeField] private bool applyLeft = true;
    [SerializeField] private bool applyRight = true;
    [SerializeField] private bool applyTop = true;

    private RectTransform _rt;
    private Vector2 _baseSizeDelta;
    private Vector2 _baseAnchoredPosition;
    private bool _captured;

    private Rect _appliedSafe;
    private Vector2Int _appliedScreen;
    private ScreenOrientation _appliedOrientation;

    private void Awake()
    {
        Capture();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (_appliedSafe != Screen.safeArea
            || _appliedScreen.x != Screen.width
            || _appliedScreen.y != Screen.height
            || _appliedOrientation != Screen.orientation)
        {
            Apply();
        }
    }

    private void Capture()
    {
        if (_captured)
        {
            return;
        }

        _rt = (RectTransform)transform;
        // 编辑器里按“顶部横向拉伸”摆放：sizeDelta.x 为左右留白（负数），sizeDelta.y 为高度
        _baseSizeDelta = _rt.sizeDelta;
        _baseAnchoredPosition = _rt.anchoredPosition;
        _captured = true;
    }

    /// <summary>按当前安全区重新计算锚点。</summary>
    public void Apply()
    {
        Capture();
        int width = Screen.width;
        int height = Screen.height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Rect safe = Screen.safeArea;
        float xMin = applyLeft ? Mathf.Clamp01(safe.xMin / width) : 0f;
        float xMax = applyRight ? Mathf.Clamp01(safe.xMax / width) : 1f;
        float top = applyTop ? Mathf.Clamp01(safe.yMax / height) : 1f;

        _rt.anchorMin = new Vector2(xMin, top);
        _rt.anchorMax = new Vector2(xMax, top);
        _rt.pivot = new Vector2(0.5f, 1f);
        _rt.sizeDelta = _baseSizeDelta;
        _rt.anchoredPosition = _baseAnchoredPosition;

        _appliedSafe = safe;
        _appliedScreen = new Vector2Int(width, height);
        _appliedOrientation = Screen.orientation;
    }
}