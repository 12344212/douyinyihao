using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TopUpPanel: instantiate TopPrefab from GemPackDefs; IAP buy; daily mr claim 200 gold once per local day.
/// </summary>
public class TopUpPanelController : MonoBehaviour
{
    [Header("Pack list")]
    [SerializeField] private Transform listRoot;
    [SerializeField] private TopUpItemSlot topPrefab;
    [SerializeField] private PurchaseLoadingUI loading;
    [SerializeField] private PurchaseTipUI purchaseTip;
    [SerializeField] private GemPackConfig gemPackConfig;

    [Header("Daily mr (free gold)")]
    [SerializeField] private CanvasGroup mrRoot;
    [SerializeField] private Button mrClaimButton;

    private Coroutine _rebuild;
    private Coroutine _flow;
    private bool _busy;

    private void Awake()
    {
        if (mrClaimButton != null)
        {
            mrClaimButton.onClick.AddListener(OnMrClaimClicked);
        }
    }

    private void OnDestroy()
    {
        if (mrClaimButton != null)
        {
            mrClaimButton.onClick.RemoveListener(OnMrClaimClicked);
        }
    }

    private void OnEnable()
    {
        if (_rebuild != null)
        {
            StopCoroutine(_rebuild);
        }

        _rebuild = StartCoroutine(RebuildRoutine());
        RefreshMrVisual();
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

        if (GemPackDefs.Current == null && gemPackConfig != null)
        {
            GemPackDefs.Bind(gemPackConfig);
        }

        ClearList();
        GemPackDefs.Pack[] packs = GemPackDefs.Packs;
        for (int i = 0; i < packs.Length; i++)
        {
            GemPackDefs.Pack pack = packs[i];
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

    private void OnMrClaimClicked()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            TipPanelUI.ShowTip("Not ready.");
            return;
        }

        if (save.IsTopUpDailyGoldClaimedToday())
        {
            TipPanelUI.ShowTip("Already claimed today.");
            RefreshMrVisual();
            return;
        }

        int gold;
        if (!save.TryClaimTopUpDailyGold(out gold))
        {
            TipPanelUI.ShowTip("Already claimed today.");
            RefreshMrVisual();
            return;
        }

        GetPanelController.Show(new GetPanelController.Reward
        {
            useCurrency = true,
            currency = CurrencyType.Gold,
            displayName = "Gold",
            count = gold
        });
        TipPanelUI.ShowTip("Claimed!");
        RefreshMrVisual();
    }

    private void RefreshMrVisual()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        bool claimed = save != null && save.IsTopUpDailyGoldClaimedToday();

        if (mrRoot != null)
        {
            mrRoot.alpha = claimed ? 0.5f : 1f;
            mrRoot.interactable = !claimed;
            mrRoot.blocksRaycasts = !claimed;
        }

        if (mrClaimButton != null)
        {
            mrClaimButton.interactable = !claimed;
        }

        // Grey tint on mr root graphic when claimed.
        if (mrRoot != null)
        {
            Graphic g = mrRoot.GetComponent<Graphic>();
            if (g != null)
            {
                g.color = claimed ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
            }
        }
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