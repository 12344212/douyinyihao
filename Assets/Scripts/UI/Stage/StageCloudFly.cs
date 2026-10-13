using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StagePanel yun1/yun2/yun3: each cloud flies in from off-screen, lands at a
/// random position inside the bounds, holds briefly (soft bob), then flies out
/// and loops continuously via DOTween.
/// </summary>
public class StageCloudFly : MonoBehaviour
{
    [SerializeField] private RectTransform[] clouds;
    [Tooltip("Usually SlotRow. Used to measure land area and off-screen edges.")]
    [SerializeField] private RectTransform boundsRoot;

    [Header("Timing")]
    [SerializeField] private Vector2 flyInDurationRange = new Vector2(2.0f, 3.4f);
    [SerializeField] private Vector2 flyOutDurationRange = new Vector2(1.4f, 2.4f);
    [SerializeField] private Vector2 landHoldRange = new Vector2(1.4f, 3.0f);
    [SerializeField] private Vector2 restDelayRange = new Vector2(0.25f, 1.1f);
    [SerializeField] private Vector2 initialDelayRange = new Vector2(0f, 1.4f);

    [Header("Land Area (local anchored, relative to boundsRoot center)")]
    [SerializeField] private Vector2 landMin = new Vector2(-420f, -70f);
    [SerializeField] private Vector2 landMax = new Vector2(420f, 110f);
    [SerializeField] private float edgePadding = 90f;

    [Header("Soft Bob After Land")]
    [SerializeField] private float landBobAmplitude = 5f;
    [SerializeField] private float landBobPeriod = 2.5f;

    private Coroutine[] _loops;
    private Vector2[] _restorePos;

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
        for (int i = 0; i < clouds.Length; i++)
        {
            RectTransform rt = clouds[i];
            if (rt == null)
            {
                continue;
            }

            _restorePos[i] = rt.anchoredPosition;
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

            float halfW = measure != null ? measure.rect.width * 0.5f : 600f;
            float cloudHalfW = Mathf.Abs(rt.rect.width * rt.localScale.x) * 0.5f;
            if (cloudHalfW < 1f)
            {
                cloudHalfW = 200f;
            }

            Vector2 land = new Vector2(
                Random.Range(landMin.x, landMax.x),
                Random.Range(landMin.y, landMax.y));

            bool fromLeft = Random.value > 0.5f;
            float startX = fromLeft
                ? -halfW - cloudHalfW - edgePadding
                : halfW + cloudHalfW + edgePadding;
            float startY = land.y + Random.Range(-24f, 24f);

            bool exitLeft = Random.value > 0.45f ? !fromLeft : fromLeft;
            float endX = exitLeft
                ? -halfW - cloudHalfW - edgePadding
                : halfW + cloudHalfW + edgePadding;
            float endY = land.y + Random.Range(-30f, 30f);

            rt.DOKill();
            rt.anchoredPosition = new Vector2(startX, startY);
            rt.localEulerAngles = Vector3.zero;
            if (!rt.gameObject.activeSelf)
            {
                rt.gameObject.SetActive(true);
            }

            float flyIn = Random.Range(flyInDurationRange.x, flyInDurationRange.y);
            bool arrived = false;
            rt.DOAnchorPos(land, Mathf.Max(0.35f, flyIn))
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetTarget(rt)
                .OnComplete(() => { arrived = true; });

            while (!arrived && enabled)
            {
                yield return null;
            }

            if (!enabled)
            {
                yield break;
            }

            float hold = Random.Range(landHoldRange.x, landHoldRange.y);
            if (landBobAmplitude > 0.01f && hold > 0.05f)
            {
                float bobDur = Mathf.Max(0.25f, landBobPeriod);
                rt.DOAnchorPosY(land.y + landBobAmplitude, bobDur * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetTarget(rt);
            }

            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            rt.DOKill();
            rt.anchoredPosition = land;

            float flyOut = Random.Range(flyOutDurationRange.x, flyOutDurationRange.y);
            bool left = false;
            rt.DOAnchorPos(new Vector2(endX, endY), Mathf.Max(0.25f, flyOut))
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .SetTarget(rt)
                .OnComplete(() => { left = true; });

            while (!left && enabled)
            {
                yield return null;
            }

            rt.DOKill();
            float rest = Random.Range(restDelayRange.x, restDelayRange.y);
            if (rest > 0f)
            {
                yield return new WaitForSecondsRealtime(rest);
            }
        }
    }
}
