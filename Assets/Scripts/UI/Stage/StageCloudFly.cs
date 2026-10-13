using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StagePanel yun1/yun2/yun3: on panel enter each cloud starts at a random on-screen X,
/// then drifts right at constant speed. When off the right edge, respawn left and loop.
/// Soft bob while flying. No land/hold.
/// </summary>
public class StageCloudFly : MonoBehaviour
{
    [SerializeField] private RectTransform[] clouds;
    [Tooltip("Usually SlotRow. Used to measure Y band and off-screen edges.")]
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

            float halfW = measure != null ? measure.rect.width * 0.5f : 600f;
            float cloudHalfW = Mathf.Abs(rt.rect.width * rt.localScale.x) * 0.5f;
            if (cloudHalfW < 1f)
            {
                cloudHalfW = 200f;
            }

            // Always drift left -> right. First enter: random on-screen X; later: spawn left.
            float endX = halfW + cloudHalfW + edgePadding;
            float startX;
            bool onScreenStart = _useOnScreenStart != null && index < _useOnScreenStart.Length && _useOnScreenStart[index];
            if (onScreenStart)
            {
                float xMin = -halfW + cloudHalfW + edgePadding * 0.25f;
                float xMax = halfW - cloudHalfW - edgePadding * 0.25f;
                if (xMax < xMin)
                {
                    xMin = -halfW * 0.4f;
                    xMax = halfW * 0.4f;
                }

                startX = Random.Range(xMin, xMax);
                _useOnScreenStart[index] = false;
            }
            else
            {
                startX = -halfW - cloudHalfW - edgePadding;
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
}
