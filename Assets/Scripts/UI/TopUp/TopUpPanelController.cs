using System.Collections;
using UnityEngine;

/// <summary>
/// 已有 TopUpPanel：用 TopPrefab 铺 GemPackDefs 的 6 档。
/// 购买走 IapPurchaseManager（编辑器 Fake Store）。成功只弹奖励（钻石已在 ProcessPurchase 里加上）；失败/取消走 PurchaseTipUI。
/// </summary>
public class TopUpPanelController : MonoBehaviour
{
    [SerializeField] private Transform listRoot;
    [SerializeField] private TopUpItemSlot topPrefab;
    [SerializeField] private PurchaseLoadingUI loading;
    [SerializeField] private PurchaseTipUI purchaseTip;

    private Coroutine _rebuild;
    private Coroutine _flow;
    private bool _busy;

    private void OnEnable()
    {
        if (_rebuild != null)
        {
            StopCoroutine(_rebuild);
        }

        _rebuild = StartCoroutine(RebuildRoutine());
    }

    private void OnDisable()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }

        _busy = false;
        if (loading != null)
        {
            loading.Hide();
        }
    }

    private IEnumerator RebuildRoutine()
    {
        if (listRoot == null || topPrefab == null)
        {
            Debug.LogWarning("[TopUpPanelController] listRoot or topPrefab is not assigned.");
            yield break;
        }

        ClearList();
        for (int i = 0; i < GemPackDefs.Packs.Length; i++)
        {
            GemPackDefs.Pack pack = GemPackDefs.Packs[i];
            TopUpItemSlot slot = Instantiate(topPrefab, listRoot);
            slot.gameObject.name = "TopPrefab_" + pack.index;
            slot.gameObject.SetActive(true);
            slot.Bind(pack, TryBuy);
        }

        _rebuild = null;
        yield break;
    }

    private void ClearList()
    {
        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = listRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    public void TryBuy(GemPackDefs.Pack pack)
    {
        if (_busy || pack.gems <= 0)
        {
            return;
        }

        if (_flow != null)
        {
            StopCoroutine(_flow);
        }

        _flow = StartCoroutine(BuyRoutine(pack));
    }

    private IEnumerator BuyRoutine(GemPackDefs.Pack pack)
    {
        _busy = true;
        if (purchaseTip != null)
        {
            purchaseTip.Hide();
        }

        if (loading != null)
        {
            loading.Show();
        }

        bool done = false;
        bool ok = false;
        IapPurchaseManager iap = IapPurchaseManager.Instance;
        if (iap == null)
        {
            if (loading != null)
            {
                loading.Hide();
            }

            ShowFail("Store is not ready.");
            _busy = false;
            _flow = null;
            yield break;
        }

        iap.Buy(pack.productId, success =>
        {
            ok = success;
            done = true;
        });

        float timeout = 180f;
        while (!done && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (loading != null)
        {
            loading.Hide();
        }

        if (ok)
        {
            GetPanelController.Show(new GetPanelController.Reward
            {
                useCurrency = true,
                currency = CurrencyType.Gems,
                displayName = "Gems",
                count = pack.gems,
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

            ShowFail(message);
        }

        _busy = false;
        _flow = null;
    }

    private void ShowFail(string message)
    {
        if (purchaseTip != null)
        {
            purchaseTip.Show(message);
            return;
        }

        TipPanelUI.ShowTip(message);
    }
}
