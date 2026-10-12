using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TopUpPanel: 用 GemPackDefs 实例化 TopPrefab；购买走 Unity IAP（Loading 在 CanvasMax）；
/// 每日 mr 领 200 金币（本地日历日一次）。
/// </summary>
public class TopUpPanelController : MonoBehaviour
{
    [Header("Pack list")]
    [SerializeField] private Transform listRoot;
    [SerializeField] private TopUpItemSlot topPrefab;
    [SerializeField] private PurchaseLoadingUI loading;
    [SerializeField] private GemPackConfig gemPackConfig;

    [Header("Daily mr (free gold)")]
    [SerializeField] private CanvasGroup mrRoot;
    [SerializeField] private Button mrClaimButton;

    private Coroutine _rebuild;
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
        // Loading 挂在 CanvasMax，购买流程不在此协程里；关闭面板时不要 Hide Loading。
        _rebuild = null;
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

        if (loading == null)
        {
            TipPanelUI.ShowTip("Store is not ready.");
            return;
        }

        if (loading.IsBusy)
        {
            return;
        }

        _busy = true;
        loading.BeginBuy(pack.productId, pack.gems, () => { _busy = false; });
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

        if (mrRoot != null)
        {
            Graphic g = mrRoot.GetComponent<Graphic>();
            if (g != null)
            {
                g.color = claimed ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
            }
        }
    }
}