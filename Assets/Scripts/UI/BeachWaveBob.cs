using DG.Tweening;
using UnityEngine;

/// <summary>
/// 海滩轻晃：对多个 RectTransform 做左右（X）浮动 + 轻倾，Yoyo 无限循环。
/// 每项可设相位偏移，避免 Stage 按钮和浪花 Lang 同步。不改 raycast，按钮照常可点。
/// 主浮动走 anchoredPosition.x（bobAmplitude / bobPeriod）；不再做 Z/缩放，也不再另开一条 X sway，避免两条 X 动画打架。
/// </summary>
public class BeachWaveBob : MonoBehaviour
{
    [System.Serializable]
    public class Target
    {
        public RectTransform rect;
        [Tooltip("相对相位 0~1，错开多个目标的节奏")]
        [Range(0f, 1f)] public float phaseOffset = 0f;
        [Tooltip("该目标的 X 幅度倍率")]
        public float amplitudeScale = 1f;
    }

    [SerializeField] private Target[] targets;

    [Header("X Bob")]
    [SerializeField] private float bobAmplitude = 10f;
    [SerializeField] private float bobPeriod = 2.4f;

    [Header("Optional Z Tilt (degrees)")]
    [SerializeField] private float tiltAngle = 1.2f;
    [SerializeField] private float tiltPeriod = 2.8f;

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

    public void StartMotion()
    {
        StopMotion(false);
        if (targets == null || targets.Length == 0)
        {
            return;
        }

        _basePos = new Vector2[targets.Length];
        _baseEuler = new Vector3[targets.Length];
        _tweens = new Tween[targets.Length * 2];
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
            float xAmp = bobAmplitude * ampScale;
            float phase = Mathf.Repeat(t.phaseOffset, 1f);
            float bobDur = Mathf.Max(0.2f, bobPeriod);
            float tiltDur = Mathf.Max(0.2f, tiltPeriod);

            float bobDelay = phase * bobDur;
            float tiltDelay = phase * tiltDur * 1.17f;

            Vector2 basePos = _basePos[i];
            Vector3 baseEuler = _baseEuler[i];

            _tweens[ti++] = rt.DOAnchorPosX(basePos.x + xAmp, bobDur * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetDelay(bobDelay)
                .SetTarget(rt);

            if (Mathf.Abs(tiltAngle) > 0.01f)
            {
                _tweens[ti++] = rt.DOLocalRotate(new Vector3(baseEuler.x, baseEuler.y, baseEuler.z + tiltAngle), tiltDur * 0.5f)
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
