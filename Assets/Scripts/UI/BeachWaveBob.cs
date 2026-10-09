using DG.Tweening;
using UnityEngine;

/// <summary>
/// 海滩轻晃：对多个 RectTransform 做「前后」纵深 + 左右微摆 + 轻倾，Yoyo 无限循环。
/// 每项可设相位偏移，避免 Stage 按钮和浪花 Lang 同步。不改 raycast，按钮照常可点。
///
/// 前后说明：主界面 UI 相机是正交的，只改 localPosition.z 几乎看不出远近。
/// 因此用同一套 bobAmplitude / bobPeriod：一边推 Z，一边用同相位的轻微缩放（近大远小）做出前后感。
/// </summary>
public class BeachWaveBob : MonoBehaviour
{
    [System.Serializable]
    public class Target
    {
        public RectTransform rect;
        [Tooltip("相对相位 0~1，错开多个目标的节奏")]
        [Range(0f, 1f)] public float phaseOffset = 0f;
        [Tooltip("该目标的前后幅度倍率")]
        public float amplitudeScale = 1f;
    }

    [SerializeField] private Target[] targets;

    [Header("Depth Bob (forward / back)")]
    [SerializeField] private float bobAmplitude = 10f;
    [SerializeField] private float bobPeriod = 2.4f;
    [Tooltip("正交 UI 下近大远小的缩放半幅（乘 amplitudeScale）")]
    [SerializeField] private float depthScaleAmplitude = 0.03f;

    [Header("Horizontal Sway")]
    [SerializeField] private float swayAmplitude = 6f;
    [SerializeField] private float swayPeriod = 3.2f;

    [Header("Optional Z Tilt (degrees)")]
    [SerializeField] private float tiltAngle = 1.2f;
    [SerializeField] private float tiltPeriod = 2.8f;

    private Tween[] _tweens;
    private Vector2[] _basePos;
    private float[] _baseLocalZ;
    private Vector3[] _baseScale;
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
        _baseLocalZ = new float[targets.Length];
        _baseScale = new Vector3[targets.Length];
        _baseEuler = new Vector3[targets.Length];
        // 每目标最多：前后(Z+缩放合一) + 左右 + 倾角
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
            _baseLocalZ[i] = rt.localPosition.z;
            _baseScale[i] = rt.localScale;
            _baseEuler[i] = rt.localEulerAngles;

            float ampScale = Mathf.Max(0.05f, t.amplitudeScale);
            float zAmp = bobAmplitude * ampScale;
            float scaleAmp = depthScaleAmplitude * ampScale;
            float phase = Mathf.Repeat(t.phaseOffset, 1f);
            float bobDur = Mathf.Max(0.2f, bobPeriod);
            float swayDur = Mathf.Max(0.2f, swayPeriod);
            float tiltDur = Mathf.Max(0.2f, tiltPeriod);

            // 用延迟模拟相位：先错开再开 Yoyo，看起来就不会齐刷刷一起晃
            float bobDelay = phase * bobDur;
            float swayDelay = phase * swayDur * 0.73f;
            float tiltDelay = phase * tiltDur * 1.17f;

            Vector2 basePos = _basePos[i];
            float baseZ = _baseLocalZ[i];
            Vector3 baseScale = _baseScale[i];
            Vector3 baseEuler = _baseEuler[i];

            // 前后：localPosition.z 推拉（幅度与原上下 bob 相同），并同相位做轻微缩放，正交相机下也能看出远近
            float depthT = 0f;
            _tweens[ti++] = DOTween.To(() => depthT, v =>
                {
                    depthT = v;
                    Vector3 lp = rt.localPosition;
                    lp.z = baseZ + zAmp * depthT;
                    rt.localPosition = lp;
                    float s = 1f + scaleAmp * depthT;
                    rt.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
                }, 1f, bobDur * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetDelay(bobDelay)
                .SetTarget(rt);

            if (Mathf.Abs(swayAmplitude) > 0.01f)
            {
                _tweens[ti++] = rt.DOAnchorPosX(basePos.x + swayAmplitude * ampScale, swayDur * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetDelay(swayDelay)
                    .SetTarget(rt);
            }

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

            if (_baseLocalZ != null && i < _baseLocalZ.Length)
            {
                Vector3 lp = rt.localPosition;
                lp.z = _baseLocalZ[i];
                rt.localPosition = lp;
            }

            if (_baseScale != null && i < _baseScale.Length)
            {
                rt.localScale = _baseScale[i];
            }

            if (_baseEuler != null && i < _baseEuler.Length)
            {
                rt.localEulerAngles = _baseEuler[i];
            }
        }
    }
}
