using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 主界面右侧浪花 Lang：只沿 X 轴，不绕 Z 转。
/// X 始终夹在 [xMin, xMax]：从右侧休息点扑向左侧（快启慢停），再慢退回右侧，循环。
/// </summary>
public class LangWaveMotion : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    [Header("X Range (anchoredPosition.x)")]
    [SerializeField] private float xMin = 725f;
    [SerializeField] private float xMax = 875f;
    [Tooltip("勾选：休息在 xMax（右），扑向 xMin（左，朝中心）；取消则对调")]
    [SerializeField] private bool restAtMax = true;

    [Header("Duration (random each cycle)")]
    [SerializeField] private Vector2 surgeDurationRange = new Vector2(0.6f, 1.2f);
    [SerializeField] private Vector2 retreatDurationRange = new Vector2(1.5f, 3.0f);

    [Header("Ease")]
    [Tooltip("扑过来：初速快、逐渐减速")]
    [SerializeField] private Ease surgeEase = Ease.OutCubic;
    [SerializeField] private Ease retreatEase = Ease.InOutSine;

    private Coroutine _loop;
    private Tween _tween;

    private float RestX
    {
        get { return restAtMax ? XMaxClamped : XMinClamped; }
    }

    private float SurgeX
    {
        get { return restAtMax ? XMinClamped : XMaxClamped; }
    }

    private float XMinClamped
    {
        get { return Mathf.Min(xMin, xMax); }
    }

    private float XMaxClamped
    {
        get { return Mathf.Max(xMin, xMax); }
    }

    private void Awake()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }
    }

    private void OnEnable()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }

        if (target == null)
        {
            Debug.LogWarning("[LangWaveMotion] No RectTransform target.");
            return;
        }

        Vector3 e = target.localEulerAngles;
        target.localEulerAngles = new Vector3(e.x, e.y, 0f);
        SetX(RestX);

        if (_loop != null)
        {
            StopCoroutine(_loop);
        }

        _loop = StartCoroutine(WaveLoop());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        KillTween();
        if (target != null)
        {
            SetX(RestX);
        }
    }

    private IEnumerator WaveLoop()
    {
        while (enabled && target != null)
        {
            float surgeDur = Mathf.Max(0.05f, Random.Range(surgeDurationRange.x, surgeDurationRange.y));
            bool surged = false;
            KillTween();
            SetX(Mathf.Clamp(target.anchoredPosition.x, XMinClamped, XMaxClamped));
            _tween = target.DOAnchorPosX(SurgeX, surgeDur)
                .SetEase(surgeEase)
                .SetUpdate(true)
                .SetTarget(target)
                .OnUpdate(ClampX)
                .OnComplete(() =>
                {
                    SetX(SurgeX);
                    surged = true;
                });

            while (!surged)
            {
                yield return null;
            }

            float retreatDur = Mathf.Max(0.05f, Random.Range(retreatDurationRange.x, retreatDurationRange.y));
            bool retreated = false;
            KillTween();
            _tween = target.DOAnchorPosX(RestX, retreatDur)
                .SetEase(retreatEase)
                .SetUpdate(true)
                .SetTarget(target)
                .OnUpdate(ClampX)
                .OnComplete(() =>
                {
                    SetX(RestX);
                    retreated = true;
                });

            while (!retreated)
            {
                yield return null;
            }
        }
    }

    private void ClampX()
    {
        if (target == null)
        {
            return;
        }

        float x = Mathf.Clamp(target.anchoredPosition.x, XMinClamped, XMaxClamped);
        if (!Mathf.Approximately(x, target.anchoredPosition.x))
        {
            SetX(x);
        }
    }

    private void SetX(float x)
    {
        Vector2 p = target.anchoredPosition;
        p.x = Mathf.Clamp(x, XMinClamped, XMaxClamped);
        target.anchoredPosition = p;
    }

    private void KillTween()
    {
        if (_tween != null && _tween.IsActive())
        {
            _tween.Kill();
        }

        _tween = null;
        if (target != null)
        {
            target.DOKill();
        }
    }
}
