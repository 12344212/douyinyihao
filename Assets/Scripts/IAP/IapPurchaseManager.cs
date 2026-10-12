using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// Unity IAP。iOS 真机走真实 App Store；编辑器与非 iOS 强制 Fake Store（不连真商店）。
/// 挂在 LoadScene 的 IapRuntime 上，DontDestroyOnLoad。由 LoadSceneController 序列化引用并调用 InitializeAsync。
/// 成功时在这里 AddGems，界面只负责弹奖励，避免加两次。
/// </summary>
public class IapPurchaseManager : MonoBehaviour, IStoreListener
{
    public static IapPurchaseManager Instance { get; private set; }

    private IStoreController _store;
    private Action<bool> _onDone;
    private bool _initializing;
    private bool _initFailed;

    public bool IsReady => _store != null;

    /// <summary>最近一次购买失败/取消的英文提示。成功时清空。</summary>
    public string LastMessage { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>初始化商店。已就绪则立刻结束；失败或超时只打日志，不抛异常。</summary>
    public IEnumerator InitializeAsync()
    {
        if (_store != null)
        {
            yield break;
        }

        if (!_initializing)
        {
            BeginInitialize();
        }

        float timeout = 12f;
        while (_initializing && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_store == null)
        {
            Debug.LogWarning("[IapPurchaseManager] Store is not ready. Entering the game anyway.");
        }
    }

    private void BeginInitialize()
    {
        _initializing = true;
        _initFailed = false;
        try
        {
            StandardPurchasingModule module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR || !UNITY_IOS
            // 编辑器与非 iOS：强制 Fake Store，不连真商店。Default 模式购买直接成功。
            module.useFakeStoreAlways = true;
            module.useFakeStoreUIMode = FakeStoreUIMode.Default;
#endif
            ConfigurationBuilder builder = ConfigurationBuilder.Instance(module);
            for (int i = 0; i < GemPackDefs.Packs.Length; i++)
            {
                builder.AddProduct(GemPackDefs.Packs[i].productId, ProductType.Consumable);
            }

            UnityPurchasing.Initialize(this, builder);
        }
        catch (Exception e)
        {
            _initializing = false;
            _initFailed = true;
            LastMessage = "Store is not ready.";
            Debug.LogWarning("[IapPurchaseManager] Init threw: " + e.Message);
        }
    }

    /// <summary>拉起购买。结果通过 onDone 回调；失败原因在 LastMessage。</summary>
    public void Buy(string productId, Action<bool> onDone)
    {
        if (_onDone != null)
        {
            LastMessage = "Purchase failed.";
            if (onDone != null)
            {
                onDone(false);
            }

            return;
        }

        _onDone = onDone;
        LastMessage = null;
        if (_store == null)
        {
            if (!_initializing && _initFailed)
            {
                BeginInitialize();
            }

            LastMessage = "Store is not ready.";
            Finish(false);
            return;
        }

        Product product = _store.products.WithID(productId);
        if (product == null || !product.availableToPurchase)
        {
            LastMessage = "This pack is unavailable.";
            Debug.LogWarning("[IapPurchaseManager] Product unavailable: " + productId);
            Finish(false);
            return;
        }

        try
        {
            _store.InitiatePurchase(product);
        }
        catch (Exception e)
        {
            LastMessage = "Purchase failed.";
            Debug.LogWarning("[IapPurchaseManager] InitiatePurchase threw: " + e.Message);
            Finish(false);
        }
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _store = controller;
        _initializing = false;
        _initFailed = false;
        Debug.Log("[IapPurchaseManager] Store initialized.");
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        FailInit(error, null);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        FailInit(error, message);
    }

    private void FailInit(InitializationFailureReason error, string message)
    {
        _initializing = false;
        _initFailed = true;
        _store = null;
        Debug.LogWarning("[IapPurchaseManager] Init failed: " + error + (string.IsNullOrEmpty(message) ? "" : " " + message));
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string id = args != null && args.purchasedProduct != null ? args.purchasedProduct.definition.id : null;
        GemPackDefs.Pack pack = GemPackDefs.GetByProductId(id);
        if (pack.gems > 0 && LocalSaveManager.Instance != null)
        {
            LocalSaveManager.Instance.AddGems(pack.gems);
        }
        else if (pack.gems > 0)
        {
            Debug.LogWarning("[IapPurchaseManager] Purchase completed but LocalSaveManager is missing. Gems were not added.");
        }

        LastMessage = null;
        Finish(true);
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning("[IapPurchaseManager] Purchase failed: " + failureReason);
        LastMessage = failureReason == PurchaseFailureReason.UserCancelled ? "Purchase canceled." : "Purchase failed.";
        Finish(false);
    }

    private void Finish(bool ok)
    {
        Action<bool> cb = _onDone;
        _onDone = null;
        if (cb != null)
        {
            cb(ok);
        }
    }
}