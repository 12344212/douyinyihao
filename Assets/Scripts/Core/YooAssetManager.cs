using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YooAsset;

/// <summary>
/// YooAsset 资源管理器（YooAsset 3.0.6）。
/// - 初始化 DefaultPackage：编辑器下 EditorSimulateMode（模拟构建），真机 OfflinePlayMode（资源内置于 StreamingAssets/yoo）。
/// - 提供 Sprite / Prefab / 配置 / 任意资源的异步加载、按地址列表或按类型预加载、释放与卸载。
/// - 挂在首个场景（Loading 场景）的物体上，由其它脚本通过 Inspector 拖引用；DontDestroyOnLoad 常驻，禁止运行时 AddComponent。
/// - 地址规则与 Assets/BundleCollectorSetting.asset 一致：EnableAddressable 关闭，location 可以是
///   资源路径（可省略扩展名，如 "Assets/UI/Common/btn_ok"）或文件名（如 "btn_ok"，内部映射到资源路径）。
/// </summary>
[DisallowMultipleComponent]
public class YooAssetManager : MonoBehaviour
{
    /// <summary>与 BundleCollectorSetting.asset 中的 PackageName 必须一致。</summary>
    public const string DefaultPackageName = "DefaultPackage";

    public static YooAssetManager Instance { get; private set; }

    [Tooltip("资源包名，需与 YooAsset 收集器配置中的 PackageName 一致")]
    [SerializeField] private string packageName = DefaultPackageName;

    [Tooltip("Start 时自动初始化。若由 Loading 场景脚本主动调用 InitializeAsync，可保持开启（重复调用安全）")]
    [SerializeField] private bool initializeOnStart = true;

    [Tooltip("加载资源清单的超时时间（秒）")]
    [SerializeField] private int manifestTimeoutSeconds = 60;

    private ResourcePackage _package;

    // 缓存：资源路径（规范化后的 AssetPath） -> (加载类型 -> 句柄)
    private readonly Dictionary<string, Dictionary<Type, AssetHandle>> _handleCache =
        new Dictionary<string, Dictionary<Type, AssetHandle>>(128, StringComparer.Ordinal);

    // 文件名（无扩展名） -> 资源路径，用于 EnableAddressable 关闭时按文件名加载
    private Dictionary<string, string> _fileNameToPath;
    private bool _initRunning;
    private bool _isQuitting;

    public bool IsReady { get; private set; }
    public bool IsInitFailed { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public ResourcePackage Package => _package;
    public string PackageName => packageName;

    /// <summary>初始化进度 0~1，供 Loading 场景进度条使用。</summary>
    public float InitProgress { get; private set; }

    /// <summary>当前初始化状态描述（英文，可直接显示）。</summary>
    public string StatusText { get; private set; } = "Idle";

    /// <summary>初始化结束回调：参数 true = 成功，false = 失败（看 LastError）。</summary>
    public event Action<bool> InitCompleted;

    /// <summary>初始化进度变化回调：(进度 0~1, 状态文本)。</summary>
    public event Action<float, string> InitProgressChanged;

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroy 延后到帧末，先关闭组件，避免本帧 Start 重复初始化
            enabled = false;
            Destroy(gameObject);
            return;
        }

        if (string.IsNullOrEmpty(packageName))
        {
            packageName = DefaultPackageName;
        }

        Instance = this;
        if (transform.parent != null)
        {
            // DontDestroyOnLoad 只对根物体生效
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (Instance != this || !enabled)
        {
            return;
        }

        if (initializeOnStart && !IsReady && !_initRunning)
        {
            StartCoroutine(InitializeAsync());
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        // 退出时 YooAssets 会自行销毁，无需再释放句柄
        if (!_isQuitting)
        {
            ReleaseAll();
        }

        Instance = null;
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化 YooAssets 与资源包：创建包 → InitializePackageAsync → RequestPackageVersionAsync → LoadPackageManifestAsync。
    /// 可重复调用：已就绪直接返回；正在初始化则等待；上次失败则重试。
    /// </summary>
    public IEnumerator InitializeAsync()
    {
        if (IsReady && _package != null)
        {
            yield break;
        }

        if (_initRunning)
        {
            while (_initRunning)
            {
                yield return null;
            }

            yield break;
        }

        _initRunning = true;
        IsReady = false;
        IsInitFailed = false;
        LastError = string.Empty;
        SetProgress(0f, "Initializing resources...");

        if (!YooAssets.IsInitialized)
        {
            YooAssets.Initialize();
        }

        if (!YooAssets.TryGetPackage(packageName, out ResourcePackage package))
        {
            package = YooAssets.CreatePackage(packageName);
        }

        _package = package;
        SetProgress(0.1f, "Initializing package...");

        while (package.InitializeStatus == EOperationStatus.Processing)
        {
            yield return null;
        }

        // 1. 初始化资源包（失败状态下再次调用会由 YooAsset 自动重置）
        if (package.InitializeStatus != EOperationStatus.Succeeded)
        {
            InitializePackageOptions options = CreateInitializeOptions(out string createError);
            if (options == null)
            {
                Fail(createError);
                yield break;
            }

            InitializePackageOperation initOp = package.InitializePackageAsync(options);
            while (!initOp.IsDone)
            {
                SetProgress(0.1f + 0.25f * initOp.Progress, "Initializing package...");
                yield return null;
            }

            if (initOp.Status != EOperationStatus.Succeeded)
            {
                Fail("Package initialize failed: " + initOp.Error);
                yield break;
            }
        }

        // 2. 请求版本 + 加载清单（包已初始化但清单缺失时也走这里，便于重试）
        if (!package.PackageValid)
        {
            SetProgress(0.35f, "Checking resource version...");
            RequestPackageVersionOperation versionOp = package.RequestPackageVersionAsync();
            while (!versionOp.IsDone)
            {
                SetProgress(0.35f + 0.2f * versionOp.Progress, "Checking resource version...");
                yield return null;
            }

            if (versionOp.Status != EOperationStatus.Succeeded)
            {
                Fail("Request package version failed: " + versionOp.Error);
                yield break;
            }

            SetProgress(0.55f, "Loading resource manifest...");
            var manifestOptions = new LoadPackageManifestOptions(versionOp.PackageVersion, manifestTimeoutSeconds);
            LoadPackageManifestOperation manifestOp = package.LoadPackageManifestAsync(manifestOptions);
            while (!manifestOp.IsDone)
            {
                SetProgress(0.55f + 0.4f * manifestOp.Progress, "Loading resource manifest...");
                yield return null;
            }

            if (manifestOp.Status != EOperationStatus.Succeeded)
            {
                Fail("Load package manifest failed: " + manifestOp.Error);
                yield break;
            }
        }

        MarkReady();
    }

    private InitializePackageOptions CreateInitializeOptions(out string error)
    {
        error = null;
#if UNITY_EDITOR
        try
        {
            // 编辑器模拟构建：按收集器配置生成虚拟清单，无需真正打包
            PackageBuildResult buildResult = EditorSimulateBuildInvoker.Build(
                packageName,
                (int)EBundleType.VirtualAssetBundle);
            return new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                    buildResult.PackageRootDirectory)
            };
        }
        catch (Exception e)
        {
            Exception inner = e;
            while (inner.InnerException != null)
            {
                inner = inner.InnerException;
            }

            error = "Editor simulate build failed: " + inner.Message +
                    " (check Assets/BundleCollectorSetting.asset; collect folders Assets/UI, Assets/Config, Assets/Prefab need at least one asset)";
            return null;
        }
#else
        // 离线模式：资源随包内置（StreamingAssets/yoo/<PackageName>）
        return new OfflinePlayModeOptions
        {
            BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
        };
#endif
    }

    private void SetProgress(float progress, string status)
    {
        InitProgress = Mathf.Clamp01(progress);
        StatusText = status;
        InitProgressChanged?.Invoke(InitProgress, StatusText);
    }

    private void MarkReady()
    {
        _fileNameToPath = null;
        IsReady = true;
        IsInitFailed = false;
        _initRunning = false;
        SetProgress(1f, "Ready");
        InitCompleted?.Invoke(true);
    }

    private void Fail(string error)
    {
        LastError = error ?? "Unknown error";
        IsInitFailed = true;
        IsReady = false;
        _initRunning = false;
        StatusText = "Resource initialization failed";
        Debug.LogError("[YooAssetManager] " + LastError);
        InitProgressChanged?.Invoke(InitProgress, StatusText);
        InitCompleted?.Invoke(false);
    }

    /// <summary>等待单例出现并就绪（场景内脚本在协程中 yield return 即可）。</summary>
    public static IEnumerator WaitInstanceReady(float timeoutSeconds = 30f)
    {
        float wait = 0f;
        while (Instance == null && wait < timeoutSeconds)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (Instance == null)
        {
            Debug.LogWarning("[YooAssetManager] Instance not found. Start the game from the Loading scene.");
            yield break;
        }

        yield return Instance.WaitUntilReady(timeoutSeconds);
    }

    /// <summary>等待本实例就绪；未在初始化则主动发起初始化。</summary>
    public IEnumerator WaitUntilReady(float timeoutSeconds = 30f)
    {
        if (IsReady)
        {
            yield break;
        }

        if (!_initRunning)
        {
            yield return InitializeAsync();
            yield break;
        }

        float wait = 0f;
        while (!IsReady && _initRunning && wait < timeoutSeconds)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    #endregion

    #region 加载

    /// <summary>
    /// 异步加载资源，返回 YooAsset 句柄（可 yield return）。同一资源同一类型重复加载返回缓存句柄。
    /// 句柄由管理器持有，请通过 Release(location) / ReleaseAll() 释放，不要直接调用 handle.Release()。
    /// </summary>
    public AssetHandle LoadAssetAsync<T>(string location) where T : UnityEngine.Object
    {
        return LoadAssetAsync(location, typeof(T));
    }

    /// <summary>按指定类型异步加载资源（非泛型版本，供按类型预加载使用）。</summary>
    public AssetHandle LoadAssetAsync(string location, Type assetType)
    {
        EnsureReady();
        if (assetType == null)
        {
            assetType = typeof(UnityEngine.Object);
        }

        if (!TryResolve(location, out string loadLocation, out string cacheKey))
        {
            // 地址无效：仍交给 YooAsset 返回失败句柄（会输出错误日志），不缓存
            return _package.LoadAssetAsync(location ?? string.Empty, assetType);
        }

        if (_handleCache.TryGetValue(cacheKey, out Dictionary<Type, AssetHandle> byType) &&
            byType.TryGetValue(assetType, out AssetHandle cached))
        {
            if (cached != null && cached.IsValid)
            {
                return cached;
            }

            byType.Remove(assetType);
        }

        AssetHandle handle = _package.LoadAssetAsync(loadLocation, assetType);
        if (byType == null)
        {
            byType = new Dictionary<Type, AssetHandle>(2);
            _handleCache[cacheKey] = byType;
        }

        byType[assetType] = handle;
        return handle;
    }

    /// <summary>加载 Sprite（图片导入类型需为 Sprite）。失败回调 null。</summary>
    public IEnumerator LoadSpriteAsync(string location, Action<Sprite> onDone)
    {
        yield return LoadAsync(location, onDone);
    }

    /// <summary>加载预制体（只加载不实例化，实例化由调用方 Instantiate）。失败回调 null。</summary>
    public IEnumerator LoadPrefabAsync(string location, Action<GameObject> onDone)
    {
        yield return LoadAsync(location, onDone);
    }

    /// <summary>加载配置（ScriptableObject / TextAsset 等）。失败回调 null。</summary>
    public IEnumerator LoadConfigAsync<T>(string location, Action<T> onDone) where T : UnityEngine.Object
    {
        yield return LoadAsync(location, onDone);
    }

    /// <summary>通用异步加载，完成后回调资源对象；失败回调 null 并释放失败句柄（可重试）。</summary>
    public IEnumerator LoadAsync<T>(string location, Action<T> onDone) where T : UnityEngine.Object
    {
        if (!IsReady)
        {
            Debug.LogWarning("[YooAssetManager] Not ready (" + StatusText + "), load skipped: " + location);
            onDone?.Invoke(null);
            yield break;
        }

        AssetHandle handle = LoadAssetAsync<T>(location);
        yield return handle;
        if (handle.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError("[YooAssetManager] Load " + typeof(T).Name + " failed: " + location + " " + handle.Error);
            DropFailedHandle(location, typeof(T), handle);
            onDone?.Invoke(null);
            yield break;
        }

        onDone?.Invoke(handle.AssetObject as T);
    }

    #endregion

    #region 预加载

    /// <summary>
    /// 按 location 列表顺序预加载，onProgress 回调 0~1。
    /// assetType 为空时按 UnityEngine.Object 加载（图片会得到 Texture2D 主资源；需要 Sprite 请传 typeof(Sprite)）。
    /// </summary>
    public IEnumerator PreloadByLocations(IList<string> locations, Action<float> onProgress, Type assetType = null)
    {
        if (locations == null || locations.Count == 0)
        {
            onProgress?.Invoke(1f);
            yield break;
        }

        EnsureReady();
        Type loadType = assetType ?? typeof(UnityEngine.Object);
        int total = locations.Count;
        for (int i = 0; i < total; i++)
        {
            string loc = locations[i];
            if (!string.IsNullOrEmpty(loc))
            {
                AssetHandle handle = LoadAssetAsync(loc, loadType);
                yield return handle;
                if (handle.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogWarning("[YooAssetManager] Preload miss: " + loc + " " + handle.Error);
                    DropFailedHandle(loc, loadType, handle);
                }
            }

            onProgress?.Invoke((i + 1f) / total);
        }
    }

    /// <summary>预加载包内所有该类型资源（类型按扩展名推断，见 CollectLocationsByType）。</summary>
    public IEnumerator PreloadAllOfType(Type assetType, Action<float> onProgress)
    {
        EnsureReady();
        List<string> locs = CollectLocationsByType(assetType);
        yield return PreloadByLocations(locs, onProgress, assetType);
    }

    /// <summary>预加载某目录（含子目录）下的全部资源，如 "Assets/Prefab/UI"。</summary>
    public IEnumerator PreloadFolder(string folderPath, Action<float> onProgress, Type assetType = null)
    {
        EnsureReady();
        List<string> locs = CollectLocationsInFolder(folderPath);
        yield return PreloadByLocations(locs, onProgress, assetType);
    }

    /// <summary>
    /// 按类型收集资源路径。YooAsset 3.0.6 的 GetAllAssetInfos 不携带 AssetType，
    /// 因此按扩展名推断（Sprite/Texture2D: 图片；GameObject: .prefab；ScriptableObject: .asset；TextAsset: .txt/.json/.bytes 等）。
    /// 注意：请求某个 ScriptableObject 子类时会返回全部 .asset。
    /// </summary>
    public List<string> CollectLocationsByType(Type assetType)
    {
        var result = new List<string>(32);
        if (_package == null || assetType == null)
        {
            return result;
        }

        AssetInfo[] allInfos = TryGetAllAssetInfos();
        if (allInfos == null)
        {
            return result;
        }

        for (int i = 0; i < allInfos.Length; i++)
        {
            AssetInfo info = allInfos[i];
            if (info == null || string.IsNullOrEmpty(info.AssetPath))
            {
                continue;
            }

            if (MatchesType(info.AssetPath, assetType))
            {
                result.Add(info.AssetPath);
            }
        }

        return result;
    }

    /// <summary>收集某目录（含子目录）下的全部资源路径。</summary>
    public List<string> CollectLocationsInFolder(string folderPath)
    {
        var result = new List<string>(32);
        if (_package == null || string.IsNullOrEmpty(folderPath))
        {
            return result;
        }

        string prefix = folderPath.Replace('\\', '/').TrimEnd('/') + "/";
        AssetInfo[] allInfos = TryGetAllAssetInfos();
        if (allInfos == null)
        {
            return result;
        }

        for (int i = 0; i < allInfos.Length; i++)
        {
            AssetInfo info = allInfos[i];
            if (info != null && !string.IsNullOrEmpty(info.AssetPath) &&
                info.AssetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(info.AssetPath);
            }
        }

        return result;
    }

    private static bool MatchesType(string assetPath, Type assetType)
    {
        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        if (assetType == typeof(UnityEngine.Object))
        {
            return true;
        }

        if (assetType == typeof(Sprite) || assetType == typeof(Texture2D) || assetType == typeof(Texture))
        {
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd" || ext == ".bmp" || ext == ".tif" || ext == ".tiff" || ext == ".exr" || ext == ".hdr";
        }

        if (assetType == typeof(GameObject))
        {
            return ext == ".prefab" || ext == ".fbx" || ext == ".obj";
        }

        if (assetType == typeof(TextAsset))
        {
            return ext == ".txt" || ext == ".json" || ext == ".bytes" || ext == ".csv" || ext == ".xml" || ext == ".yaml";
        }

        if (assetType == typeof(AudioClip))
        {
            return ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aif" || ext == ".aiff";
        }

        if (assetType == typeof(Material))
        {
            return ext == ".mat";
        }

        if (assetType == typeof(Font))
        {
            return ext == ".ttf" || ext == ".otf";
        }

        if (assetType == typeof(AnimationClip))
        {
            return ext == ".anim";
        }

        if (typeof(RuntimeAnimatorController).IsAssignableFrom(assetType))
        {
            return ext == ".controller" || ext == ".overridecontroller";
        }

        if (typeof(ScriptableObject).IsAssignableFrom(assetType))
        {
            return ext == ".asset";
        }

        return false;
    }

    #endregion

    #region 释放

    /// <summary>释放某地址的全部已缓存句柄（所有加载类型）。引用计数归零后 YooAsset 会尝试卸载资源包。</summary>
    public void Release(string location)
    {
        if (string.IsNullOrEmpty(location) || _package == null)
        {
            return;
        }

        string cacheKey = location;
        if (IsReady && TryResolve(location, out _, out string resolvedKey))
        {
            cacheKey = resolvedKey;
        }

        if (_handleCache.TryGetValue(cacheKey, out Dictionary<Type, AssetHandle> byType))
        {
            foreach (KeyValuePair<Type, AssetHandle> kv in byType)
            {
                if (kv.Value != null && kv.Value.IsValid)
                {
                    kv.Value.Release();
                }
            }

            _handleCache.Remove(cacheKey);
        }
    }

    /// <summary>释放管理器持有的全部句柄。</summary>
    public void ReleaseAll()
    {
        foreach (KeyValuePair<string, Dictionary<Type, AssetHandle>> entry in _handleCache)
        {
            foreach (KeyValuePair<Type, AssetHandle> kv in entry.Value)
            {
                if (kv.Value != null && kv.Value.IsValid)
                {
                    kv.Value.Release();
                }
            }
        }

        _handleCache.Clear();
    }

    /// <summary>卸载引用计数为 0 的资源（切场景后可调用）。</summary>
    public IEnumerator UnloadUnusedAssetsAsync()
    {
        if (!IsReady || _package == null)
        {
            yield break;
        }

        UnloadUnusedAssetsOperation op = _package.UnloadUnusedAssetsAsync();
        yield return op;
    }

    private void DropFailedHandle(string location, Type assetType, AssetHandle handle)
    {
        if (TryResolve(location, out _, out string cacheKey) &&
            _handleCache.TryGetValue(cacheKey, out Dictionary<Type, AssetHandle> byType) &&
            byType.TryGetValue(assetType, out AssetHandle cached) && cached == handle)
        {
            byType.Remove(assetType);
            if (byType.Count == 0)
            {
                _handleCache.Remove(cacheKey);
            }
        }

        if (handle != null && handle.IsValid)
        {
            handle.Release();
        }
    }

    #endregion

    #region 地址解析

    /// <summary>当前包内是否存在该地址（资源路径或文件名）。</summary>
    public bool HasLocation(string location)
    {
        return IsReady && TryResolve(location, out _, out _);
    }

    /// <summary>
    /// 解析 location：优先按 YooAsset 原生地址（资源路径，可无扩展名）；否则按文件名映射到资源路径。
    /// cacheKey 为规范化的资源路径，保证文件名与路径两种写法共用同一缓存句柄。
    /// </summary>
    private bool TryResolve(string location, out string loadLocation, out string cacheKey)
    {
        loadLocation = location;
        cacheKey = location;
        if (string.IsNullOrEmpty(location) || _package == null)
        {
            return false;
        }

        if (_package.IsLocationValid(location))
        {
            AssetInfo info = _package.GetAssetInfo(location);
            if (info != null && info.IsValid && !string.IsNullOrEmpty(info.AssetPath))
            {
                cacheKey = info.AssetPath;
            }

            return true;
        }

        EnsureFileNameMap();
        if (_fileNameToPath.TryGetValue(location, out string path))
        {
            loadLocation = path;
            cacheKey = path;
            return true;
        }

        return false;
    }

    private void EnsureFileNameMap()
    {
        if (_fileNameToPath != null)
        {
            return;
        }

        _fileNameToPath = new Dictionary<string, string>(128, StringComparer.OrdinalIgnoreCase);
        AssetInfo[] all = TryGetAllAssetInfos();
        if (all == null)
        {
            return;
        }

        for (int i = 0; i < all.Length; i++)
        {
            AssetInfo info = all[i];
            if (info == null || string.IsNullOrEmpty(info.AssetPath))
            {
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(info.AssetPath);
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            if (_fileNameToPath.TryGetValue(fileName, out string existing))
            {
                // 文件名重复时保留第一个，请改用完整路径加载
                Debug.LogWarning("[YooAssetManager] Duplicate file name '" + fileName + "': " + existing + " / " + info.AssetPath + ". Load by full path instead.");
                continue;
            }

            _fileNameToPath[fileName] = info.AssetPath;
        }
    }

    private AssetInfo[] TryGetAllAssetInfos()
    {
        try
        {
            return _package.GetAllAssetInfos();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[YooAssetManager] GetAllAssetInfos failed: " + e.Message);
            return null;
        }
    }

    private void EnsureReady()
    {
        if (!IsReady || _package == null)
        {
            throw new InvalidOperationException("YooAssetManager is not ready. Call InitializeAsync first.");
        }
    }

    #endregion
}