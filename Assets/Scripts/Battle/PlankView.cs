using DG.Tweening;
using UnityEngine;

/// <summary>
/// 木板表现。血条与着色在预制体上配好，运行时创建亦可。
/// </summary>
public class PlankView : MonoBehaviour
{
    private const float GhostAlpha = 0.4f;
    private const float BreathePeriod = 1.25f;
    private const float BreatheScaleMin = 0.95f;
    private const float BreatheScaleMax = 1.05f;
    private const float GhostAlphaMin = 0.32f;
    private const float GhostAlphaMax = 0.48f;

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Transform bodyScale;
    [SerializeField] private SpriteRenderer barFill;
    [SerializeField] private Transform towerAnchor;
    [SerializeField] private int cellX;
    [SerializeField] private int cellY;

    private Vector3 _fillBaseScale;
    private Vector3 _fillBasePos;
    private Color _baseColor = Color.white;

    private bool _breathe;
    private bool _breatheAlpha;
    private Vector3 _breatheBaseScale = Vector3.one;
    private float _breathePhase;

    private SpriteRenderer[] _sprites;
    private int[] _savedOrders;

    private Tween _hitShakeTween;
    private Vector3 _hitShakeBaseLocal;
    private bool _hitShakeBaseCached;

    public int CellX => cellX;
    public int CellY => cellY;
    public Transform TowerAnchor => towerAnchor;
    public Color BaseColor => _baseColor;

    private void Awake()
    {
        if (barFill != null)
        {
            _fillBaseScale = barFill.transform.localScale;
            _fillBasePos = barFill.transform.localPosition;
        }
    }

    private void Update()
    {
        if (!_breathe)
        {
            return;
        }

        // 建造模式战斗逻辑暂停，用 unscaledTime 保证预览仍有呼吸感
        float wave = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / BreathePeriod) + _breathePhase) + 1f) * 0.5f;
        float scaleMul = Mathf.Lerp(BreatheScaleMin, BreatheScaleMax, wave);
        Transform scaleTarget = bodyScale != null ? bodyScale : transform;
        scaleTarget.localScale = _breatheBaseScale * scaleMul;

        if (_breatheAlpha && body != null)
        {
            Color c = body.color;
            c.a = Mathf.Lerp(GhostAlphaMin, GhostAlphaMax, wave);
            body.color = c;
        }
    }

    public void SetCell(int x, int y)
    {
        cellX = x;
        cellY = y;
    }

    public void Paint(Color plankColor)
    {
        float shade = 0.88f + (Mathf.Abs(cellX * 3 + cellY * 5) % 4) * 0.04f;
        _baseColor = plankColor * shade;
        _baseColor.a = 1f;
        SetHealth01(1f);
    }

    public float BodyRadius()
    {
        if (body != null)
        {
            Vector3 ext = body.bounds.extents;
            return Mathf.Max(ext.x, ext.y);
        }

        if (bodyScale != null)
        {
            return bodyScale.lossyScale.x * 0.5f;
        }

        return 0.81f;
    }

    public void SetBodyScale(float size)
    {
        if (bodyScale != null)
        {
            bodyScale.localScale = new Vector3(size, size, 1f);
        }
    }

    public void SetHealth01(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
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

    /// <summary>建造模式幽灵木板：半透明、隐藏血条、提高排序，并开启含透明度的呼吸。</summary>
    public void ApplyGhostLook()
    {
        EnsureSprites();
        for (int i = 0; i < _sprites.Length; i++)
        {
            SpriteRenderer sr = _sprites[i];
            if (sr == null)
            {
                continue;
            }

            if (sr == body)
            {
                Color c = sr.color;
                c.a = GhostAlpha;
                sr.color = c;
                sr.sortingOrder = 45;
            }
            else
            {
                sr.enabled = false;
            }
        }

        StartBreathe(true);
    }

    /// <summary>建造模式下可选真木板：仅缩放呼吸，保留血条与不透明。</summary>
    public void StartTargetBreathe()
    {
        StartBreathe(false);
    }

    /// <summary>停止建造预览呼吸（幽灵销毁前 / 真木板退出建造模式时调用）。</summary>
    public void StopGhostBreathe()
    {
        if (!_breathe)
        {
            return;
        }

        bool wasAlpha = _breatheAlpha;
        _breathe = false;
        _breatheAlpha = false;
        Transform scaleTarget = bodyScale != null ? bodyScale : transform;
        scaleTarget.localScale = _breatheBaseScale;
        if (wasAlpha && body != null)
        {
            Color c = body.color;
            c.a = GhostAlpha;
            body.color = c;
        }
    }

    private void StartBreathe(bool pulseAlpha)
    {
        Transform scaleTarget = bodyScale != null ? bodyScale : transform;
        _breatheBaseScale = scaleTarget.localScale;
        _breathePhase = (GetInstanceID() & 1023) * 0.017f;
        _breatheAlpha = pulseAlpha;
        _breathe = true;
    }

    /// <summary>建造模式抬高空木板排序，使其压在遮罩之上。</summary>
    public void BoostSorting(int baseOrder)
    {
        EnsureSprites();
        if (_savedOrders == null || _savedOrders.Length != _sprites.Length)
        {
            _savedOrders = new int[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
            {
                _savedOrders[i] = _sprites[i] != null ? _sprites[i].sortingOrder : 0;
            }
        }

        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] != null)
            {
                _sprites[i].sortingOrder = baseOrder + i;
            }
        }
    }

    public void RestoreSorting()
    {
        if (_sprites == null || _savedOrders == null)
        {
            return;
        }

        for (int i = 0; i < _sprites.Length && i < _savedOrders.Length; i++)
        {
            if (_sprites[i] != null)
            {
                _sprites[i].sortingOrder = _savedOrders[i];
            }
        }

        _savedOrders = null;
    }

    /// <summary>Brief DOTween shake on body when any plank is hit (shared HP feedback).</summary>
    public void PlayHitShake()
    {
        Transform target = bodyScale != null ? bodyScale : transform;
        if (target == null)
        {
            return;
        }

        if (!_hitShakeBaseCached || _hitShakeTween == null || !_hitShakeTween.IsActive())
        {
            _hitShakeBaseLocal = target.localPosition;
            _hitShakeBaseCached = true;
        }

        if (_hitShakeTween != null && _hitShakeTween.IsActive())
        {
            _hitShakeTween.Kill(false);
        }

        target.localPosition = _hitShakeBaseLocal;
        _hitShakeTween = target
            .DOShakePosition(0.22f, 0.14f, 18, 90f, false, true)
            .SetLink(gameObject)
            .SetUpdate(false)
            .OnKill(() =>
            {
                if (target != null)
                {
                    target.localPosition = _hitShakeBaseLocal;
                }
            })
            .OnComplete(() =>
            {
                if (target != null)
                {
                    target.localPosition = _hitShakeBaseLocal;
                }
            });
    }

    private void OnDisable()
    {
        if (_hitShakeTween != null && _hitShakeTween.IsActive())
        {
            _hitShakeTween.Kill(false);
        }

        _hitShakeTween = null;
        if (_hitShakeBaseCached)
        {
            Transform target = bodyScale != null ? bodyScale : transform;
            if (target != null)
            {
                target.localPosition = _hitShakeBaseLocal;
            }
        }
    }

    private void EnsureSprites()
    {
        if (_sprites == null || _sprites.Length == 0)
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }
}
