using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enemy view shared by melee and ranged. Bind Firepoint for ranged.
/// Hpbg/Image (Filled) shows HP via hpFill; legacy barFill kept for old Sprite bars.
/// Attack cues: melee lunge / ranged recoil on bodyScale via DOTween.
/// </summary>
public class BattleEnemy : MonoBehaviour
{
    private const float MeleeLungeDist = 0.38f;
    private const float MeleeLungeOut = 0.07f;
    private const float MeleeLungeBack = 0.11f;
    private const float ShootRecoilDist = 0.22f;
    private const float ShootRecoilOut = 0.05f;
    private const float ShootRecoilBack = 0.12f;
    private const float MuzzlePunchScale = 0.12f;
    private const float MuzzlePunchDur = 0.16f;

    [SerializeField] private Transform bodyScale;
    [SerializeField] private SpriteRenderer barFill;
    [Tooltip("Ranged enemy: fire Bulie3 from Firepoint")]
    [SerializeField] private bool ranged;
    [Tooltip("Drag Firepoint child here for ranged")]
    [SerializeField] private Transform firePoint;
    [Tooltip("Drag Canvas/Hpbg/Image (Filled) here")]
    [SerializeField] private Image hpFill;

    private Vector3 _fillBaseScale;
    private Vector3 _fillBasePos;
    private Vector3 _bodyBaseLocal;
    private Vector3 _bodyBaseScale = Vector3.one;
    private bool _bodyBaseCached;
    private Tween _attackTween;
    private Transform _faceRoot;
    private bool _faceRootResolved;

    private void Awake()
    {
        if (barFill != null)
        {
            _fillBaseScale = barFill.transform.localScale;
            _fillBasePos = barFill.transform.localPosition;
        }

        CacheBodyBase();

        // 未拖引用时尝试自动绑�?Hpbg 下的填充 Image
        if (hpFill == null)
        {
            Transform canvas = transform.Find("Canvas");
            Transform hpbg = canvas != null ? canvas.Find("Hpbg") : null;
            Transform fill = hpbg != null ? hpbg.Find("Image") : null;
            if (fill != null)
            {
                hpFill = fill.GetComponent<Image>();
            }
        }
    }

    private void OnDisable()
    {
        KillAttackTween(true);
    }

    private void OnDestroy()
    {
        KillAttackTween(false);
    }

    public bool IsRanged => ranged;

    public Vector3 MuzzleWorldPos()
    {
        if (firePoint != null)
        {
            return firePoint.position;
        }

        return transform.position;
    }

    public float BodyRadius()
    {
        if (bodyScale == null)
        {
            return 0.31f;
        }

        return bodyScale.lossyScale.x * 0.5f;
    }

    public void SetBodyScale(float size)
    {
        if (bodyScale != null)
        {
            KillAttackTween(false);
            bodyScale.localScale = new Vector3(size, size, 1f);
            _bodyBaseCached = false;
            CacheBodyBase();
        }
    }

    public void SetHealth01(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
        }

        if (barFill == null)
        {
            return;
        }

        Vector3 scale = _fillBaseScale;
        scale.x = Mathf.Max(0.001f, _fillBaseScale.x * ratio);
        barFill.transform.localScale = scale;
        Vector3 pos = _fillBasePos;
        pos.x = _fillBasePos.x + _fillBaseScale.x * (ratio - 1f) * 0.5f;
        barFill.transform.localPosition = pos;
    }

    /// <summary>Melee impact: short lunge toward plank on bodyScale (root stays on logic pos).</summary>
    public void PlayMeleeImpact(Vector3 worldToward)
    {
        Transform body = AnimBody();
        if (body == null)
        {
            return;
        }

        CacheBodyBase();
        KillAttackTween(true);
        Vector3 localDir = ToLocalFlat(worldToward);
        if (localDir.sqrMagnitude < 0.0001f)
        {
            localDir = Vector3.right;
        }

        Vector3 peak = _bodyBaseLocal + localDir * MeleeLungeDist;
        Sequence seq = DOTween.Sequence().SetLink(gameObject).SetUpdate(false);
        seq.Append(body.DOLocalMove(peak, MeleeLungeOut).SetEase(Ease.OutQuad));
        seq.Append(body.DOLocalMove(_bodyBaseLocal, MeleeLungeBack).SetEase(Ease.InQuad));
        _attackTween = seq;
    }

    /// <summary>Ranged fire: recoil away from aim + light muzzle scale punch.</summary>
    public void PlayShootRecoil(Vector3 worldFireDir)
    {
        Transform body = AnimBody();
        if (body == null)
        {
            return;
        }

        CacheBodyBase();
        KillAttackTween(true);
        Vector3 localDir = ToLocalFlat(worldFireDir);
        if (localDir.sqrMagnitude < 0.0001f)
        {
            localDir = Vector3.right;
        }

        Vector3 recoil = _bodyBaseLocal - localDir * ShootRecoilDist;
        Sequence seq = DOTween.Sequence().SetLink(gameObject).SetUpdate(false);
        seq.Append(body.DOLocalMove(recoil, ShootRecoilOut).SetEase(Ease.OutQuad));
        seq.Join(body.DOPunchScale(Vector3.one * MuzzlePunchScale, MuzzlePunchDur, 6, 0.55f));
        seq.Append(body.DOLocalMove(_bodyBaseLocal, ShootRecoilBack).SetEase(Ease.OutBack));
        _attackTween = seq;
    }

    /// <summary>
    /// Rotate visual child T (fallback bodyScale) so its +X faces worldDir (movement / target).
    /// Root stays upright so HP canvas does not tilt.
    /// </summary>
    public void FaceToward(Vector3 worldDir)
    {
        worldDir.z = 0f;
        if (worldDir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Transform face = ResolveFaceRoot();
        if (face == null)
        {
            return;
        }

        float angle = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg + 180f;
        face.localRotation = Quaternion.Euler(0f, 0f, angle);

        // Keep empty marker T aligned when body is the visual face root.
        Transform tMarker = transform.Find("T");
        if (tMarker != null && tMarker != face)
        {
            tMarker.localRotation = face.localRotation;
        }
    }

    private Transform ResolveFaceRoot()
    {
        if (_faceRootResolved)
        {
            return _faceRoot;
        }

        _faceRootResolved = true;
        Transform t = transform.Find("T");
        // Prefab T is often an empty marker; prefer it only when it has visible sprites.
        if (t != null && t.GetComponentInChildren<SpriteRenderer>(true) != null)
        {
            _faceRoot = t;
            return _faceRoot;
        }

        if (bodyScale != null)
        {
            _faceRoot = bodyScale;
            return _faceRoot;
        }

        _faceRoot = t;
        return _faceRoot;
    }

    private Transform AnimBody()
    {
        return bodyScale != null ? bodyScale : null;
    }

    private void CacheBodyBase()
    {
        Transform body = AnimBody();
        if (body == null)
        {
            return;
        }

        if (!_bodyBaseCached || _attackTween == null || !_attackTween.IsActive())
        {
            _bodyBaseLocal = body.localPosition;
            _bodyBaseScale = body.localScale;
            _bodyBaseCached = true;
        }
    }

    private Vector3 ToLocalFlat(Vector3 worldDir)
    {
        worldDir.z = 0f;
        if (worldDir.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 local = transform.InverseTransformDirection(worldDir.normalized);
        local.z = 0f;
        if (local.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        return local.normalized;
    }

    private void KillAttackTween(bool resetPose)
    {
        if (_attackTween != null && _attackTween.IsActive())
        {
            _attackTween.Kill(false);
        }

        _attackTween = null;
        if (resetPose)
        {
            Transform body = AnimBody();
            if (body != null && _bodyBaseCached)
            {
                body.localPosition = _bodyBaseLocal;
                body.localScale = _bodyBaseScale;
            }
        }
    }
}
