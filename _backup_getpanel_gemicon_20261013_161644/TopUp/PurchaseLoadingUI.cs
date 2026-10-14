using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 购买过程遮罩（挂在 CanvasMax 下）。显示时让序列化的转圈节点绕 Z 轴旋转。
/// 整段购买等待与结果后的 2 秒停留在本组件上跑，避免 TopUpPanel 关闭时协程被掐断。
/// 用 CanvasGroup 显隐，不 SetActive(false)，以免打断自身协程。
/// 成功时在弹 GetPanel 前 AddGems 一次（ProcessPurchase 不发钻）；失败路径不加钻。
/// </summary>
public class PurchaseLoadingUI : MonoBehaviour
{
    private const float PostResultHoldSeconds = 2f;
    private const float PurchaseTimeoutSeconds = 180f;

    [SerializeField] private RectTransform spinTarget;
    [SerializeField] private float spinSpeed = 180f;
    [SerializeField] private CanvasGroup canvasGroup;

    private bool _spinning;
    private Coroutine _flow;
    private Action _onSettled;

    public bool IsBusy => _flow != null;

    private void Awake()
    {
        EnsureCanvasGroup();
        ApplyVisible(false);
    }

    private void Update()
    {
        if (!_spinning || spinTarget == null)
        {
            return;
        }

        spinTarget.Rotate(0f, 0f, -spinSpeed * Time.unscaledDeltaTime, Space.Self);
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        EnsureCanvasGroup();
        ApplyVisible(true);
        _spinning = true;
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        _spinning = false;
        EnsureCanvasGroup();
        ApplyVisible(false);
        if (spinTarget != null)
        {
            spinTarget.localEulerAngles = Vector3.zero;
        }
    }

    /// <summary>
    /// 显示 Loading → Unity Purchasing → 结果后再停留 2 秒 → 关 Loading → Tip +（成功时）GetPanel。
    /// </summary>
    public void BeginBuy(string productId, int gems, Action onSettled)
    {
        if (_flow != null)
        {
            if (onSettled != null)
            {
                onSettled();
            }

            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        _onSettled = onSettled;
        _flow = StartCoroutine(BuyFlow(productId, gems));
    }

    private IEnumerator BuyFlow(string productId, int gems)
    {
        Show();

        bool done = false;
        bool ok = false;
        IapPurchaseManager iap = IapPurchaseManager.Instance;
        if (iap == null)
        {
            yield return new WaitForSecondsRealtime(PostResultHoldSeconds);
            Hide();
            TipPanelUI.ShowTip("Store is not ready.");
            FinishSettled();
            yield break;
        }

        iap.Buy(productId, success =>
        {
            ok = success;
            done = true;
        });

        float timeout = PurchaseTimeoutSeconds;
        while (!done && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // 无论成功/失败/超时：结果出来后再挂 2 秒 Loading
        yield return new WaitForSecondsRealtime(PostResultHoldSeconds);
        Hide();

        if (ok)
        {
            // 仅成功且即将弹奖励时发钻一次；失败/取消不加。
            if (gems > 0 && LocalSaveManager.Instance != null)
            {
                LocalSaveManager.Instance.AddGems(gems);
            }
            else if (gems > 0)
            {
                Debug.LogWarning("[PurchaseLoadingUI] Purchase ok but LocalSaveManager missing; gems not added.");
            }

            TipPanelUI.ShowTip("Purchase successful.");
            GetPanelController.Show(new GetPanelController.Reward
            {
                useCurrency = true,
                currency = CurrencyType.Gems,
                displayName = "Gems",
                count = gems,
                iconLocation = GameItemDefs.CurrencyIconLocation(CurrencyType.Gems)
            });
        }
        else
        {
            string message = iap.LastMessage;
            if (string.IsNullOrEmpty(message))
            {
                message = done ? "Purchase failed." : "Purchase timed out.";
            }

            TipPanelUI.ShowTip(message);
        }

        FinishSettled();
    }

    private void FinishSettled()
    {
        Action cb = _onSettled;
        _onSettled = null;
        _flow = null;
        if (cb != null)
        {
            cb();
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ApplyVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}