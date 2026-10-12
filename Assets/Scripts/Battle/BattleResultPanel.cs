using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle result UI on CanvasPanel/Reslut.
/// Win: Next Level + Main Menu. Lose: Restart + Main Menu.
/// Instantiates BagPrefab under the active branch T node.
/// Grants gold once via LocalSaveManager (win 100*stageLevel, lose 50*stageLevel).
/// </summary>
[DisallowMultipleComponent]
public class BattleResultPanel : MonoBehaviour
{
    public const int WinGoldPerStage = 100;
    public const int LoseGoldPerStage = 50;

    [Header("Refs")]
    [SerializeField] private BattleDirector director;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Branches")]
    [SerializeField] private GameObject winRoot;
    [SerializeField] private GameObject loseRoot;
    [SerializeField] private RectTransform winBagParent;
    [SerializeField] private RectTransform loseBagParent;

    [Header("Buttons (Win)")]
    [SerializeField] private Button winNextButton;
    [SerializeField] private Button winHomeButton;

    [Header("Buttons (Lose)")]
    [SerializeField] private Button loseRestartButton;
    [SerializeField] private Button loseHomeButton;

    [Header("Bag")]
    [Tooltip("Assets/Prefab/UI/BagPrefab.prefab (BagItemSlot). If empty, loads YooAsset location BagPrefab.")]
    [SerializeField] private BagItemSlot bagPrefab;

    private bool _rewardGranted;
    private GameObject _bagInstance;
    private Coroutine _bagLoadCo;

    public CanvasGroup CanvasGroup => canvasGroup;

    private void Awake()
    {
        // Reslut starts inactive in the scene; do not SetActive(false) here.
        TryAutoWire();
        EnsureCanvasGroup();
        BindButtons(true);
    }

    private void OnDestroy()
    {
        BindButtons(false);
        if (_bagLoadCo != null)
        {
            StopCoroutine(_bagLoadCo);
            _bagLoadCo = null;
        }
    }

    /// <summary>
    /// Open result UI. stageLevel is 1-based global index (1-1=1, 2-1=11).
    /// showPrimary: on win, show Next only when a next stage exists; on lose Restart stays visible.
    /// </summary>
    public void Show(bool victory, int chapter, int stage, bool showPrimary)
    {
        gameObject.SetActive(true);
        TryAutoWire();
        EnsureCanvasGroup();

        if (winRoot != null)
        {
            winRoot.SetActive(victory);
        }

        if (loseRoot != null)
        {
            loseRoot.SetActive(!victory);
        }

        if (winNextButton != null)
        {
            winNextButton.gameObject.SetActive(victory && showPrimary);
        }

        if (loseRestartButton != null)
        {
            loseRestartButton.gameObject.SetActive(!victory);
        }

        int stageLevel = StageLevelNumber(chapter, stage);
        int gold = victory ? WinGoldPerStage * stageLevel : LoseGoldPerStage * stageLevel;
        GrantGoldOnce(gold);

        RectTransform bagParent = victory ? winBagParent : loseBagParent;
        PlaceBag(bagParent, gold);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>1-based global stage number: 1-1 => 1, 2-1 => 11.</summary>
    public static int StageLevelNumber(int chapter, int stage)
    {
        return BattleTuning.StageIndex(chapter, stage) + 1;
    }

    private void GrantGoldOnce(int gold)
    {
        if (_rewardGranted || gold <= 0)
        {
            return;
        }

        _rewardGranted = true;
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[BattleResultPanel] LocalSaveManager missing. Gold was not granted: " + gold);
            return;
        }

        save.AddGold(gold);
        Debug.Log("[BattleResultPanel] Granted gold " + gold);
    }

    private void PlaceBag(RectTransform parent, int goldAmount)
    {
        ClearBagInstance();
        if (parent == null)
        {
            Debug.LogWarning("[BattleResultPanel] Bag parent T is not assigned.");
            return;
        }

        if (bagPrefab != null)
        {
            SpawnBag(bagPrefab, parent, goldAmount);
            return;
        }

        if (_bagLoadCo != null)
        {
            StopCoroutine(_bagLoadCo);
        }

        _bagLoadCo = StartCoroutine(LoadBagViaYoo(parent, goldAmount));
    }

    private IEnumerator LoadBagViaYoo(RectTransform parent, int goldAmount)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (yoo == null || !yoo.IsReady)
        {
            Debug.LogWarning("[BattleResultPanel] BagPrefab not assigned and YooAsset is not ready.");
            _bagLoadCo = null;
            yield break;
        }

        GameObject prefabGo = null;
        yield return yoo.LoadPrefabAsync("BagPrefab", go => prefabGo = go);
        BagItemSlot prefab = prefabGo != null ? prefabGo.GetComponent<BagItemSlot>() : null;
        if (prefab == null)
        {
            Debug.LogWarning("[BattleResultPanel] Failed to load BagPrefab via YooAsset.");
            _bagLoadCo = null;
            yield break;
        }

        SpawnBag(prefab, parent, goldAmount);
        _bagLoadCo = null;
    }

    private void SpawnBag(BagItemSlot prefab, RectTransform parent, int goldAmount)
    {
        BagItemSlot slot = Instantiate(prefab, parent);
        _bagInstance = slot.gameObject;
        RectTransform rt = slot.transform as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        string icon = GameItemDefs.CurrencyIconLocation(CurrencyType.Gold);
        slot.Bind("gold", "Gold", goldAmount, icon, null);
        slot.SetSelected(false);
    }

    private void ClearBagInstance()
    {
        if (_bagInstance != null)
        {
            Destroy(_bagInstance);
            _bagInstance = null;
        }
    }

    private void BindButtons(bool bind)
    {
        Wire(winNextButton, OnNext, bind);
        Wire(winHomeButton, OnHome, bind);
        Wire(loseRestartButton, OnRestart, bind);
        Wire(loseHomeButton, OnHome, bind);
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction action, bool bind)
    {
        if (button == null)
        {
            return;
        }

        if (bind)
        {
            button.onClick.AddListener(action);
        }
        else
        {
            button.onClick.RemoveListener(action);
        }
    }

    private void OnNext()
    {
        if (director != null)
        {
            director.OnPrimary();
        }
    }

    private void OnRestart()
    {
        if (director != null)
        {
            director.OnPrimary();
        }
    }

    private void OnHome()
    {
        if (director != null)
        {
            director.OnBack();
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

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>Fill missing refs from Win/Lose/T/Resu/Refresh/Home child names.</summary>
    private void TryAutoWire()
    {
        if (director == null)
        {
            director = FindObjectOfType<BattleDirector>();
        }

        if (winRoot == null)
        {
            Transform t = transform.Find("Win");
            if (t != null)
            {
                winRoot = t.gameObject;
            }
        }

        if (loseRoot == null)
        {
            Transform t = transform.Find("Lose");
            if (t != null)
            {
                loseRoot = t.gameObject;
            }
        }

        if (winBagParent == null && winRoot != null)
        {
            winBagParent = FindNamedRect(winRoot.transform, "T");
        }

        if (loseBagParent == null && loseRoot != null)
        {
            loseBagParent = FindNamedRect(loseRoot.transform, "T");
        }

        if (winNextButton == null && winRoot != null)
        {
            winNextButton = FindNamedButton(winRoot.transform, "Resu");
        }

        if (winHomeButton == null && winRoot != null)
        {
            winHomeButton = FindNamedButton(winRoot.transform, "Home");
        }

        if (loseRestartButton == null && loseRoot != null)
        {
            loseRestartButton = FindNamedButton(loseRoot.transform, "Refresh");
        }

        if (loseHomeButton == null && loseRoot != null)
        {
            loseHomeButton = FindNamedButton(loseRoot.transform, "Home");
        }
    }

    private static RectTransform FindNamedRect(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t as RectTransform : null;
    }

    private static Button FindNamedButton(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}