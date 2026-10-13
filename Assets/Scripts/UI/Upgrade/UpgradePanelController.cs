using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UpgradePanel stats: Attack / Range / Net Speed / Hp fills, cost, upgrade button.
/// Equipment slots: UpgradeEquipController.
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
    [SerializeField] private TMP_Text attackName;

    [Header("Range")]
    [SerializeField] private Image rangeFill;
    [SerializeField] private TMP_Text rangeValue;
    [SerializeField] private TMP_Text rangeName;

    [Header("Net Speed")]
    [SerializeField] private Image netSpeedFill;
    [SerializeField] private TMP_Text netSpeedValue;
    [SerializeField] private TMP_Text netSpeedName;

    [Header("Hp")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpValue;
    [SerializeField] private TMP_Text hpName;

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

    /// <summary>按当前存档等级刷新三维属性、进度条、花费与按钮。</summary>
    public void Refresh()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int level = save != null ? save.GetCharacterLevel() : CharacterUpgradeDefs.MinLevel;

        if (attackName != null)
        {
            attackName.text = "Attack";
        }

        if (rangeName != null)
        {
            rangeName.text = "Range";
        }

        if (netSpeedName != null)
        {
            netSpeedName.text = "Net Speed";
        }

        if (hpName != null)
        {
            hpName.text = "Hp";
        }

        SetStat(attackFill, attackValue, CharacterUpgradeDefs.AttackFill(level), CharacterUpgradeDefs.FormatAttack(level));
        SetStat(rangeFill, rangeValue, CharacterUpgradeDefs.RangeFill(level), CharacterUpgradeDefs.FormatRange(level));
        SetStat(netSpeedFill, netSpeedValue, CharacterUpgradeDefs.NetSpeedFill(level), CharacterUpgradeDefs.FormatNetSpeed(level));
        SetStat(hpFill, hpValue, CharacterUpgradeDefs.HpFill(level), CharacterUpgradeDefs.FormatHp(level));

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