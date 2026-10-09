using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 主界面右侧浪花 Lang：只沿 X 轴，不绕 Z 转。
/// 每轮先「扑过来」（朝屏幕中心，X 减小），再慢慢退回原位，循环；冲刺/回退时长每轮随机。
/// </summary>
public class LangWaveMotion : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    [Header("Distance")]
    [Tooltip("扑过来的水平距离（像素）。右侧 Lang 默认向左（负 X）")]
    [SerializeField] private float surgeDistance = 60f;
    [Tooltip("勾选：扑过来时 X 减小（右侧浪朝中心）；取消则反向")]
    [SerializeField] private bool surgeTowardNegativeX = true;

    [Header("Duration (random each cycle)")]
    [SerializeField] private Vector2 surgeDurationRange = new Vector2(0.6f, 1.2f);
    [SerializeField] private Vector2 retreatDurationRange = new Vector2(1.5f, 3.0f);

    [Header("Ease")]
    [SerializeField] private Ease surgeEase = Ease.OutQuad;
    [SerializeField] private Ease retreatEase = Ease.InOutSine;

    private Coroutine _loop;
    private Tween _tween;
    private float _baseX;
    private bool _hasBase;

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

        // 不改 Z 旋转；若之前被 BeachWaveBob 带过倾角，复位
        Vector3 e = target.localEulerAngles;
        target.localEulerAngles = new Vector3(e.x, e.y, 0f);

        if (!_hasBase)
        {
            _baseX = target.anchoredPosition.x;
            _hasBase = true;
        }
        else
        {
            Vector2 p = target.anchoredPosition;
            p.x = _baseX;
            target.anchoredPosition = p;
        }

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
        if (target != null && _hasBase)
        {
            Vector2 p = target.anchoredPosition;
            p.x = _baseX;
            target.anchoredPosition = p;
        }
    }

    private IEnumerator WaveLoop()
    {
        while (enabled && target != null)
        {
            float sign = surgeTowardNegativeX ? -1f : 1f;
            float surgeX = _baseX + sign * Mathf.Max(0f, surgeDistance);
            float surgeDur = Random.Range(surgeDurationRange.x, surgeDurationRange.y);
            surgeDur = Mathf.Max(0.05f, surgeDur);

            bool surged = false;
            KillTween();
            _tween = target.DOAnchorPosX(surgeX, surgeDur)
                .SetEase(surgeEase)
                .SetUpdate(true)
                .SetTarget(target)
                .OnComplete(() => { surged = true; });

            while (!surged)
            {
                yield return null;
            }

            float retreatDur = Random.Range(retreatDurationRange.x, retreatDurationRange.y);
            retreatDur = Mathf.Max(0.05f, retreatDur);

            bool retreated = false;
            KillTween();
            _tween = target.DOAnchorPosX(_baseX, retreatDur)
                .SetEase(retreatEase)
                .SetUpdate(true)
                .SetTarget(target)
                .OnComplete(() => { retreated = true; });

            while (!retreated)
            {
                yield return null;
            }
        }
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
