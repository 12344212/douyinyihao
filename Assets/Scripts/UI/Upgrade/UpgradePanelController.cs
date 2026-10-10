using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UpgradePanel 逻辑：按存档等级刷新三维属性 / 进度条 / 升级花费，点击 Upgrade 扣金币升级。
/// 装备槽仅装饰，不在此处理。
/// </summary>
[DisallowMultipleComponent]
public class UpgradePanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private UIPanel panel;
    [SerializeField] private Button upgradeButton;

    [Header("Attack")]
    [SerializeField] private Image attackFill;
    [SerializeField] private TMP_Text attackValue;

    [Header("Range")]
    [SerializeField] private Image rangeFill;
    [SerializeField] private TMP_Text rangeValue;

    [Header("Defense")]
    [SerializeField] private Image defenseFill;
    [SerializeField] private TMP_Text defenseValue;

    [Header("Cost")]
    [SerializeField] private TMP_Text costText;

    private void Awake()
    {
        if (panel == null)
        {
            panel = GetComponent<UIPanel>();
        }
    }

    private void OnEnable()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        LocalSaveManager.OnCurrencyChanged += OnCurrencyChanged;
        if (panel != null)
        {
            panel.Shown += OnPanelShown;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        LocalSaveManager.OnCurrencyChanged -= OnCurrencyChanged;
        if (panel != null)
        {
            panel.Shown -= OnPanelShown;
        }
    }

    private void OnPanelShown(UIPanel _)
    {
        Refresh();
    }

    private void OnCurrencyChanged()
    {
        if (isActiveAndEnabled)
        {
            Refresh();
        }
    }

    /// <summary>按当前存档等级刷新属性文字、进度条、花费与按钮可点状态。</summary>
    public void Refresh()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int level = save != null ? save.GetCharacterLevel() : CharacterUpgradeDefs.MinLevel;
        int stat = CharacterUpgradeDefs.StatAtLevel(level);
        int maxStat = CharacterUpgradeDefs.MaxStat;
        float fill = CharacterUpgradeDefs.FillAmount(level);
        string valueText = stat + "/" + maxStat;

        SetStat(attackFill, attackValue, fill, valueText);
        SetStat(rangeFill, rangeValue, fill, valueText);
        SetStat(defenseFill, defenseValue, fill, valueText);

        bool atMax = level >= CharacterUpgradeDefs.MaxLevel;
        if (costText != null)
        {
            costText.text = atMax ? "—" : CharacterUpgradeDefs.CostToNext(level).ToString();
        }

        if (upgradeButton != null)
        {
            upgradeButton.interactable = !atMax;
        }
    }

    private static void SetStat(Image fill, TMP_Text value, float amount, string text)
    {
        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = amount;
        }

        if (value != null)
        {
            value.text = text;
        }
    }

    private void OnUpgradeClicked()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            TipPanelUI.ShowTip("Not enough gold.");
            Debug.LogWarning("[UpgradePanelController] LocalSaveManager missing. Start from LoadScene.");
            return;
        }

        int level = save.GetCharacterLevel();
        if (level >= CharacterUpgradeDefs.MaxLevel)
        {
            TipPanelUI.ShowTip("Max level.");
            Refresh();
            return;
        }

        int cost = CharacterUpgradeDefs.CostToNext(level);
        if (!save.TrySpendGold(cost))
        {
            TipPanelUI.ShowTip("Not enough gold.");
            return;
        }

        save.SetCharacterLevel(level + 1);
        TipPanelUI.ShowTip("Upgraded!");
        Refresh();
    }
}