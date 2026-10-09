using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 主界面右侧浪花 Lang：只沿 X 轴，不绕 Z 转。
/// X 夹在 [xMin, xMax]：从右侧休息点扑向左侧（快启慢停），再慢退回右侧。
/// 可挂跟随者（如 Stage 木筏）：按 Lang 的位移差 * followScale 同步飘，幅度更小，时间/缓动与浪一致。
/// </summary>
public class LangWaveMotion : MonoBehaviour
{
    [System.Serializable]
    public class Follower
    {
        public RectTransform rect;
        [Tooltip("相对浪花位移的倍率，越小木筏晃得越轻")]
        [Range(0.05f, 1f)] public float followScale = 0.4f;
    }

    [SerializeField] private RectTransform target;

    [Header("X Range (anchoredPosition.x)")]
    [SerializeField] private float xMin = 725f;
    [SerializeField] private float xMax = 875f;
    [Tooltip("勾选：休息在 xMax（右），扑向 xMin（左，朝中心）；取消则对调")]
    [SerializeField] private bool restAtMax = true;

    [Header("Followers (e.g. Stage raft)")]
    [SerializeField] private Follower[] followers;

    [Header("Duration (random each cycle)")]
    [SerializeField] private Vector2 surgeDurationRange = new Vector2(0.6f, 1.2f);
    [SerializeField] private Vector2 retreatDurationRange = new Vector2(1.5f, 3.0f);

    [Header("Ease")]
    [Tooltip("扑过来：初速快、逐渐减速")]
    [SerializeField] private Ease surgeEase = Ease.OutCubic;
    [SerializeField] private Ease retreatEase = Ease.InOutSine;

    private Coroutine _loop;
    private Tween _tween;
    private float[] _followerBaseX;
    private bool _followerBasesReady;

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
        CaptureFollowerBases();
        SetLangX(RestX);
        SyncFollowers();

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
            SetLangX(RestX);
            SyncFollowers();
        }
    }

    private IEnumerator WaveLoop()
    {
        while (enabled && target != null)
        {
            float surgeDur = Mathf.Max(0.05f, Random.Range(surgeDurationRange.x, surgeDurationRange.y));
            bool surged = false;
            KillTween();
            SetLangX(Mathf.Clamp(target.anchoredPosition.x, XMinClamped, XMaxClamped));
            SyncFollowers();
            _tween = target.DOAnchorPosX(SurgeX, surgeDur)
                .SetEase(surgeEase)
                .SetUpdate(true)
                .SetTarget(target)
                .OnUpdate(OnWaveUpdate)
                .OnComplete(() =>
                {
                    SetLangX(SurgeX);
                    SyncFollowers();
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
                .OnUpdate(OnWaveUpdate)
                .OnComplete(() =>
                {
                    SetLangX(RestX);
                    SyncFollowers();
                    retreated = true;
                });

            while (!retreated)
            {
                yield return null;
            }
        }
    }

    private void OnWaveUpdate()
    {
        ClampLangX();
        SyncFollowers();
    }

    private void CaptureFollowerBases()
    {
        if (followers == null || followers.Length == 0)
        {
            _followerBaseX = null;
            _followerBasesReady = true;
            return;
        }

        _followerBaseX = new float[followers.Length];
        for (int i = 0; i < followers.Length; i++)
        {
            if (followers[i] != null && followers[i].rect != null)
            {
                _followerBaseX[i] = followers[i].rect.anchoredPosition.x;
            }
        }

        _followerBasesReady = true;
    }

    private void SyncFollowers()
    {
        if (!_followerBasesReady || followers == null || _followerBaseX == null || target == null)
        {
            return;
        }

        float delta = target.anchoredPosition.x - RestX;
        for (int i = 0; i < followers.Length; i++)
        {
            Follower f = followers[i];
            if (f == null || f.rect == null || i >= _followerBaseX.Length)
            {
                continue;
            }

            Vector2 p = f.rect.anchoredPosition;
            p.x = _followerBaseX[i] + delta * Mathf.Clamp(f.followScale, 0.05f, 1f);
            f.rect.anchoredPosition = p;
        }
    }

    private void ClampLangX()
    {
        if (target == null)
        {
            return;
        }

        float x = Mathf.Clamp(target.anchoredPosition.x, XMinClamped, XMaxClamped);
        if (!Mathf.Approximately(x, target.anchoredPosition.x))
        {
            SetLangX(x);
        }
    }

    private void SetLangX(float x)
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
