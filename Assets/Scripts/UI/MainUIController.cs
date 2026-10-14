using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainScene main UI controller (on Canvas).
/// Opens feature panels one at a time; TopBar avatar/gold+/gems+; Collection opens CollectionPanel.
/// Main menu buttons except Stage use child T as click hit (Button + Image alpha 0, raycastable);
/// on T click play DOPunchScale on parent then open the panel.
/// </summary>
public class MainUIController : MonoBehaviour
{
    public const string BattleSceneName = "BattleScene";
    private const string TouchChildName = "T";

    [Header("Main Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button stagesButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button bagButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button topUpButton;

    [Header("Top Bar (avatar / gold + / gems +)")]
    [SerializeField] private TopBarUI topBar;

    [Header("Panels")]
    [SerializeField] private UIPanel stagePanel;
    [SerializeField] private UIPanel shopPanel;
    [SerializeField] private UIPanel bagPanel;
    [SerializeField] private UIPanel topUpPanel;
    [SerializeField] private UIPanel upgradePanel;
    [SerializeField] private UIPanel profilePanel;
    [SerializeField] private UIPanel collectionPanel;

    [Header("Flow")]
    [SerializeField] private string battleSceneName = BattleSceneName;

    [Header("Click Punch (T hit -> punch parent)")]
    [SerializeField] private float clickPunchStrength = 0.12f;
    [SerializeField] private float clickPunchDuration = 0.22f;
    [SerializeField] private int clickPunchVibrato = 8;
    [SerializeField] private float clickPunchElasticity = 0.55f;

    private UIPanel[] _panels;
    private UIPanel _currentPanel;
    private bool _loadingBattle;

    private Button _playClick;
    private Button _stagesClick;
    private Button _shopClick;
    private Button _bagClick;
    private Button _upgradeClick;
    private Button _topUpClick;

    private UnityAction _playAction;
    private UnityAction _stagesAction;
    private UnityAction _shopAction;
    private UnityAction _bagAction;
    private UnityAction _upgradeAction;
    private UnityAction _topUpAction;

    public UIPanel CurrentPanel => _currentPanel;
    public bool IsPanelOpen => _currentPanel != null;

    private void Awake()
    {
        _panels = new[] { stagePanel, shopPanel, bagPanel, topUpPanel, upgradePanel, profilePanel, collectionPanel };
        for (int i = 0; i < _panels.Length; i++)
        {
            if (_panels[i] != null)
            {
                _panels[i].CloseRequested += OnPanelCloseRequested;
                _panels[i].HideImmediate();
            }
        }

        _playClick = ResolveMenuClick(playButton, useTouchChild: true);
        _stagesClick = ResolveMenuClick(stagesButton, useTouchChild: false);
        _shopClick = ResolveMenuClick(shopButton, useTouchChild: true);
        _bagClick = ResolveMenuClick(bagButton, useTouchChild: true);
        _upgradeClick = ResolveMenuClick(upgradeButton, useTouchChild: true);
        _topUpClick = ResolveMenuClick(topUpButton, useTouchChild: true);

        _playAction = () => OnMenuClicked(playButton, OpenCollection);
        _stagesAction = OpenStages;
        _shopAction = () => OnMenuClicked(shopButton, OpenShop);
        _bagAction = () => OnMenuClicked(bagButton, OpenBag);
        _upgradeAction = () => OnMenuClicked(upgradeButton, OpenUpgrade);
        _topUpAction = () => OnMenuClicked(topUpButton, OpenTopUp);

        Bind(_playClick, _playAction);
        Bind(_stagesClick, _stagesAction);
        Bind(_shopClick, _shopAction);
        Bind(_bagClick, _bagAction);
        Bind(_upgradeClick, _upgradeAction);
        Bind(_topUpClick, _topUpAction);

        if (topBar != null)
        {
            topBar.AvatarClicked += OpenProfile;
            topBar.GoldPlusClicked += OpenShop;
            topBar.GemsPlusClicked += OpenTopUp;
        }
    }

    private void Start()
    {
        if (LocalSaveManager.Instance == null)
        {
            Debug.LogWarning("[MainUIController] LocalSaveManager not found. Start the game from LoadScene for full functionality.");
        }
    }

    private void OnDestroy()
    {
        if (_panels != null)
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] != null)
                {
                    _panels[i].CloseRequested -= OnPanelCloseRequested;
                }
            }
        }

        Unbind(_playClick, _playAction);
        Unbind(_stagesClick, _stagesAction);
        Unbind(_shopClick, _shopAction);
        Unbind(_bagClick, _bagAction);
        Unbind(_upgradeClick, _upgradeAction);
        Unbind(_topUpClick, _topUpAction);

        if (topBar != null)
        {
            topBar.AvatarClicked -= OpenProfile;
            topBar.GoldPlusClicked -= OpenShop;
            topBar.GemsPlusClicked -= OpenTopUp;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackPressed();
        }
    }

    public void OpenStages() => OpenPanel(stagePanel);
    public void OpenShop() => OpenPanel(shopPanel);
    public void OpenBag() => OpenPanel(bagPanel);
    public void OpenTopUp() => OpenPanel(topUpPanel);
    public void OpenUpgrade() => OpenPanel(upgradePanel);
    public void OpenProfile() => OpenPanel(profilePanel);
    public void OpenCollection() => OpenPanel(collectionPanel);

    public void OpenPanel(UIPanel panel)
    {
        if (_loadingBattle)
        {
            return;
        }

        if (panel == null)
        {
            Debug.LogWarning("[MainUIController] Panel is not assigned.");
            return;
        }

        if (_currentPanel == panel && panel.IsOpen)
        {
            return;
        }

        if (_currentPanel != null)
        {
            _currentPanel.HideImmediate();
        }

        _currentPanel = panel;
        panel.Show();
        RefreshCurrency();
    }

    public void CloseAllPanels()
    {
        UIPanel closing = _currentPanel;
        _currentPanel = null;
        if (closing != null)
        {
            closing.Hide();
        }

        if (_panels == null)
        {
            return;
        }

        for (int i = 0; i < _panels.Length; i++)
        {
            if (_panels[i] != null && _panels[i] != closing)
            {
                _panels[i].HideImmediate();
            }
        }
    }

    public void OnBackPressed()
    {
        if (_currentPanel != null)
        {
            CloseAllPanels();
        }
    }

    private void OnPanelCloseRequested(UIPanel panel)
    {
        if (panel == _currentPanel)
        {
            CloseAllPanels();
        }
        else
        {
            panel.Hide();
        }
    }

    public void RefreshCurrency()
    {
        if (topBar != null)
        {
            topBar.Refresh(true);
        }
    }

    public void StartBattle()
    {
        if (_loadingBattle)
        {
            return;
        }

        if (string.IsNullOrEmpty(battleSceneName) || !Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            Debug.LogError($"[MainUIController] Scene '{battleSceneName}' is not in Build Settings.");
            return;
        }

        _loadingBattle = true;
        SetClickInteractable(_playClick, false);
        if (playButton != null)
        {
            playButton.interactable = false;
        }

        if (_currentPanel != null)
        {
            _currentPanel.HideImmediate();
            _currentPanel = null;
        }

        StartCoroutine(LoadBattle());
    }

    private IEnumerator LoadBattle()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(battleSceneName);
        while (op != null && !op.isDone)
        {
            yield return null;
        }
    }

    private void OnMenuClicked(Button host, UnityAction openAction)
    {
        if (host != null)
        {
            PlayClickPunch(host.transform);
        }

        if (openAction != null)
        {
            openAction.Invoke();
        }
    }

    private void PlayClickPunch(Transform target)
    {
        if (target == null)
        {
            return;
        }

        DOTween.Kill(ClickPunchId(target), complete: false);
        target
            .DOPunchScale(
                Vector3.one * Mathf.Max(0.01f, clickPunchStrength),
                Mathf.Max(0.05f, clickPunchDuration),
                Mathf.Max(1, clickPunchVibrato),
                Mathf.Clamp01(clickPunchElasticity))
            .SetId(ClickPunchId(target))
            .SetUpdate(true)
            .SetLink(target.gameObject);
    }

    private static object ClickPunchId(Transform target) => "MainUIClickPunch:" + target.GetInstanceID();

    private static Button ResolveMenuClick(Button host, bool useTouchChild)
    {
        if (host == null)
        {
            return null;
        }

        if (!useTouchChild)
        {
            return host;
        }

        Transform touch = host.transform.Find(TouchChildName);
        if (touch == null)
        {
            Debug.LogWarning($"[MainUIController] Child '{TouchChildName}' missing under '{host.name}', falling back to host Button.");
            return host;
        }

        ConfigureTouchChild(touch);
        DisableHostRaycast(host);
        return touch.GetComponent<Button>();
    }

    /// <summary>T: active, Image alpha 0 + raycast, Button with no color tint.</summary>
    public static void ConfigureTouchChild(Transform touch)
    {
        if (touch == null)
        {
            return;
        }

        if (!touch.gameObject.activeSelf)
        {
            touch.gameObject.SetActive(true);
        }

        Image img = touch.GetComponent<Image>();
        if (img == null)
        {
            img = touch.gameObject.AddComponent<Image>();
        }

        Color c = img.color;
        c.a = 0f;
        img.color = c;
        img.raycastTarget = true;

        Button btn = touch.GetComponent<Button>();
        if (btn == null)
        {
            btn = touch.gameObject.AddComponent<Button>();
        }

        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;
        btn.interactable = true;
    }

    /// <summary>Disable raycast on host/siblings so only T receives clicks.</summary>
    public static void DisableHostRaycast(Button host)
    {
        if (host == null)
        {
            return;
        }

        Transform touch = host.transform.Find(TouchChildName);
        Graphic[] graphics = host.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (g == null)
            {
                continue;
            }

            if (touch != null && (g.transform == touch || g.transform.IsChildOf(touch)))
            {
                continue;
            }

            g.raycastTarget = false;
        }
    }

    private static void SetClickInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null && action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void Unbind(Button button, UnityAction action)
    {
        if (button != null && action != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}