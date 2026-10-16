using System;
using UnityEngine;

/// <summary>
/// 战斗场景卡通海面根控制器：双层底图（haidi）+ 多条独立波纹 + 池化高光。
/// 挂在原 BattleWater / Water 根节点上，保持 Transform 引用不断裂。
/// 底图为单层 haidi.png：等比 Cover 铺满。光斑沿右上到左下来回循环，
/// 不持续卷动，避免接缝撕裂。第二层视差默认关闭。
/// OceanBackground / 远近层 sortingOrder=2；波纹与高光保持 order=3。
/// 高光由 OceanHighlightController 单独管理；波纹生命周期在 OceanRippleFloat。
/// </summary>
[DisallowMultipleComponent]
public class OceanCartoonRoot : MonoBehaviour
{
    /// <summary>
    /// 单层 haidi.png 的 UV 波纹，用 MaterialPropertyBlock 写入，不复制材质。
    /// </summary>
    [Serializable]
    private class CausticLayerSettings
    {
        [Tooltip("往左下走出的距离，到这里就退回。不要超过 0.03，否则会露出接缝。")]
        [Range(0f, 0.05f)] public float amp = 0.02f;
        [Tooltip("空间频率：画面里有几道波。")]
        public float freq = 4.2f;
        [Tooltip("光斑起伏快慢，不是整体平移。整体平移调 Flow。")]
        public float speed = 0.24f;
        [Range(0f, 0.05f)] public float amp2 = 0.007f;
        public float freq2 = 2.6f;
        public float speed2 = 0.4f;
        [Tooltip("往返循环的快慢。约 1.25 时一轮 5 秒：往左下，再退回，然后重复。")]
        public float flow = 1.25f;
        [Tooltip("相位（弧度）。远近层错开，叠色后亮暗才会重组。")]
        public float phase;
    }

    [Header("贴图（haidi / bol切片 / gaoliang切片）")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("bol 切图切片；优先于单张 rippleSprite")]
    [SerializeField] private Sprite[] rippleSprites;
    [Tooltip("兼容旧单张；数组为空时回退")]
    [SerializeField] private Sprite rippleSprite;
    [Tooltip("gaoliang 切图切片；优先于单张 highlightSprite")]
    [SerializeField] private Sprite[] highlightSprites;
    [Tooltip("兼容旧单张；数组为空时回退")]
    [SerializeField] private Sprite highlightSprite;

    [Header("层级节点（可空，运行时自动创建）")]
    [Tooltip("远景底图（OceanBackground）；双层视差中作为 Far 层")]
    [SerializeField] private SpriteRenderer oceanBackground;
    [SerializeField] private Transform waveContainer;
    [SerializeField] private Transform highlightContainer;
    [SerializeField] private OceanHighlightController highlightController;

    [Header("排序（越小越靠后）")]
    [Tooltip("底图远/近层统一用此 order（默认 2）")]
    [SerializeField] private int backgroundOrder = 2;
    [SerializeField] private int rippleOrder = 3;
    [SerializeField] private int highlightOrder = 3;

    [Header("波纹数量/外观")]
    [SerializeField] private int rippleCount = 12;
    [Tooltip("bol 在 PPU=100 时约 5wu，需缩小")]
    [SerializeField] private float rippleBaseScale = 1f;
    [SerializeField] [Range(0.5f, 1f)] private float rippleAreaPadding = 0.92f;
    [Tooltip("初始按序号轮询切片；重生时由 OceanRippleFloat 随机")]
    [SerializeField] private bool rippleSequentialOnSpawn = true;

    [Header("波纹运动/生命周期（注入到各 OceanRippleFloat）")]
    [Tooltip("水平速度 wu/s，建议明显快于早期 0.03~0.12")]
    [SerializeField] private Vector2 rippleSpeedRange = new Vector2(0.22f, 0.65f);
    [Tooltip("完全可见阶段存活时间（秒）")]
    [SerializeField] private Vector2 rippleLifetimeRange = new Vector2(2.5f, 6.5f);
    [SerializeField] private float rippleFadeInDuration = 0.55f;
    [SerializeField] private float rippleFadeOutDuration = 0.7f;
    [Tooltip("渐隐结束后到再次出现的等待（秒）")]
    [SerializeField] private Vector2 rippleRespawnDelayRange = new Vector2(0.4f, 2.2f);
    [Tooltip("首次出现前错峰延迟（秒），避免全部同时可见")]
    [SerializeField] private Vector2 rippleInitialDelayRange = new Vector2(0f, 2.8f);

    [Header("底图覆盖")]
    [Tooltip("相对相机可视区域的覆盖倍率，>1 留边防止露空")]
    [SerializeField] private float coverPadding = 1.05f;
    [SerializeField] private Camera targetCamera;
    [Tooltip("勾选后把根节点 scale 置 1，由底图自身缩放铺满相机")]
    [SerializeField] private bool normalizeRootScale = true;

    [Header("高光并发")]
    [SerializeField] private int highlightMaxConcurrent = 3;

    [Header("双层视差底图（haidi.png）")]
    [Tooltip("关闭时只画一层底图。勾选才叠第二层视差。")]
    [SerializeField] private bool parallaxEnabled = false;
    [Tooltip("近景层；可空，运行时自动创建为 OceanBgNear")]
    [SerializeField] private SpriteRenderer oceanBgNear;
    [Tooltip("近景叠在远景上的透明度。未开循环时使用。")]
    [SerializeField] [Range(0f, 1f)] private float nearLayerAlpha = 0.3f;
    [Tooltip("显示 OceanBgNear。颜色 A 在下限和上限之间随机变化，不是固定来回。")]
    [SerializeField] private bool nearAlphaCycle = true;
    [Tooltip("颜色 A 的下限，和 Inspector 里 0–255 的 A 同一套")]
    [SerializeField] [Range(0f, 255f)] private float nearAlphaMin = 30f;
    [Tooltip("颜色 A 的上限，和 Inspector 里 0–255 的 A 同一套")]
    [SerializeField] [Range(0f, 255f)] private float nearAlphaMax = 100f;
    [Tooltip("单次变到下一个随机 A 的大致秒数，实际会在这附近随机")]
    [SerializeField] private float nearAlphaPeriod = 4f;
    [Tooltip("远景相对 Cover 的额外放大，给漂移留边")]
    [SerializeField] private float farScaleMultiplier = 1.06f;
    [Tooltip("近景相对 Cover 的额外放大，略大于远景更易露层次")]
    [SerializeField] private float nearScaleMultiplier = 1.1f;
    [Tooltip("远景漂移振幅（世界单位 XY）。光斑流动交给 caustic，这里宜接近 0")]
    [SerializeField] private Vector2 farDriftAmplitude = new Vector2(0.01f, 0.006f);
    [Tooltip("近景漂移振幅（世界单位 XY）。光斑流动交给 caustic，这里宜接近 0")]
    [SerializeField] private Vector2 nearDriftAmplitude = new Vector2(0.012f, 0.008f);
    [Tooltip("远景漂移周期（秒）：X=水平，Y=垂直；越大越慢")]
    [SerializeField] private Vector2 farDriftPeriod = new Vector2(20f, 26f);
    [Tooltip("近景漂移周期（秒）：X=水平，Y=垂直")]
    [SerializeField] private Vector2 nearDriftPeriod = new Vector2(12f, 15f);
    [Tooltip("远景相位偏移（弧度），避免与近景同步")]
    [SerializeField] private Vector2 farDriftPhaseOffset = new Vector2(0f, 1.2f);
    [Tooltip("近景相位偏移（弧度）")]
    [SerializeField] private Vector2 nearDriftPhaseOffset = new Vector2(2.1f, 0.4f);
    [Tooltip("远景色调（一般保持白色）")]
    [SerializeField] private Color farColor = Color.white;
    [Tooltip("近景色调；A 会被 nearLayerAlpha 覆盖")]
    [SerializeField] private Color nearColor = Color.white;

    [Header("底层光斑（单层 haidi）")]
    [Tooltip("速率在下面的 Flow：往左下走到 Amp 就退回，循环，不一直滑。")]
    [SerializeField] private Material causticMaterial;
    [SerializeField] private CausticLayerSettings caustic = new CausticLayerSettings();

    private static readonly int WaveAmpId = Shader.PropertyToID("_WaveAmp");
    private static readonly int WaveFreqId = Shader.PropertyToID("_WaveFreq");
    private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveAmp2Id = Shader.PropertyToID("_WaveAmp2");
    private static readonly int WaveFreq2Id = Shader.PropertyToID("_WaveFreq2");
    private static readonly int WaveSpeed2Id = Shader.PropertyToID("_WaveSpeed2");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private MaterialPropertyBlock _causticBlock;
    private MaterialPropertyBlock _nearCausticBlock;
    private float _alphaFrom;
    private float _alphaTo;
    private float _alphaSegmentDuration;
    private float _alphaSegmentStart;
    private bool _alphaSegmentReady;

    // 拟合后的锚点，漂移在此基础上做微小正弦偏移
    private Vector3 _farBaseWorldPos;
    private Vector3 _nearBaseWorldPos;
    private bool _bgFitted;

    private void Awake()
    {
        // 关掉旧 UV 波纹组件，避免与卡通层叠效果冲突
        BattleWaterWave legacy = GetComponent<BattleWaterWave>();
        if (legacy != null)
        {
            legacy.enabled = false;
        }

        EnsureHierarchy();
        EnsureParallaxLayers();
        ApplySpritesAndSorting();
        if (normalizeRootScale)
        {
            transform.localScale = Vector3.one;
        }

        FitBackgroundToCamera();
        SpawnOrRefreshRipples();
        ConfigureHighlights();
    }

    private void OnEnable()
    {
        _alphaSegmentReady = false;
        ApplyCausticLayers();
        // 相机比例可能在首帧后才稳定，再拟合一次
        if (oceanBackground != null)
        {
            FitBackgroundToCamera();
        }
    }

    private void Update()
    {
        TickNearAlphaCycle();

        if (!parallaxEnabled || !_bgFitted)
        {
            return;
        }

        TickParallaxDrift();
    }

    private Sprite[] GetRippleVariants()
    {
        if (rippleSprites != null && rippleSprites.Length > 0)
        {
            return rippleSprites;
        }

        if (rippleSprite != null)
        {
            return new[] { rippleSprite };
        }

        return System.Array.Empty<Sprite>();
    }

    private Sprite[] GetHighlightVariants()
    {
        if (highlightSprites != null && highlightSprites.Length > 0)
        {
            return highlightSprites;
        }

        if (highlightSprite != null)
        {
            return new[] { highlightSprite };
        }

        return System.Array.Empty<Sprite>();
    }

    private static Sprite PickVariant(Sprite[] variants, int index, bool sequential)
    {
        if (variants == null || variants.Length == 0)
        {
            return null;
        }

        if (sequential)
        {
            return variants[index % variants.Length];
        }

        return variants[UnityEngine.Random.Range(0, variants.Length)];
    }

    private void EnsureHierarchy()
    {
        // 若根上仍有旧 SpriteRenderer，优先复用为底图
        if (oceanBackground == null)
        {
            Transform bgT = transform.Find("OceanBackground");
            if (bgT != null)
            {
                oceanBackground = bgT.GetComponent<SpriteRenderer>();
            }
        }

        if (oceanBackground == null)
        {
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null)
            {
                // 把根上的渲染器「降级」为子节点，保持排序语义清晰
                GameObject bgGo = new GameObject("OceanBackground");
                bgGo.transform.SetParent(transform, false);
                bgGo.transform.localPosition = Vector3.zero;
                bgGo.transform.localScale = Vector3.one;
                oceanBackground = bgGo.AddComponent<SpriteRenderer>();
                oceanBackground.sprite = rootSr.sprite;
                oceanBackground.color = rootSr.color;
                oceanBackground.sortingOrder = rootSr.sortingOrder;
                // 禁用根渲染器，避免双绘
                rootSr.enabled = false;
                rootSr.sprite = null;
            }
            else
            {
                GameObject bgGo = new GameObject("OceanBackground");
                bgGo.transform.SetParent(transform, false);
                oceanBackground = bgGo.AddComponent<SpriteRenderer>();
            }
        }

        if (waveContainer == null)
        {
            Transform t = transform.Find("WaveContainer");
            if (t == null)
            {
                GameObject go = new GameObject("WaveContainer");
                go.transform.SetParent(transform, false);
                waveContainer = go.transform;
            }
            else
            {
                waveContainer = t;
            }
        }

        if (highlightContainer == null)
        {
            Transform t = transform.Find("HighlightContainer");
            if (t == null)
            {
                GameObject go = new GameObject("HighlightContainer");
                go.transform.SetParent(transform, false);
                highlightContainer = go.transform;
            }
            else
            {
                highlightContainer = t;
            }
        }

        if (highlightController == null)
        {
            highlightController = highlightContainer.GetComponent<OceanHighlightController>();
            if (highlightController == null)
            {
                highlightController = highlightContainer.gameObject.AddComponent<OceanHighlightController>();
            }
        }
    }

    /// <summary>
    /// 确保近景层存在：与 OceanBackground（远景）同级，共用 haidi 贴图。
    /// </summary>
    private void EnsureParallaxLayers()
    {
        if (oceanBgNear == null)
        {
            Transform nearT = transform.Find("OceanBgNear");
            if (nearT != null)
            {
                oceanBgNear = nearT.GetComponent<SpriteRenderer>();
            }
        }

        if (oceanBgNear == null)
        {
            GameObject nearGo = new GameObject("OceanBgNear");
            // 插在 OceanBackground 之后，便于 Hierarchy 阅读
            int insertAt = 0;
            if (oceanBackground != null)
            {
                insertAt = oceanBackground.transform.GetSiblingIndex() + 1;
            }

            nearGo.transform.SetParent(transform, false);
            nearGo.transform.SetSiblingIndex(insertAt);
            nearGo.transform.localPosition = Vector3.zero;
            nearGo.transform.localRotation = Quaternion.identity;
            nearGo.transform.localScale = Vector3.one;
            oceanBgNear = nearGo.AddComponent<SpriteRenderer>();
        }

        // 近景略靠前（更小 z），同 sortingOrder 时仍压在远景之上
        if (oceanBackground != null)
        {
            Vector3 fp = oceanBackground.transform.localPosition;
            oceanBackground.transform.localPosition = new Vector3(fp.x, fp.y, 0f);
        }

        if (oceanBgNear != null)
        {
            Vector3 np = oceanBgNear.transform.localPosition;
            oceanBgNear.transform.localPosition = new Vector3(np.x, np.y, -0.01f);
        }
    }

    private void ApplySpritesAndSorting()
    {
        Sprite bg = backgroundSprite;
        if (bg == null && oceanBackground != null)
        {
            bg = oceanBackground.sprite;
        }

        if (oceanBackground != null)
        {
            if (bg != null)
            {
                oceanBackground.sprite = bg;
            }

            oceanBackground.sortingOrder = backgroundOrder;
            Color fc = farColor;
            fc.a = 1f;
            oceanBackground.color = fc;
        }

        if (oceanBgNear != null)
        {
            // 保留 OceanBgNear 自己的图；空了才回退到底图。
            if (oceanBgNear.sprite == null)
            {
                oceanBgNear.sprite = bg != null ? bg : (oceanBackground != null ? oceanBackground.sprite : null);
            }

            bool showNear = parallaxEnabled || nearAlphaCycle;
            oceanBgNear.sortingOrder = backgroundOrder;
            Color nc = nearColor;
            nc.a = nearAlphaCycle ? Alpha255ToUnit(nearAlphaMin) : Mathf.Clamp01(nearLayerAlpha);
            oceanBgNear.color = nc;
            oceanBgNear.enabled = showNear;
            oceanBgNear.gameObject.SetActive(showNear);
        }

        ApplyCausticLayers();
    }

    /// <summary>
    /// 底图等比铺满正交相机视野（Cover），不拉伸变形；远/近各自额外放大以容纳微小漂移。
    /// </summary>
    public void FitBackgroundToCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        float worldH = cam.orthographicSize * 2f * coverPadding;
        float worldW = worldH * cam.aspect;
        Vector3 camPos = cam.transform.position;
        Vector3 rootPos = transform.position;
        Vector3 center = new Vector3(camPos.x, camPos.y, rootPos.z);

        FitOneLayer(oceanBackground, center, worldW, worldH, farScaleMultiplier, 0f);
        if (oceanBackground != null)
        {
            _farBaseWorldPos = oceanBackground.transform.position;
        }

        if ((parallaxEnabled || nearAlphaCycle) && oceanBgNear != null && oceanBgNear.sprite != null)
        {
            FitOneLayer(oceanBgNear, center, worldW, worldH, nearScaleMultiplier, -0.01f);
            _nearBaseWorldPos = oceanBgNear.transform.position;
        }

        _bgFitted = oceanBackground != null && oceanBackground.sprite != null;

        // 拟合后立刻应用一帧漂移，避免首帧跳变
        if (parallaxEnabled && _bgFitted)
        {
            TickParallaxDrift();
        }
    }

    private static void FitOneLayer(
        SpriteRenderer sr,
        Vector3 worldCenter,
        float worldW,
        float worldH,
        float scaleMul,
        float localZ)
    {
        if (sr == null || sr.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = sr.sprite.bounds.size;
        if (spriteSize.x < 0.0001f || spriteSize.y < 0.0001f)
        {
            return;
        }

        float cover = Mathf.Max(worldW / spriteSize.x, worldH / spriteSize.y);
        float scale = cover * Mathf.Max(1f, scaleMul);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.position = new Vector3(worldCenter.x, worldCenter.y, worldCenter.z + localZ);
    }

    /// <summary>
    /// 远/近层各自正弦微移：振幅小、周期长，不造成整幅卷轴感。
    /// </summary>
    private void TickParallaxDrift()
    {
        float t = Time.time;

        if (oceanBackground != null)
        {
            Vector2 amp = farDriftAmplitude;
            Vector2 period = farDriftPeriod;
            Vector2 phase = farDriftPhaseOffset;
            float ox = amp.x * Mathf.Sin(Omega(period.x) * t + phase.x);
            float oy = amp.y * Mathf.Sin(Omega(period.y) * t + phase.y);
            Vector3 p = _farBaseWorldPos;
            p.x += ox;
            p.y += oy;
            oceanBackground.transform.position = p;
        }

        if (oceanBgNear != null && oceanBgNear.gameObject.activeInHierarchy)
        {
            Vector2 amp = nearDriftAmplitude;
            Vector2 period = nearDriftPeriod;
            Vector2 phase = nearDriftPhaseOffset;
            float ox = amp.x * Mathf.Sin(Omega(period.x) * t + phase.x);
            float oy = amp.y * Mathf.Sin(Omega(period.y) * t + phase.y);
            Vector3 p = _nearBaseWorldPos;
            p.x += ox;
            p.y += oy;
            oceanBgNear.transform.position = p;
        }
    }

    /// <summary>
    /// 单层底图写 caustic 参数。第二层视差开启时才给近景再写一份。
    /// </summary>
    private void ApplyCausticLayers()
    {
        if (causticMaterial == null)
        {
            Shader shader = Shader.Find("Battle/OceanCausticRipple");
            if (shader == null)
            {
                Debug.LogWarning("[OceanCartoonRoot] Shader Battle/OceanCausticRipple not found. Assign causticMaterial.");
                return;
            }

            causticMaterial = new Material(shader);
            causticMaterial.name = "OceanCausticRipple (Runtime)";
        }

        ApplyCausticLayer(oceanBackground, caustic, ref _causticBlock);
        if (oceanBgNear != null && oceanBgNear.gameObject.activeInHierarchy)
        {
            ApplyCausticLayer(oceanBgNear, caustic, ref _nearCausticBlock);
        }
    }

    /// <summary>
    /// OceanBgNear 的颜色 A 在 30–100 内随机走到下一个值，时间和落点都不固定。
    /// </summary>
    private void TickNearAlphaCycle()
    {
        if (!nearAlphaCycle || oceanBgNear == null || !oceanBgNear.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!_alphaSegmentReady || Time.time >= _alphaSegmentStart + _alphaSegmentDuration)
        {
            BeginNextAlphaSegment();
        }

        float t = Mathf.Clamp01((Time.time - _alphaSegmentStart) / Mathf.Max(0.05f, _alphaSegmentDuration));
        float eased = t * t * (3f - 2f * t);
        float alpha255 = Mathf.Lerp(_alphaFrom, _alphaTo, eased);
        Color c = nearColor;
        c.a = Alpha255ToUnit(alpha255);
        oceanBgNear.color = c;
    }

    private void BeginNextAlphaSegment()
    {
        float min = Mathf.Min(nearAlphaMin, nearAlphaMax);
        float max = Mathf.Max(nearAlphaMin, nearAlphaMax);
        _alphaFrom = _alphaSegmentReady ? _alphaTo : UnityEngine.Random.Range(min, max);

        float next = _alphaFrom;
        float minStep = Mathf.Max(8f, (max - min) * 0.2f);
        for (int i = 0; i < 5; i++)
        {
            next = UnityEngine.Random.Range(min, max);
            if (Mathf.Abs(next - _alphaFrom) >= minStep)
            {
                break;
            }
        }

        _alphaTo = next;
        float baseDuration = Mathf.Max(0.2f, nearAlphaPeriod);
        _alphaSegmentDuration = UnityEngine.Random.Range(baseDuration * 0.45f, baseDuration * 1.5f);
        _alphaSegmentStart = Time.time;
        _alphaSegmentReady = true;
    }

    private static float Alpha255ToUnit(float alpha255)
    {
        return Mathf.Clamp(alpha255, 0f, 255f) / 255f;
    }

    private void ApplyCausticLayer(SpriteRenderer sr, CausticLayerSettings settings, ref MaterialPropertyBlock block)
    {
        if (sr == null || settings == null || causticMaterial == null)
        {
            return;
        }

        if (sr.sharedMaterial != causticMaterial)
        {
            sr.sharedMaterial = causticMaterial;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        sr.GetPropertyBlock(block);
        // SpriteRenderer 的贴图走 PropertyBlock；用户块覆盖后把 haidi 贴图写回去。
        if (sr.sprite != null && sr.sprite.texture != null)
        {
            block.SetTexture(MainTexId, sr.sprite.texture);
        }

        block.SetFloat(WaveAmpId, settings.amp);
        block.SetFloat(WaveFreqId, settings.freq);
        block.SetFloat(WaveSpeedId, settings.speed);
        block.SetFloat(WaveAmp2Id, settings.amp2);
        block.SetFloat(WaveFreq2Id, settings.freq2);
        block.SetFloat(WaveSpeed2Id, settings.speed2);
        block.SetFloat(FlowSpeedId, settings.flow);
        block.SetFloat(PhaseId, settings.phase);
        sr.SetPropertyBlock(block);
    }

    private static float Omega(float periodSeconds)
    {
        float p = Mathf.Max(0.5f, periodSeconds);
        return (Mathf.PI * 2f) / p;
    }

    private Bounds GetVisibleSeaBounds()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize * rippleAreaPadding;
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Bounds(c, new Vector3(w * 2f, h * 2f, 0f));
        }

        // 回退：用底图包围盒
        if (oceanBackground != null)
        {
            return oceanBackground.bounds;
        }

        return new Bounds(transform.position, new Vector3(20f, 14f, 0f));
    }

    private void SpawnOrRefreshRipples()
    {
        Sprite[] variants = GetRippleVariants();
        if (waveContainer == null || variants.Length == 0)
        {
            return;
        }

        Bounds b = GetVisibleSeaBounds();
        Vector2 min = new Vector2(b.min.x, b.min.y);
        Vector2 max = new Vector2(b.max.x, b.max.y);

        // 复用已有子节点，不够再创建；多出的隐藏
        int existing = waveContainer.childCount;
        int need = Mathf.Max(0, rippleCount);

        for (int i = 0; i < need; i++)
        {
            OceanRippleFloat ripple;
            SpriteRenderer sr;
            if (i < existing)
            {
                Transform child = waveContainer.GetChild(i);
                child.gameObject.SetActive(true);
                ripple = child.GetComponent<OceanRippleFloat>();
                if (ripple == null)
                {
                    ripple = child.gameObject.AddComponent<OceanRippleFloat>();
                }

                sr = child.GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    sr = child.gameObject.AddComponent<SpriteRenderer>();
                }
            }
            else
            {
                GameObject go = new GameObject($"Wave_{i + 1:00}");
                go.transform.SetParent(waveContainer, false);
                sr = go.AddComponent<SpriteRenderer>();
                ripple = go.AddComponent<OceanRippleFloat>();
            }

            Sprite picked = PickVariant(variants, i, rippleSequentialOnSpawn);
            sr.sprite = picked;
            sr.sortingOrder = rippleOrder;
            Color col = sr.color;
            col.r = col.g = col.b = 1f;
            // 起始透明，由生命周期渐显；避免首帧全亮
            col.a = 0f;
            sr.color = col;

            ripple.SetSpriteVariants(variants);
            ripple.ConfigureArea(min, max);
            ripple.SetScaleMultiplier(rippleBaseScale);
            // 根节点 Inspector 参数注入到每条波纹
            ripple.ApplyMotionAndLifecycle(
                rippleSpeedRange,
                rippleLifetimeRange,
                rippleFadeInDuration,
                rippleFadeOutDuration,
                rippleRespawnDelayRange,
                rippleInitialDelayRange);
            // 错峰首次出现：隐藏 → 随机等待 → 渐显 → 漂移 → 渐隐 → 等待 → 换位重生
            ripple.BeginLifecycle(staggerFirstAppear: true);
        }

        for (int i = need; i < existing; i++)
        {
            waveContainer.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void ConfigureHighlights()
    {
        if (highlightController == null)
        {
            return;
        }

        Bounds b = GetVisibleSeaBounds();
        Vector2 min = new Vector2(b.min.x, b.min.y);
        Vector2 max = new Vector2(b.max.x, b.max.y);
        Sprite[] variants = GetHighlightVariants();
        highlightController.Configure(variants, min, max, highlightOrder, highlightMaxConcurrent);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rippleCount = Mathf.Clamp(rippleCount, 0, 64);
        highlightMaxConcurrent = Mathf.Clamp(highlightMaxConcurrent, 0, 16);
        rippleFadeInDuration = Mathf.Max(0.01f, rippleFadeInDuration);
        rippleFadeOutDuration = Mathf.Max(0.01f, rippleFadeOutDuration);
        nearLayerAlpha = Mathf.Clamp01(nearLayerAlpha);
        nearAlphaMin = Mathf.Clamp(nearAlphaMin, 0f, 255f);
        nearAlphaMax = Mathf.Clamp(nearAlphaMax, nearAlphaMin, 255f);
        nearAlphaPeriod = Mathf.Max(0.2f, nearAlphaPeriod);
        farScaleMultiplier = Mathf.Max(1f, farScaleMultiplier);
        nearScaleMultiplier = Mathf.Max(1f, nearScaleMultiplier);
        farDriftPeriod.x = Mathf.Max(0.5f, farDriftPeriod.x);
        farDriftPeriod.y = Mathf.Max(0.5f, farDriftPeriod.y);
        nearDriftPeriod.x = Mathf.Max(0.5f, nearDriftPeriod.x);
        nearDriftPeriod.y = Mathf.Max(0.5f, nearDriftPeriod.y);
        // 振幅钳制：防止误调成「大卷轴」
        farDriftAmplitude.x = Mathf.Clamp(farDriftAmplitude.x, 0f, 0.5f);
        farDriftAmplitude.y = Mathf.Clamp(farDriftAmplitude.y, 0f, 0.5f);
        nearDriftAmplitude.x = Mathf.Clamp(nearDriftAmplitude.x, 0f, 0.75f);
        nearDriftAmplitude.y = Mathf.Clamp(nearDriftAmplitude.y, 0f, 0.75f);
        backgroundOrder = 2;
        // 波纹/高光保持在底图之上
        if (rippleOrder <= backgroundOrder)
        {
            rippleOrder = backgroundOrder + 1;
        }

        if (highlightOrder <= backgroundOrder)
        {
            highlightOrder = backgroundOrder + 1;
        }

        if (caustic != null)
        {
            caustic.amp = Mathf.Clamp(caustic.amp, 0f, 0.05f);
            caustic.amp2 = Mathf.Clamp(caustic.amp2, 0f, 0.05f);
        }

        if (Application.isPlaying)
        {
            ApplyCausticLayers();
        }
    }
#endif
}
