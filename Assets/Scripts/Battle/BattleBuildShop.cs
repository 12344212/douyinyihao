using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗商店。木头和生铁只用来放置建筑或回血，强化仍走升级卡。
/// </summary>
public class BattleBuildShop : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private BattleDirector director;
    [SerializeField] private Button openButton;
    [SerializeField] private GameObject plankButtonObject;
    [SerializeField] private GameObject attackButtonObject;
    [SerializeField] private GameObject attractorButtonObject;
    [SerializeField] private GameObject captureButtonObject;
    [SerializeField] private GameObject healButtonObject;
    [SerializeField] private TMP_Text plankCost;
    [SerializeField] private TMP_Text attackCost;
    [SerializeField] private TMP_Text attractorCost;
    [SerializeField] private TMP_Text captureCost;
    [SerializeField] private TMP_Text healCost;
    [SerializeField] private TMP_Text healAmountLabel;

    private Button _plankButton;
    private Button _attackButton;
    private Button _attractorButton;
    private Button _captureButton;
    private Button _healButton;
    private GameObject _blocker;
    private bool _timeHeld;

    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }
    }

    private void OnEnable()
    {
        BindRows();
        ShowBlocker();
        if (!_timeHeld)
        {
            BattleTime.Hold();
            _timeHeld = true;
        }
        if (openButton != null)
        {
            openButton.interactable = true;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (_blocker != null)
        {
            _blocker.SetActive(false);
        }

        if (_timeHeld)
        {
            BattleTime.Release();
            _timeHeld = false;
        }
    }

    private void OnDestroy()
    {
        if (_blocker != null)
        {
            Destroy(_blocker);
        }
    }

    private void Update()
    {
        Refresh();
    }

    public void Toggle()
    {
        GameObject target = panel != null ? panel : gameObject;
        target.SetActive(!target.activeSelf);
    }

    public void Close()
    {
        GameObject target = panel != null ? panel : gameObject;
        target.SetActive(false);
    }

    private void ShowBlocker()
    {
        if (_blocker == null)
        {
            _blocker = new GameObject("UpSeletBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _blocker.transform.SetParent(transform.parent, false);
            RectTransform rect = _blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = _blocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            Button button = _blocker.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(Close);
        }

        _blocker.SetActive(true);
        _blocker.transform.SetAsLastSibling();
        transform.SetAsLastSibling();
    }

    private void BindRows()
    {
        _plankButton = Bind(_plankButton, plankButtonObject, BuyPlank);
        _attackButton = Bind(_attackButton, attackButtonObject, BuyAttack);
        _attractorButton = Bind(_attractorButton, attractorButtonObject, BuyAttractor);
        _captureButton = Bind(_captureButton, captureButtonObject, BuyCapture);
        _healButton = Bind(_healButton, healButtonObject, BuyHeal);
    }

    private static Button Bind(Button current, GameObject target, UnityEngine.Events.UnityAction action)
    {
        Button button = EnsureButton(target);
        if (button == null || button == current)
        {
            return button != null ? button : current;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        return button;
    }

    private static Button EnsureButton(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.AddComponent<Button>();
            button.targetGraphic = target.GetComponent<Image>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        return button;
    }

    private void BuyPlank()
    {
        if (director != null && director.TryBuyPlank())
        {
            Close();
        }
    }

    private void BuyAttack()
    {
        if (director != null && director.TryBuyAttack())
        {
            Close();
        }
    }

    private void BuyAttractor()
    {
        if (director != null && director.TryBuyAttractor())
        {
            Close();
        }
    }

    private void BuyCapture()
    {
        if (director != null && director.TryBuyCapture())
        {
            Close();
        }
    }

    private void BuyHeal()
    {
        if (director != null)
        {
            director.TryBuyHeal();
        }
    }

    private void Refresh()
    {
        if (director == null)
        {
            return;
        }

        SetCost(plankCost, director.NextPlankWoodCost);
        SetCost(attackCost, director.NextAttackIronCost);
        SetCost(attractorCost, director.NextAttractorIronCost);
        SetCost(captureCost, director.NextCaptureWoodCost);
        SetCost(healCost, director.NextHealWoodCost);
        if (healAmountLabel != null)
        {
            healAmountLabel.text = "+" + BattleTuning.ShopHealAmount;
        }

        SetButton(_plankButton, director.CanBuyPlank());
        SetButton(_attackButton, director.CanBuyAttack());
        SetButton(_attractorButton, director.CanBuyAttractor());
        SetButton(_captureButton, director.CanBuyCapture());
        SetButton(_healButton, director.CanBuyHeal());
    }

    private static void SetCost(TMP_Text label, int cost)
    {
        if (label != null)
        {
            label.text = cost.ToString();
        }
    }

    private static void SetButton(Button button, bool enabled)
    {
        if (button != null)
        {
            button.interactable = enabled;
        }
    }
}
