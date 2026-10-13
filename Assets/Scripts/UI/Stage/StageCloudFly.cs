using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StagePanel yun1/yun2/yun3: on panel enter each cloud starts at a random on-screen X/Y,
/// then drifts right at constant speed. Loops only after the entire sprite (rect width,
/// pivot-aware) is outside the canvas/view. Respawn on the left starts fully off-screen.
/// Soft bob while flying. No land/hold.
/// </summary>
public class StageCloudFly : MonoBehaviour
{
    [SerializeField] private RectTransform[] clouds;
    [Tooltip("Usually SlotRow. Used for Y band; horizontal edges use the canvas/view.")]
    [SerializeField] private RectTransform boundsRoot;

    [Header("Motion")]
    [Tooltip("Anchored-position units per second. Duration = distance / speed.")]
    [SerializeField] private Vector2 speedRange = new Vector2(60f, 110f);
    [SerializeField] private Vector2 restDelayRange = new Vector2(0.25f, 1.1f);
    [SerializeField] private Vector2 initialDelayRange = new Vector2(0f, 0.35f);
    [SerializeField] private float edgePadding = 90f;

    [Header("Y Band (local anchored, relative to boundsRoot center)")]
    [SerializeField] private Vector2 yRange = new Vector2(-70f, 110f);

    [Header("Soft Bob While Flying")]
    [SerializeField] private float bobAmplitude = 5f;
    [SerializeField] private Vector2 bobPeriodRange = new Vector2(2.2f, 3.6f);

    private Coroutine[] _loops;
    private Vector2[] _restorePos;
    /// <summary>True for first pass after panel enter: start at random on-screen X.</summary>
    private bool[] _useOnScreenStart;

    private void OnEnable()
    {
        StartMotion();
    }

    private void OnDisable()
    {
        StopMotion(true);
    }

    private void OnDestroy()
    {
        StopMotion(false);
    }

    public void SetClouds(RectTransform[] next, RectTransform bounds)
    {
        StopMotion(true);
        clouds = next;
        boundsRoot = bounds;
        if (isActiveAndEnabled)
        {
            StartMotion();
        }
    }

    public void StartMotion()
    {
        StopMotion(false);
        if (clouds == null || clouds.Length == 0)
        {
            return;
        }

        _restorePos = new Vector2[clouds.Length];
        _loops = new Coroutine[clouds.Length];
        _useOnScreenStart = new bool[clouds.Length];
        for (int i = 0; i < clouds.Length; i++)
        {
            RectTransform rt = clouds[i];
            if (rt == null)
            {
                continue;
            }

            _restorePos[i] = rt.anchoredPosition;
            _useOnScreenStart[i] = true;
            Image img = rt.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
            }

            float delay = Random.Range(initialDelayRange.x, initialDelayRange.y);
            _loops[i] = StartCoroutine(CloudLoop(i, delay));
        }
    }

    public void StopMotion(bool restore)
    {
        if (_loops != null)
        {
            for (int i = 0; i < _loops.Length; i++)
            {
                if (_loops[i] != null)
                {
                    StopCoroutine(_loops[i]);
                }
            }

            _loops = null;
        }

        _useOnScreenStart = null;

        if (clouds == null)
        {
            return;
        }

        for (int i = 0; i < clouds.Length; i++)
        {
            RectTransform rt = clouds[i];
            if (rt == null)
            {
                continue;
            }

            rt.DOKill();
            if (restore && _restorePos != null && i < _restorePos.Length)
            {
                rt.anchoredPosition = _restorePos[i];
            }

            rt.localEulerAngles = Vector3.zero;
            if (!rt.gameObject.activeSelf)
            {
                rt.gameObject.SetActive(true);
            }
        }
    }

    private IEnumerator CloudLoop(int index, float initialDelay)
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(initialDelay);
        }

        RectTransform rt = clouds[index];
        while (enabled && rt != null)
        {
            RectTransform measure = boundsRoot != null ? boundsRoot : rt.parent as RectTransform;
            while (enabled && measure != null && measure.rect.width < 8f)
            {
                yield return null;
                measure = boundsRoot != null ? boundsRoot : rt.parent as RectTransform;
            }

            RectTransform localSpace = rt.parent as RectTransform;
            if (localSpace == null)
            {
                localSpace = measure != null ? measure : rt;
            }

            float viewMinX;
            float viewMaxX;
            if (!TryGetViewLocalX(localSpace, out viewMinX, out viewMaxX))
            {
                float halfW = measure != null ? measure.rect.width * 0.5f : 600f;
                viewMinX = -halfW;
                viewMaxX = halfW;
            }

            // Full sprite width in local units; extents from pivot (not assuming center).
            float cloudW = Mathf.Abs(rt.rect.width * rt.localScale.x);
            if (cloudW < 1f)
            {
                cloudW = 400f;
            }

            float toLeft = cloudW * rt.pivot.x;
            float toRight = cloudW * (1f - rt.pivot.x);

            // Entire sprite past the right view edge (left edge clears viewMax).
            float endX = viewMaxX + toLeft + edgePadding;
            float startX;
            bool onScreenStart = _useOnScreenStart != null && index < _useOnScreenStart.Length && _useOnScreenStart[index];
            if (onScreenStart)
            {
                float xMin = viewMinX + toLeft + edgePadding * 0.25f;
                float xMax = viewMaxX - toRight - edgePadding * 0.25f;
                if (xMax < xMin)
                {
                    float mid = (viewMinX + viewMaxX) * 0.5f;
                    float span = Mathf.Max(40f, (viewMaxX - viewMinX) * 0.2f);
                    xMin = mid - span;
                    xMax = mid + span;
                }

                startX = Random.Range(xMin, xMax);
                _useOnScreenStart[index] = false;
            }
            else
            {
                // Entire sprite starts left of view (right edge clears viewMin).
                startX = viewMinX - toRight - edgePadding;
            }

            float baseY = Random.Range(yRange.x, yRange.y);

            rt.DOKill();
            rt.anchoredPosition = new Vector2(startX, baseY);
            rt.localEulerAngles = Vector3.zero;
            if (!rt.gameObject.activeSelf)
            {
                rt.gameObject.SetActive(true);
            }

            float speed = Random.Range(speedRange.x, speedRange.y);
            float distance = Mathf.Abs(endX - startX);
            float duration = Mathf.Max(0.8f, distance / Mathf.Max(1f, speed));

            float bobAmp = Mathf.Max(0f, bobAmplitude);
            float bobPeriod = Random.Range(bobPeriodRange.x, bobPeriodRange.y);
            float bobPhase = Random.Range(0f, Mathf.PI * 2f);
            float bobOmega = (Mathf.PI * 2f) / Mathf.Max(0.2f, bobPeriod);

            bool finished = false;
            float x = startX;

            DOTween.To(() => x, v =>
                {
                    x = v;
                    float bob = bobAmp > 0.01f
                        ? Mathf.Sin(Time.unscaledTime * bobOmega + bobPhase) * bobAmp
                        : 0f;
                    rt.anchoredPosition = new Vector2(x, baseY + bob);
                }, endX, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetTarget(rt)
                .OnComplete(() => { finished = true; });

            while (!finished && enabled)
            {
                yield return null;
            }

            rt.DOKill();
            if (!enabled)
            {
                yield break;
            }

            float rest = Random.Range(restDelayRange.x, restDelayRange.y);
            if (rest > 0f)
            {
                yield return new WaitForSecondsRealtime(rest);
            }
        }
    }

    /// <summary>
    /// Canvas/view left-right edges in <paramref name="localSpace"/> local X.
    /// Prefer root canvas so SlotRow (narrow) does not cause early wrap while still on screen.
    /// </summary>
    private static bool TryGetViewLocalX(RectTransform localSpace, out float minX, out float maxX)
    {
        minX = -600f;
        maxX = 600f;
        if (localSpace == null)
        {
            return false;
        }

        Canvas canvas = localSpace.GetComponentInParent<Canvas>();
        RectTransform view = null;
        if (canvas != null)
        {
            Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            view = root.transform as RectTransform;
        }

        if (view == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        view.GetWorldCorners(corners);
        float x0 = localSpace.InverseTransformPoint(corners[0]).x;
        float x1 = localSpace.InverseTransformPoint(corners[2]).x;
        minX = Mathf.Min(x0, x1);
        maxX = Mathf.Max(x0, x1);
        return (maxX - minX) > 8f;
    }
}
