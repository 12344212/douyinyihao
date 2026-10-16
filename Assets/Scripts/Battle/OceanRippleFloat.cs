using UnityEngine;

/// <summary>
/// 单条海浪波纹：水平漂移 + 轻微上下浮动，带独立生命周期（渐显/可见/渐隐/等待重生）。
/// 仅用 Transform / SpriteRenderer，无 Shader、无 DOTween、无协程（避免 OnDisable 叠协程）。
/// 高光系统由 OceanHighlightController 单独管理，本组件不参与。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class OceanRippleFloat : MonoBehaviour
{
    private enum Phase
    {
        /// <summary>隐藏等待（首次错峰或重生间隔）</summary>
        WaitingHidden,
        /// <summary>渐显</summary>
        FadingIn,
        /// <summary>完全可见并漂移</summary>
        Drifting,
        /// <summary>渐隐</summary>
        FadingOut
    }

    [Header("运动范围（世界单位，由 OceanCartoonRoot 注入）")]
    [SerializeField] private Vector2 areaMin = new Vector2(-12f, -7f);
    [SerializeField] private Vector2 areaMax = new Vector2(12f, 7f);

    [Header("水平速度 (wu/s)")]
    [Tooltip("比早期版本更快，可在 Inspector 继续拉高")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.22f, 0.65f);

    [Header("上下浮动")]
    [SerializeField] private Vector2 bobAmpRange = new Vector2(0.02f, 0.08f);
    [SerializeField] private Vector2 bobPeriodRange = new Vector2(2f, 4f);

    [Header("外观微调")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 rotationRange = new Vector2(-2f, 2f);
    [SerializeField] private Vector2 alphaRange = new Vector2(0.55f, 0.95f);

    [Header("生命周期")]
    [Tooltip("完全可见后、开始渐隐前的存活时间（秒）")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(2.5f, 6.5f);
    [Tooltip("渐显时长（秒）")]
    [SerializeField] private float fadeInDuration = 0.55f;
    [Tooltip("渐隐时长（秒）")]
    [SerializeField] private float fadeOutDuration = 0.7f;
    [Tooltip("渐隐结束后到下一次出现的等待（秒）")]
    [SerializeField] private Vector2 respawnDelayRange = new Vector2(0.4f, 2.2f);
    [Tooltip("首次出现前的错峰延迟（秒），避免全部同时渐显")]
    [SerializeField] private Vector2 initialDelayRange = new Vector2(0f, 2.8f);

    [Tooltip("重生时是否从切片池随机换一张")]
    [SerializeField] private bool randomizeSpriteOnRespawn = true;

    private SpriteRenderer _sr;
    private Sprite[] _spriteVariants;
    private Phase _phase = Phase.WaitingHidden;
    private float _dir;
    private float _speed;
    private float _bobAmp;
    private float _bobPeriod;
    private float _bobPhase;
    private float _baseY;
    private float _baseScale;
    private float _baseRotZ;
    private float _wrapPadding;
    private float _targetAlpha;
    private float _fadeFromAlpha;
    private float _phaseTimer;
    private float _phaseDuration;
    private float _lifetimeLeft;
    private float _scaleMultiplier = 1f;
    private bool _configured;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void OnDisable()
    {
        // 无协程可停；禁用时强制透明并回到等待，避免再次启用时状态错乱
        _phase = Phase.WaitingHidden;
        _phaseTimer = 0f;
        _phaseDuration = 0f;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        // 父物体重新激活时，若已配置则重新错峰开局（不叠协程）
        if (_configured && _sr != null)
        {
            BeginLifecycle(staggerFirstAppear: true);
        }
    }

    /// <summary>由根控制器设置可见海面范围。</summary>
    public void ConfigureArea(Vector2 min, Vector2 max)
    {
        areaMin = min;
        areaMax = max;
    }

    /// <summary>注入 bol 切图变体；重生时可随机换一张。</summary>
    public void SetSpriteVariants(Sprite[] variants)
    {
        _spriteVariants = variants;
    }

    /// <summary>基础缩放倍率（应对贴图 PPU 偏大）。</summary>
    public void SetScaleMultiplier(float multiplier)
    {
        _scaleMultiplier = Mathf.Max(0.05f, multiplier);
    }

    /// <summary>
    /// 由 OceanCartoonRoot 一次性注入运动/生命周期参数（可在根节点 Inspector 调）。
    /// </summary>
    public void ApplyMotionAndLifecycle(
        Vector2 speed,
        Vector2 lifetime,
        float fadeIn,
        float fadeOut,
        Vector2 respawnDelay,
        Vector2 initialDelay)
    {
        speedRange = speed;
        lifetimeRange = lifetime;
        fadeInDuration = Mathf.Max(0.01f, fadeIn);
        fadeOutDuration = Mathf.Max(0.01f, fadeOut);
        respawnDelayRange = respawnDelay;
        initialDelayRange = initialDelay;
    }

    /// <summary>
    /// 开始生命周期：先隐藏，随机错峰后再渐显；之后按 可见→渐隐→等待→换位重生 循环。
    /// </summary>
    /// <param name="staggerFirstAppear">true=首次用 initialDelay 错峰；false=立刻准备渐显</param>
    public void BeginLifecycle(bool staggerFirstAppear = true)
    {
        if (_sr == null)
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        _configured = true;
        RandomizeParamsAndPlace(placeInside: true, swapSprite: false);
        SetAlpha(0f);

        // 关键→等待错峰→渐显
        float wait = staggerFirstAppear
            ? Random.Range(initialDelayRange.x, initialDelayRange.y)
            : 0f;
        EnterWaitingHidden(Mathf.Max(0f, wait));
    }

    /// <summary>兼容旧调用：等价于 BeginLifecycle(staggerFirstAppear: placeInside)。</summary>
    public void RandomizeInArea(bool placeInside)
    {
        BeginLifecycle(staggerFirstAppear: placeInside);
    }

    private void EnterWaitingHidden(float seconds)
    {
        _phase = Phase.WaitingHidden;
        _phaseTimer = 0f;
        _phaseDuration = Mathf.Max(0f, seconds);
        SetAlpha(0f);
    }

    private void EnterFadingIn()
    {
        _phase = Phase.FadingIn;
        _phaseTimer = 0f;
        _phaseDuration = Mathf.Max(0.01f, fadeInDuration);
        SetAlpha(0f);
    }

    private void EnterDrifting()
    {
        _phase = Phase.Drifting;
        _phaseTimer = 0f;
        _lifetimeLeft = Random.Range(lifetimeRange.x, lifetimeRange.y);
        SetAlpha(_targetAlpha);
    }

    private void EnterFadingOut()
    {
        _phase = Phase.FadingOut;
        _phaseTimer = 0f;
        _phaseDuration = Mathf.Max(0.01f, fadeOutDuration);
        // 从当前透明度开始渐隐，避免渐显中途出界时突然跳到满 alpha
        _fadeFromAlpha = _sr != null ? _sr.color.a : _targetAlpha;
    }

    /// <summary>随机运动/外观参数并放到海面内（或从外侧入场）。</summary>
    private void RandomizeParamsAndPlace(bool placeInside, bool swapSprite)
    {
        if (_sr == null)
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        // 重生时可换 bol 切片；首次由 OceanCartoonRoot 按序号指定
        if (swapSprite && randomizeSpriteOnRespawn && _spriteVariants != null && _spriteVariants.Length > 0)
        {
            Sprite pick = _spriteVariants[Random.Range(0, _spriteVariants.Length)];
            if (pick != null)
            {
                _sr.sprite = pick;
            }
        }

        _dir = Random.value < 0.5f ? -1f : 1f;
        _speed = Random.Range(speedRange.x, speedRange.y);
        _bobAmp = Random.Range(bobAmpRange.x, bobAmpRange.y);
        _bobPeriod = Mathf.Max(0.2f, Random.Range(bobPeriodRange.x, bobPeriodRange.y));
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        _baseScale = Random.Range(scaleRange.x, scaleRange.y);
        _baseRotZ = Random.Range(rotationRange.x, rotationRange.y);
        _targetAlpha = Random.Range(alphaRange.x, alphaRange.y);

        float s = _baseScale * _scaleMultiplier;
        transform.localScale = new Vector3(s, s, 1f);
        transform.localEulerAngles = new Vector3(0f, 0f, _baseRotZ);

        // 用 sprite 世界半宽做换边余量
        _wrapPadding = 0.5f;
        if (_sr.sprite != null)
        {
            _wrapPadding = Mathf.Max(0.3f, _sr.bounds.extents.x);
        }

        float x;
        float y = Random.Range(areaMin.y, areaMax.y);
        if (placeInside)
        {
            x = Random.Range(areaMin.x, areaMax.x);
        }
        else
        {
            // 从行进方向的外侧入场
            x = _dir > 0f ? (areaMin.x - _wrapPadding) : (areaMax.x + _wrapPadding);
        }

        _baseY = y;
        transform.position = new Vector3(x, y, transform.position.z);
    }

    private void SetAlpha(float a)
    {
        if (_sr == null)
        {
            return;
        }

        Color c = _sr.color;
        c.a = Mathf.Clamp01(a);
        _sr.color = c;
    }

    private void Update()
    {
        if (!_configured || _sr == null)
        {
            return;
        }

        switch (_phase)
        {
            case Phase.WaitingHidden:
                UpdateWaitingHidden();
                break;
            case Phase.FadingIn:
                TickDrift();
                UpdateFadingIn();
                break;
            case Phase.Drifting:
                TickDrift();
                UpdateDrifting();
                break;
            case Phase.FadingOut:
                TickDrift();
                UpdateFadingOut();
                break;
        }
    }

    private void UpdateWaitingHidden()
    {
        _phaseTimer += Time.deltaTime;
        if (_phaseTimer < _phaseDuration)
        {
            return;
        }

        // 等待结束：若已有位置则直接渐显；重生路径会在进入等待前先换参换位
        EnterFadingIn();
    }

    private void UpdateFadingIn()
    {
        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / _phaseDuration);
        SetAlpha(Mathf.Lerp(0f, _targetAlpha, t));

        if (t >= 1f)
        {
            EnterDrifting();
            return;
        }

        // 渐显途中出界则提前进入渐隐
        if (IsOffBounds())
        {
            EnterFadingOut();
        }
    }

    private void UpdateDrifting()
    {
        _lifetimeLeft -= Time.deltaTime;
        if (_lifetimeLeft <= 0f || IsOffBounds())
        {
            EnterFadingOut();
        }
    }

    private void UpdateFadingOut()
    {
        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / _phaseDuration);
        // 从进入渐隐时的透明度插值到 0
        SetAlpha(Mathf.Lerp(_fadeFromAlpha, 0f, t));

        if (t < 1f)
        {
            return;
        }

        // 渐隐完毕 → 随机等待 → 换位换参再渐显
        RespawnAfterDelay();
    }

    /// <summary>换新参数与位置，进入隐藏等待后再渐显。</summary>
    private void RespawnAfterDelay()
    {
        float delay = Random.Range(respawnDelayRange.x, respawnDelayRange.y);
        RandomizeParamsAndPlace(placeInside: true, swapSprite: true);
        SetAlpha(0f);
        EnterWaitingHidden(delay);
    }

    private void TickDrift()
    {
        Vector3 p = transform.position;
        p.x += _dir * _speed * Time.deltaTime;

        float bob = Mathf.Sin((Time.time / _bobPeriod) * Mathf.PI * 2f + _bobPhase) * _bobAmp;
        p.y = _baseY + bob;
        transform.position = p;

        float rotWobble = Mathf.Sin((Time.time / (_bobPeriod * 1.3f)) * Mathf.PI * 2f + _bobPhase) * 0.6f;
        transform.localEulerAngles = new Vector3(0f, 0f, _baseRotZ + rotWobble);
    }

    private bool IsOffBounds()
    {
        float x = transform.position.x;
        return x > areaMax.x + _wrapPadding || x < areaMin.x - _wrapPadding;
    }
}
