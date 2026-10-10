using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 HUD。数值、按钮、血条等在预制体上配好。按钮只调本地方法再转发 BattleDirector。
/// </summary>
public class BattleHudView : MonoBehaviour
{
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
    [Tooltip("XpFill 进度条 DOTween 时长（秒）")]
    [SerializeField] private float xpTweenDuration = 0.35f;

    private Tween _xpTween;
    private int _shownLevel = -1;
    private float _shownFill;

    public CanvasGroup LevelGroup => levelGroup;
    public CanvasGroup ResultGroup => resultGroup;

    private void Awake()
    {
        BindCanvasLikeMain();
    }

    private void OnDestroy()
    {
        KillXpTween();
    }

    /// <summary>与主场景 UI 一致：Screen Space - Camera + Scale With Screen Size / Expand。</summary>
    private void BindCanvasLikeMain()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 100f;
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null && canvas != null)
        {
            scaler = canvas.GetComponent<CanvasScaler>();
        }

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;
        }
    }

    public void SelectNet()
    {
        if (director != null) { director.SelectNet(); }
    }

    public void SelectSpear()
    {
        if (director != null) { director.SelectSpear(); }
    }

    public void ChoosePlank()
    {
        if (director != null) { director.ChoosePlank(); }
    }

    public void ChooseCapture()
    {
        if (director != null) { director.ChooseCapture(); }
    }

    public void ChooseAttack()
    {
        if (director != null) { director.ChooseAttack(); }
    }

    public void ChooseRange()
    {
        if (director != null) { director.ChooseRange(); }
    }

    public void ChooseCapacity()
    {
        if (director != null) { director.ChooseCapacity(); }
    }

    public void OnPrimary()
    {
        if (director != null) { director.OnPrimary(); }
    }

    public void OnBack()
    {
        if (director != null) { director.OnBack(); }
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
        if (xpFill == null)
        {
            return;
        }

        float target = need <= 0 ? 1f : Mathf.Clamp01((float)xp / need);
        // 首次直接对齐，避免进战斗闪一下
        if (_shownLevel < 0)
        {
            _shownLevel = level;
            _shownFill = target;
            xpFill.fillAmount = target;
            return;
        }

        KillXpTween();
        float duration = Mathf.Max(0.05f, xpTweenDuration);

        if (level > _shownLevel)
        {
            // 升级：先涨满，再归零后涨到新进度
            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(xpFill.DOFillAmount(1f, duration * 0.45f).SetEase(Ease.OutQuad));
            seq.AppendCallback(() =>
            {
                xpFill.fillAmount = 0f;
                _shownFill = 0f;
                _shownLevel = level;
            });
            seq.Append(xpFill.DOFillAmount(target, duration * 0.55f).SetEase(Ease.OutQuad));
            seq.OnComplete(() => { _shownFill = target; });
            _xpTween = seq;
            return;
        }

        if (level < _shownLevel)
        {
            _shownLevel = level;
            xpFill.fillAmount = 0f;
            _shownFill = 0f;
        }
        else
        {
            _shownLevel = level;
        }

        _xpTween = xpFill.DOFillAmount(target, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => { _shownFill = target; });
    }

    private void KillXpTween()
    {
        if (_xpTween != null && _xpTween.IsActive())
        {
            _xpTween.Kill();
        }

        _xpTween = null;
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

    /// <summary>展示随机抽到的 3 张升级卡（复用前三张卡槽，隐藏 Range/Capacity 卡）。</summary>
    public void ShowLevelUpOffers(BattleUpgradeKind[] offers)
    {
        if (levelPanel != null)
        {
            levelPanel.SetActive(true);
        }

        if (rangeButton != null)
        {
            rangeButton.gameObject.SetActive(false);
        }

        if (capacityButton != null)
        {
            capacityButton.gameObject.SetActive(false);
        }

        BindOfferSlot(0, plankButton, plankTitle, plankDesc, offers);
        BindOfferSlot(1, captureButton, captureTitle, captureDesc, offers);
        BindOfferSlot(2, attackButton, attackTitle, attackDesc, offers);
    }

    private static void BindOfferSlot(int index, Button button, TMP_Text title, TMP_Text desc, BattleUpgradeKind[] offers)
    {
        if (button == null)
        {
            return;
        }

        bool ok = offers != null && index < offers.Length;
        button.gameObject.SetActive(ok);
        if (!ok)
        {
            return;
        }

        BattleUpgradeKind kind = offers[index];
        string head;
        string body;
        GetOfferText(kind, out head, out body);
        if (title != null)
        {
            title.text = head;
            title.color = new Color(1f, 0.92f, 0.75f, 1f);
        }

        if (desc != null)
        {
            desc.text = body;
        }

        button.interactable = true;
    }

    private static void GetOfferText(BattleUpgradeKind kind, out string title, out string body)
    {
        switch (kind)
        {
            case BattleUpgradeKind.Plank:
                title = "Plank";
                body = "Place one plank on an adjacent empty cell.";
                break;
            case BattleUpgradeKind.PlaceCapture:
                title = "Capture Post";
                body = "Build on an empty plank. Auto-nets resources in range.";
                break;
            case BattleUpgradeKind.PlaceAttack:
                title = "Attack Post";
                body = "Build on an empty plank. Auto-attacks enemies in range.";
                break;
            case BattleUpgradeKind.HeroRange:
                title = "Throw Range";
                body = "Increases how far you can throw the net and the spear.";
                break;
            case BattleUpgradeKind.HeroCapacity:
                title = "Net Capacity";
                body = "Your net can hold 4 more resources.";
                break;
            case BattleUpgradeKind.HeroDamage:
                title = "Hero Damage";
                body = "Increases spear damage.";
                break;
            case BattleUpgradeKind.NetSize:
                title = "Net Size";
                body = "Your net becomes larger and catches more easily.";
                break;
            case BattleUpgradeKind.NetSpeed:
                title = "Net Speed";
                body = "Your net flies out faster.";
                break;
            case BattleUpgradeKind.CaptureRange:
                title = "Capture Range";
                body = "All Capture Posts reach farther.";
                break;
            case BattleUpgradeKind.CaptureCapacity:
                title = "Capture Capacity";
                body = "Capture nets can hold more resources.";
                break;
            case BattleUpgradeKind.CaptureSize:
                title = "Capture Net Size";
                body = "Capture Post nets become larger.";
                break;
            case BattleUpgradeKind.CaptureSpeed:
                title = "Capture Speed";
                body = "Capture Posts throw nets more often.";
                break;
            case BattleUpgradeKind.AttackRange:
                title = "Attack Range";
                body = "All Attack Posts reach farther.";
                break;
            case BattleUpgradeKind.AttackDamage:
                title = "Attack Damage";
                body = "Attack Posts deal more damage.";
                break;
            case BattleUpgradeKind.PlaceAttractor:
                title = "Attractor";
                body = "Build on an empty plank. Pulls nearby resources to you every few seconds.";
                break;
            case BattleUpgradeKind.AttractorRange:
                title = "Attractor Range";
                body = "All Attractors reach farther.";
                break;
            case BattleUpgradeKind.AttractorFrequency:
                title = "Attractor Frequency";
                body = "Attractors pull resources more often.";
                break;
            default:
                title = "Upgrade";
                body = "Improve your raft.";
                break;
        }
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
        if (primaryButtonObject != null) { primaryButtonObject.SetActive(showPrimary); }

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

        if (resultPanel != null) { resultPanel.SetActive(true); }
    }

    public void SetGroupBlocks(CanvasGroup group, bool blocks)
    {
        if (group == null) { return; }
        group.alpha = 1f;
        group.blocksRaycasts = blocks;
        group.interactable = blocks;
    }

    private static void Style(Button button, TMP_Text title, TMP_Text desc, bool enabled, string body)
    {
        if (button != null) { button.interactable = enabled; }
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
        if (label != null) { label.text = value; }
    }
}
