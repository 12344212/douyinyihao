using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainScene 主界面控制器（挂在 MainScene 的 Canvas 上，与参考项目一致）。
/// - 主界面按钮打开对应功能面板（CanvasPanel 下的 UIPanel），同一时间只开一个；面板 Close / Esc / Android 返回键关闭。
/// - 顶栏（CanvasMax 下的 TopBarPrefab 实例）：头像 → Profile 弹窗，金币 "+" → Shop，钻石 "+" → Top Up。
/// - Collection（原 playButton）打开 CollectionPanel；进战斗仍由关卡格子调用 StartBattle。
/// 货币显示与 OnCurrencyChanged 订阅由 TopBarUI 负责，这里只提供 RefreshCurrency 转发。
/// 所有引用通过 Inspector 序列化字段赋值（MainSceneUISetup 生成并绑定）。
/// </summary>
public class MainUIController : MonoBehaviour
{
    public const string BattleSceneName = "BattleScene";

    [Header("Main Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button stagesButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button bagButton;
    [SerializeField] private Button upgradeButton;

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

    private UIPanel[] _panels;
    private UIPanel _currentPanel;
    private bool _loadingBattle;

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

        Bind(playButton, OpenCollection);
        Bind(stagesButton, OpenStages);
        Bind(shopButton, OpenShop);
        Bind(bagButton, OpenBag);
        Bind(upgradeButton, OpenUpgrade);

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

        Unbind(playButton, OpenCollection);
        Unbind(stagesButton, OpenStages);
        Unbind(shopButton, OpenShop);
        Unbind(bagButton, OpenBag);
        Unbind(upgradeButton, OpenUpgrade);

        if (topBar != null)
        {
            topBar.AvatarClicked -= OpenProfile;
            topBar.GoldPlusClicked -= OpenShop;
            topBar.GemsPlusClicked -= OpenTopUp;
        }
    }

    private void Update()
    {
        // Esc（编辑器 / PC）与 Android 返回键都映射为 KeyCode.Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackPressed();
        }
    }

    // ---------- 面板 ----------

    public void OpenStages() => OpenPanel(stagePanel);
    public void OpenShop() => OpenPanel(shopPanel);
    public void OpenBag() => OpenPanel(bagPanel);
    public void OpenTopUp() => OpenPanel(topUpPanel);
    public void OpenUpgrade() => OpenPanel(upgradePanel);
    public void OpenProfile() => OpenPanel(profilePanel);
    public void OpenCollection() => OpenPanel(collectionPanel);

    /// <summary>打开指定面板；已有其它面板时先立即关闭它（同一时间只开一个）。</summary>
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

    /// <summary>关闭当前面板（带动画），并确保其它面板都已隐藏。</summary>
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

    /// <summary>返回键：有面板则关闭；主界面无面板时暂不处理（以后可在这里加退出确认）。</summary>
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

    // ---------- 货币 ----------

    /// <summary>刷新顶栏货币显示（TopBarUI 自己也会订阅 OnCurrencyChanged）。</summary>
    public void RefreshCurrency()
    {
        if (topBar != null)
        {
            topBar.Refresh(true);
        }
    }

    // ---------- 战斗 ----------

    /// <summary>进入战斗场景（异步加载）。</summary>
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

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}