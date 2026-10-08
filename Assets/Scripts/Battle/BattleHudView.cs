using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 HUD。文字、按钮、血条都在预制体上拖好。按钮只调这里的方法，再转给 BattleDirector。
/// </summary>
public class BattleHudView : MonoBehaviour
{
    private const string PlankBody = "Place one plank on an adjacent empty cell.";
    private const string CaptureBody = "On an empty plank. Automatically nets resources in range.";
    private const string AttackBody = "On an empty plank. Automatically attacks enemies in range.";
    private const string RangeBody = "Increases how far you can throw the net and the spear.";
    private const string CapacityBody = "The net can hold 4 more resources.";
    private const string CannotPlace = "Cannot be placed.";

    [SerializeField] private BattleDirector director;
    [SerializeField] private TMP_Text woodText;
    [SerializeField] private TMP_Text leafText;
    [SerializeField] private TMP_Text scrapText;
    [SerializeField] private TMP_Text xpLabel;
    [SerializeField] private Image xpFill;
    [SerializeField] private Image netButtonImage;
    [SerializeField] private Image spearButtonImage;
    [SerializeField] private GameObject levelPanel;
    [SerializeField] private CanvasGroup levelGroup;
    [SerializeField] private Button plankButton;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button rangeButton;
    [SerializeField] private Button capacityButton;
    [SerializeField] private TMP_Text plankTitle;
    [SerializeField] private TMP_Text captureTitle;
    [SerializeField] private TMP_Text attackTitle;
    [SerializeField] private TMP_Text rangeTitle;
    [SerializeField] private TMP_Text capacityTitle;
    [SerializeField] private TMP_Text plankDesc;
    [SerializeField] private TMP_Text captureDesc;
    [SerializeField] private TMP_Text attackDesc;
    [SerializeField] private TMP_Text rangeDesc;
    [SerializeField] private TMP_Text capacityDesc;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private CanvasGroup resultGroup;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private GameObject primaryButtonObject;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private RectTransform primaryRect;
    [SerializeField] private RectTransform backRect;

    public CanvasGroup LevelGroup => levelGroup;
    public CanvasGroup ResultGroup => resultGroup;

    public void SelectNet()
    {
        if (director != null)
        {
            director.SelectNet();
        }
    }

    public void SelectSpear()
    {
        if (director != null)
        {
            director.SelectSpear();
        }
    }

    public void ChoosePlank()
    {
        if (director != null)
        {
            director.ChoosePlank();
        }
    }

    public void ChooseCapture()
    {
        if (director != null)
        {
            director.ChooseCapture();
        }
    }

    public void ChooseAttack()
    {
        if (director != null)
        {
            director.ChooseAttack();
        }
    }

    public void ChooseRange()
    {
        if (director != null)
        {
            director.ChooseRange();
        }
    }

    public void ChooseCapacity()
    {
        if (director != null)
        {
            director.ChooseCapacity();
        }
    }

    public void OnPrimary()
    {
        if (director != null)
        {
            director.OnPrimary();
        }
    }

    public void OnBack()
    {
        if (director != null)
        {
            director.OnBack();
        }
    }

    public void SetCounts(int wood, int leaf, int scrap)
    {
        Set(woodText, "Wood " + wood);
        Set(leafText, "Leaf " + leaf);
        Set(scrapText, "Meat " + scrap);
        if (scrapText != null)
        {
            scrapText.color = BattleTuning.MeatColor;
        }
    }

    public void SetXp(int level, int xp, int need)
    {
        Set(xpLabel, "Lv." + level + "  " + xp + "/" + need);
        if (xpFill != null)
        {
            xpFill.fillAmount = need <= 0 ? 1f : Mathf.Clamp01((float)xp / need);
        }
    }

    public void SetWeapon(BattleWeapon weapon)
    {
        if (netButtonImage != null)
        {
            netButtonImage.color = weapon == BattleWeapon.Net
                ? new Color(0.20f, 0.62f, 0.82f, 1f)
                : new Color(0.18f, 0.28f, 0.36f, 1f);
        }

        if (spearButtonImage != null)
        {
            spearButtonImage.color = weapon == BattleWeapon.Spear
                ? new Color(0.78f, 0.46f, 0.16f, 1f)
                : new Color(0.32f, 0.24f, 0.16f, 1f);
        }
    }

    public void ShowLevelUp(bool canPlank, bool canBuild)
    {
        if (levelPanel != null)
        {
            levelPanel.SetActive(true);
        }

        Style(plankButton, plankTitle, plankDesc, canPlank, PlankBody);
        Style(captureButton, captureTitle, captureDesc, canBuild, CaptureBody);
        Style(attackButton, attackTitle, attackDesc, canBuild, AttackBody);
        Style(rangeButton, rangeTitle, rangeDesc, true, RangeBody);
        Style(capacityButton, capacityTitle, capacityDesc, true, CapacityBody);
    }

    public void HideLevelUp()
    {
        if (levelPanel != null)
        {
            levelPanel.SetActive(false);
        }
    }

    public void ShowResult(bool victory, bool showPrimary)
    {
        if (resultTitle != null)
        {
            resultTitle.text = victory ? "Victory" : "Defeat";
            resultTitle.color = victory ? new Color(1f, 0.9f, 0.55f, 1f) : new Color(0.95f, 0.45f, 0.4f, 1f);
        }

        Set(primaryLabel, victory ? "Next" : "Retry");
        if (primaryButtonObject != null)
        {
            primaryButtonObject.SetActive(showPrimary);
        }

        if (primaryRect != null && backRect != null)
        {
            if (showPrimary)
            {
                primaryRect.anchoredPosition = new Vector2(-130f, 48f);
                backRect.anchoredPosition = new Vector2(130f, 48f);
            }
            else
            {
                backRect.anchoredPosition = new Vector2(0f, 48f);
            }
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }
    }

    public void SetGroupBlocks(CanvasGroup group, bool blocks)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = 1f;
        group.blocksRaycasts = blocks;
        group.interactable = blocks;
    }

    private static void Style(Button button, TMP_Text title, TMP_Text desc, bool enabled, string body)
    {
        if (button != null)
        {
            button.interactable = enabled;
        }

        if (title != null)
        {
            title.color = enabled ? new Color(1f, 0.92f, 0.75f, 1f) : new Color(0.62f, 0.62f, 0.62f, 1f);
        }

        if (desc != null)
        {
            desc.text = enabled ? body : body + "\n" + CannotPlace;
        }
    }

    private static void Set(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}