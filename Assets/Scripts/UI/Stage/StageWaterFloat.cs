using DG.Tweening;
using UnityEngine;

/// <summary>
/// Light water-float for StagePanel: gentle Y bob (+ optional tiny X) via DOTween.
/// Used on the 5 stage slots and 3 yun clouds. Phase offsets keep motion desynced.
/// </summary>
public class StageWaterFloat : MonoBehaviour
{
    [System.Serializable]
    public class Target
    {
        public RectTransform rect;
        [Range(0f, 1f)] public float phaseOffset = 0f;
        public float amplitudeScale = 1f;
    }

    [SerializeField] private Target[] targets;

    [Header("Y Bob (water float)")]
    [SerializeField] private float bobAmplitudeY = 8f;
    [SerializeField] private float bobPeriod = 2.6f;

    [Header("Optional X Drift")]
    [SerializeField] private float bobAmplitudeX = 3f;

    [Header("Optional Z Tilt (degrees)")]
    [SerializeField] private float tiltAngle = 0.8f;
    [SerializeField] private float tiltPeriod = 3.2f;

    private Tween[] _tweens;
    private Vector2[] _basePos;
    private Vector3[] _baseEuler;

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

    public void SetTargets(Target[] next)
    {
        StopMotion(true);
        targets = next;
        if (isActiveAndEnabled)
        {
            StartMotion();
        }
    }

    public void StartMotion()
    {
        StopMotion(false);
        if (targets == null || targets.Length == 0)
        {
            return;
        }

        _basePos = new Vector2[targets.Length];
        _baseEuler = new Vector3[targets.Length];
        _tweens = new Tween[targets.Length * 3];
        int ti = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            Target t = targets[i];
            if (t == null || t.rect == null)
            {
                continue;
            }

            RectTransform rt = t.rect;
            _basePos[i] = rt.anchoredPosition;
            _baseEuler[i] = rt.localEulerAngles;

            float ampScale = Mathf.Max(0.05f, t.amplitudeScale);
            float phase = Mathf.Repeat(t.phaseOffset, 1f);
            float bobDur = Mathf.Max(0.25f, bobPeriod);
            float tiltDur = Mathf.Max(0.25f, tiltPeriod);
            float bobDelay = phase * bobDur;
            float tiltDelay = phase * tiltDur * 1.13f;

            Vector2 basePos = _basePos[i];
            Vector3 baseEuler = _baseEuler[i];
            float yAmp = bobAmplitudeY * ampScale;
            float xAmp = bobAmplitudeX * ampScale;

            _tweens[ti++] = rt.DOAnchorPosY(basePos.y + yAmp, bobDur * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetDelay(bobDelay)
                .SetTarget(rt);

            if (Mathf.Abs(xAmp) > 0.01f)
            {
                _tweens[ti++] = rt.DOAnchorPosX(basePos.x + xAmp, bobDur * 0.55f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetDelay(bobDelay * 0.7f)
                    .SetTarget(rt);
            }

            if (Mathf.Abs(tiltAngle) > 0.01f)
            {
                _tweens[ti++] = rt.DOLocalRotate(
                        new Vector3(baseEuler.x, baseEuler.y, baseEuler.z + tiltAngle * ampScale),
                        tiltDur * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetDelay(tiltDelay)
                    .SetTarget(rt);
            }
        }
    }

    private void StopMotion(bool restore)
    {
        if (_tweens != null)
        {
            for (int i = 0; i < _tweens.Length; i++)
            {
                if (_tweens[i] != null && _tweens[i].IsActive())
                {
                    _tweens[i].Kill();
                }
            }

            _tweens = null;
        }

        if (!restore || targets == null || _basePos == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null || targets[i].rect == null)
            {
                continue;
            }

            RectTransform rt = targets[i].rect;
            rt.DOKill();
            if (i < _basePos.Length)
            {
                rt.anchoredPosition = _basePos[i];
            }

            if (_baseEuler != null && i < _baseEuler.Length)
            {
                rt.localEulerAngles = _baseEuler[i];
            }
        }
    }
}
