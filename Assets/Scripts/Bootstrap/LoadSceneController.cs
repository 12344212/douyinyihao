using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LoadScene 控制器（与参考项目一致，挂在 LoadScene 的 Canvas 上）。
/// 流程：YooAsset 初始化 → 本地存档就绪 → 分段预加载（图片 / 预制体 / 配置）→ IAP 初始化 → 进度补满
///       → DOTween 进度条补满（至少 minLoadSeconds）→ Start 按钮淡入 + 循环呼吸 → 点击后异步加载 MainScene。
/// YooAsset 初始化失败时显示英文错误面板 + Retry 按钮（重新走一遍流程）。
/// 所有引用都通过 Inspector 序列化字段赋值（由编辑器脚本 LoadSceneUISetup 生成并绑定），不使用 Find / GetComponent 查找其它物体。
/// </summary>
public class LoadSceneController : MonoBehaviour
{
    /// <summary>主场景名（与 Build Settings 中的 MainScene 一致）。</summary>
    public const string MainSceneName = "MainScene";

    // 进度分配：初始化 0~0.3 / 存档 0.3~0.35 / 图片 ~0.6 / 预制体 ~0.8 / 配置 ~0.95 / 收尾 ~1.0
    private const float InitEnd = 0.3f;
    private const float SaveEnd = 0.35f;
    private const float ImagesEnd = 0.6f;
    private const float PrefabsEnd = 0.8f;
    private const float ConfigsEnd = 0.95f;

    [Header("Managers")]
    [SerializeField] private YooAssetManager yooAssetManager;
    [SerializeField] private LocalSaveManager localSaveManager;
    [SerializeField] private IapPurchaseManager iapPurchaseManager;
    [SerializeField] private ItemConfig itemConfig;
    [SerializeField] private GemPackConfig gemPackConfig;

    [Header("Progress")]
    [SerializeField] private RectTransform progressBarRoot; // JDTBG
    [SerializeField] private Image progressFillImage;       // JD（Filled Horizontal）
    [SerializeField] private TMP_Text progressText;         // 百分比
    [SerializeField] private TMP_Text statusText;           // 状态文字

    [Header("Start")]
    [SerializeField] private Button startButton;
    [SerializeField] private CanvasGroup startButtonGroup;  // 淡入用
    [SerializeField] private TMP_Text startButtonLabel;

    [Header("Error")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button retryButton;

    [Header("Flow")]
    [SerializeField] private string nextSceneName = MainSceneName;
    [SerializeField] private float minLoadSeconds = 1f;
    [SerializeField] private float fillTweenDuration = 0.25f;

    [Header("Start Button Tween")]
    [SerializeField] private float startFadeDuration = 0.4f;
    [SerializeField] private float startPulseScale = 1.08f;
    [SerializeField] private float startPulseDuration = 0.8f;
    [SerializeField] private Ease startPulseEase = Ease.InOutSine;

    [Header("Optional preload overrides (empty = collect by type from YooAsset)")]
    [SerializeField] private string[] imageLocations;
    [SerializeField] private string[] prefabLocations;
    [SerializeField] private string[] configLocations;

    private Tween _fillTween;
    private Tween _startFadeTween;
    private Tween _startPulseTween;
    private float _displayedFill;
    private Coroutine _flow;
    private YooAssetManager _yoo;
    private bool _subscribedYoo;
    private bool _loadingNextScene;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryClicked);
        }
    }

    private void Start()
    {
        BeginFlow();
    }

    private void OnDestroy()
    {
        UnsubscribeYoo();
        KillFillTween();
        KillStartTweens();

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
        }
    }

    // ---------- 流程 ----------

    private void BeginFlow()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
        }

        ResetUI();
        _flow = StartCoroutine(RunLoadFlow());
    }

    private IEnumerator RunLoadFlow()
    {
        float startTime = Time.realtimeSinceStartup;

        // 1. YooAsset 初始化（单例优先：重复进入 LoadScene 时场景里的新实例会被销毁）
        _yoo = YooAssetManager.Instance != null ? YooAssetManager.Instance : yooAssetManager;
        if (_yoo == null)
        {
            ShowError("Resource manager is missing.", "YooAssetManager is not assigned in the Inspector.");
            yield break;
        }

        if (!_yoo.IsReady)
        {
            SetStatus(string.IsNullOrEmpty(_yoo.StatusText) || _yoo.StatusText == "Idle" ? "Loading..." : _yoo.StatusText);
            _yoo.InitProgressChanged += OnYooInitProgress;
            _subscribedYoo = true;
            yield return _yoo.InitializeAsync(); // 正在初始化时会等待；上次失败会重试
            UnsubscribeYoo();
        }

        if (!_yoo.IsReady)
        {
            ShowError("Failed to load game resources.", _yoo.LastError);
            yield break;
        }

        TweenToFill(InitEnd, fillTweenDuration);

        // 2. 本地存档（LocalSaveManager 在 Awake 已读取，这里只做兜底）
        SetStatus("Loading save data...");
        LocalSaveManager save = LocalSaveManager.Instance != null ? LocalSaveManager.Instance : localSaveManager;
        if (save == null)
        {
            Debug.LogError("[LoadSceneController] LocalSaveManager is not assigned in the Inspector.");
        }
        else if (!save.IsLoaded)
        {
            save.LoadOrCreate();
        }

        TweenToFill(SaveEnd, fillTweenDuration);
        yield return null;

        // 3. 预加载（收集目录为空时直接走完对应进度段）
        SetStatus("Loading images...");
        yield return RunPhase(ResolveLocations(imageLocations, typeof(Sprite)), typeof(Sprite), SaveEnd, ImagesEnd);

        SetStatus("Loading prefabs...");
        yield return RunPhase(ResolveLocations(prefabLocations, typeof(GameObject)), typeof(GameObject), ImagesEnd, PrefabsEnd);

        SetStatus("Loading configs...");
        yield return RunPhase(ResolveLocations(configLocations, typeof(TextAsset), typeof(ScriptableObject)), null, PrefabsEnd, ConfigsEnd);
        yield return BindGameConfigs();

        // 4. IAP 初始化（商品 id 来自 GemPackConfig）
        yield return InitializeIap();

        // 5. 收尾：补满进度，保证至少显示 minLoadSeconds
        SetStatus("Almost ready...");
        float remain = minLoadSeconds - (Time.realtimeSinceStartup - startTime);
        float tail = Mathf.Max(fillTweenDuration, remain);
        TweenToFill(1f, tail);
        yield return new WaitForSecondsRealtime(tail);

        SetStatus("Ready");
        _flow = null;
        ShowStartButton();
    }

    /// <summary>
    /// 初始化 Unity IAP。优先用 Inspector 上的 IapPurchaseManager，没有再退回单例。
    /// 失败或超时不阻止进入游戏。
    /// </summary>
    /// <summary>
    /// 按文件名地址加载 ItemConfig / GemPackConfig。YooAsset 成功就用加载结果，否则用 Inspector 上的引用。
    /// </summary>
    private IEnumerator BindGameConfigs()
    {
        if (itemConfig != null)
        {
            GameItemDefs.Bind(itemConfig);
        }

        if (gemPackConfig != null)
        {
            GemPackDefs.Bind(gemPackConfig);
        }

        if (_yoo != null && _yoo.IsReady)
        {
            ItemConfig loadedItems = null;
            yield return _yoo.LoadConfigAsync<ItemConfig>(GameItemDefs.ConfigAddress, c => loadedItems = c);
            if (loadedItems != null)
            {
                GameItemDefs.Bind(loadedItems);
            }

            GemPackConfig loadedPacks = null;
            yield return _yoo.LoadConfigAsync<GemPackConfig>(GemPackDefs.ConfigAddress, c => loadedPacks = c);
            if (loadedPacks != null)
            {
                GemPackDefs.Bind(loadedPacks);
            }
        }

        if (GameItemDefs.Current == null)
        {
            Debug.LogWarning("[LoadSceneController] ItemConfig is not loaded.");
        }

        if (GemPackDefs.Current == null)
        {
            Debug.LogWarning("[LoadSceneController] GemPackConfig is not loaded.");
        }
    }

    private IEnumerator InitializeIap()
    {
        SetStatus("Initializing store...");
        IapPurchaseManager iap = iapPurchaseManager != null ? iapPurchaseManager : IapPurchaseManager.Instance;
        if (iap == null)
        {
            Debug.LogWarning("[LoadSceneController] IapPurchaseManager is not assigned.");
            yield break;
        }

        yield return iap.InitializeAsync();
    }

    private IEnumerator RunPhase(List<string> locations, Type assetType, float sliceStart, float sliceEnd)
    {
        // 为空也走完该进度段，保证进度分配稳定
        if (locations == null || locations.Count == 0)
        {
            TweenToFill(sliceEnd, fillTweenDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.15f, fillTweenDuration));
            yield break;
        }

        yield return _yoo.PreloadByLocations(locations, p =>
        {
            TweenToFill(Mathf.Lerp(sliceStart, sliceEnd, Mathf.Clamp01(p)), fillTweenDuration);
        }, assetType);

        TweenToFill(sliceEnd, fillTweenDuration);
        yield return new WaitForSecondsRealtime(fillTweenDuration * 0.5f);
    }

    private List<string> ResolveLocations(string[] overrideLocations, params Type[] types)
    {
        List<string> result = new List<string>(32);
        if (overrideLocations != null && overrideLocations.Length > 0)
        {
            for (int i = 0; i < overrideLocations.Length; i++)
            {
                if (!string.IsNullOrEmpty(overrideLocations[i]))
                {
                    result.Add(overrideLocations[i]);
                }
            }

            return result;
        }

        if (_yoo == null)
        {
            return result;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int t = 0; t < types.Length; t++)
        {
            List<string> part = _yoo.CollectLocationsByType(types[t]);
            for (int i = 0; i < part.Count; i++)
            {
                if (seen.Add(part[i]))
                {
                    result.Add(part[i]);
                }
            }
        }

        return result;
    }

    private void OnYooInitProgress(float progress, string status)
    {
        TweenToFill(Mathf.Lerp(0f, InitEnd, progress), fillTweenDuration);
        if (!string.IsNullOrEmpty(status))
        {
            SetStatus(status);
        }
    }

    private void UnsubscribeYoo()
    {
        if (_subscribedYoo && _yoo != null)
        {
            _yoo.InitProgressChanged -= OnYooInitProgress;
        }

        _subscribedYoo = false;
    }

    // ---------- 按钮 ----------

    private void OnStartClicked()
    {
        if (_loadingNextScene)
        {
            return;
        }

        if (string.IsNullOrEmpty(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[LoadSceneController] Scene '{nextSceneName}' is not in Build Settings.");
            return;
        }

        _loadingNextScene = true;
        StartCoroutine(LoadNextScene());
    }

    private IEnumerator LoadNextScene()
    {
        KillStartTweens();
        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.gameObject.SetActive(false);
        }

        SetActive(progressBarRoot, true);
        SetStatus("Entering game...");

        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneName);
        while (op != null && !op.isDone)
        {
            ApplyFill(Mathf.Clamp01(op.progress / 0.9f));
            yield return null;
        }
    }

    private void OnRetryClicked()
    {
        BeginFlow();
    }

    // ---------- UI ----------

    private void ResetUI()
    {
        KillFillTween();
        KillStartTweens();
        _displayedFill = 0f;

        if (progressFillImage != null)
        {
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        ApplyFill(0f);
        SetActive(progressBarRoot, true);
        SetStatus("Loading...");

        if (startButton != null)
        {
            startButton.interactable = true;
            startButton.gameObject.SetActive(false);
        }

        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
    }

    private void ShowStartButton()
    {
        SetActive(progressBarRoot, false);
        if (startButton == null)
        {
            Debug.LogWarning("[LoadSceneController] Start button is not assigned.");
            return;
        }

        KillStartTweens();
        Transform t = startButton.transform;
        t.localScale = Vector3.one;
        startButton.interactable = true;
        startButton.gameObject.SetActive(true);

        if (startButtonGroup != null)
        {
            startButtonGroup.alpha = 0f;
            _startFadeTween = DOTween.To(() => startButtonGroup.alpha, a => startButtonGroup.alpha = a, 1f, Mathf.Max(0.05f, startFadeDuration))
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(StartPulse);
        }
        else
        {
            StartPulse();
        }
    }

    private void StartPulse()
    {
        if (startButton == null)
        {
            return;
        }

        // 循环呼吸：1 ↔ startPulseScale
        _startPulseTween = startButton.transform.DOScale(Mathf.Max(1.01f, startPulseScale), Mathf.Max(0.2f, startPulseDuration))
            .SetEase(startPulseEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void ShowError(string message, string detail)
    {
        UnsubscribeYoo();
        KillFillTween();
        _flow = null;
        Debug.LogError($"[LoadSceneController] {message} {detail}");

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        if (errorPanel == null)
        {
            // 没有错误面板时退化为状态文字提示
            SetStatus(message);
            return;
        }

        SetActive(progressBarRoot, false);
        if (errorText != null)
        {
            string text = message + "\nPlease try again.";
            if (Debug.isDebugBuild && !string.IsNullOrEmpty(detail))
            {
                // 仅开发版显示技术细节
                text += "\n\n<size=60%>" + detail + "</size>";
            }

            errorText.text = text;
        }

        if (retryButton != null)
        {
            retryButton.interactable = true;
        }

        errorPanel.SetActive(true);
    }

    private void TweenToFill(float target, float duration)
    {
        KillFillTween();
        target = Mathf.Clamp01(target);
        if (progressFillImage == null && progressText == null)
        {
            _displayedFill = target;
            return;
        }

        _fillTween = DOTween.To(() => _displayedFill, ApplyFill, target, Mathf.Max(0.05f, duration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void ApplyFill(float value)
    {
        _displayedFill = value;
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = value;
        }

        if (progressText != null)
        {
            progressText.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    private static void SetActive(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void KillFillTween()
    {
        if (_fillTween != null && _fillTween.IsActive())
        {
            _fillTween.Kill();
        }

        _fillTween = null;
    }

    private void KillStartTweens()
    {
        if (_startFadeTween != null && _startFadeTween.IsActive())
        {
            _startFadeTween.Kill();
        }

        if (_startPulseTween != null && _startPulseTween.IsActive())
        {
            _startPulseTween.Kill();
        }

        _startFadeTween = null;
        _startPulseTween = null;

        if (startButton != null)
        {
            startButton.transform.localScale = Vector3.one;
        }

        if (startButtonGroup != null)
        {
            startButtonGroup.alpha = 1f;
        }
    }
}