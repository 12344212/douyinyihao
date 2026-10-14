using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;
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
    [SerializeField] private Image plankIcon;
    [SerializeField] private Image captureIcon;
    [SerializeField] private Image attackIcon;
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
    private int _offerGeneration;
    private readonly BattleUpgradeKind[] _slotKinds = new BattleUpgradeKind[3];
    private readonly Dictionary<string, Sprite> _offerSprites = new Dictionary<string, Sprite>();
    private Image _hurtFlash;
    private Tween _hurtFlashTween;
    private const float HurtFlashPeakAlpha = 0.22f;
    private const float HurtFlashDuration = 0.2f;

    public CanvasGroup LevelGroup => levelGroup;
    public CanvasGroup ResultGroup => resultGroup;

    private void Awake()
    {
        BindCanvasLikeMain();
    }

    private void OnDestroy()
    {
        KillXpTween();
        KillHurtFlashTween();
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

    public void SetCounts(int wood, int iron)
    {
        Set(woodText, wood.ToString());
        Set(leafText, iron.ToString());
    }

    public void SetHp(float current, float max)
    {
        float cap = max < 1f ? 1f : max;
        float now = Mathf.Clamp(current, 0f, cap);
        Set(hpText, Mathf.RoundToInt(now) + "/" + Mathf.RoundToInt(cap));
        if (hpFill != null)
        {
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFill.fillAmount = now / cap;
        }
    }

    /// <summary>Brief mild red screen flash when the player takes damage.</summary>
    public void PlayHurtFlash()
    {
        EnsureHurtFlash();
        if (_hurtFlash == null)
        {
            return;
        }

        KillHurtFlashTween();
        Color peak = new Color(1f, 0.12f, 0.12f, HurtFlashPeakAlpha);
        _hurtFlash.color = peak;
        _hurtFlash.enabled = true;
        _hurtFlash.gameObject.SetActive(true);
        _hurtFlashTween = _hurtFlash
            .DOFade(0f, HurtFlashDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (_hurtFlash != null)
                {
                    _hurtFlash.enabled = false;
                }
            });
    }

    private void EnsureHurtFlash()
    {
        if (_hurtFlash != null)
        {
            return;
        }

        GameObject go = new GameObject("HurtFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        // Stay under level/result modals so they remain readable.
        if (levelPanel != null)
        {
            go.transform.SetSiblingIndex(levelPanel.transform.GetSiblingIndex());
        }
        else if (resultPanel != null)
        {
            go.transform.SetSiblingIndex(resultPanel.transform.GetSiblingIndex());
        }
        else
        {
            rt.SetAsLastSibling();
        }

        _hurtFlash = go.GetComponent<Image>();
        _hurtFlash.raycastTarget = false;
        _hurtFlash.sprite = CreateWhiteSprite();
        _hurtFlash.type = Image.Type.Simple;
        _hurtFlash.color = new Color(1f, 0.12f, 0.12f, 0f);
        _hurtFlash.enabled = false;
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private void KillHurtFlashTween()
    {
        if (_hurtFlashTween != null && _hurtFlashTween.IsActive())
        {
            _hurtFlashTween.Kill();
        }

        _hurtFlashTween = null;
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

    private const float WeaponButtonSelectedScale = 0.9f;
    private const float WeaponButtonUnselectedScale = 1.1f;

    public void SetWeapon(BattleWeapon weapon)
    {
        // Selection feedback is scale only; do not tint Net/Spear button colors.
        if (netButtonImage != null)
        {
            float scale = weapon == BattleWeapon.Net
                ? WeaponButtonSelectedScale
                : WeaponButtonUnselectedScale;
            netButtonImage.transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (spearButtonImage != null)
        {
            float scale = weapon == BattleWeapon.Spear
                ? WeaponButtonSelectedScale
                : WeaponButtonUnselectedScale;
            spearButtonImage.transform.localScale = new Vector3(scale, scale, 1f);
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

        _offerGeneration++;
        BindOfferSlot(0, plankButton, plankTitle, plankDesc, plankIcon, offers);
        BindOfferSlot(1, captureButton, captureTitle, captureDesc, captureIcon, offers);
        BindOfferSlot(2, attackButton, attackTitle, attackDesc, attackIcon, offers);
    }

    private void BindOfferSlot(int index, Button button, TMP_Text title, TMP_Text desc, Image icon, BattleUpgradeKind[] offers)
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
        _slotKinds[index] = kind;
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
        ShowOfferIcon(index, icon, kind);
    }

    private void ShowOfferIcon(int slot, Image icon, BattleUpgradeKind kind)
    {
        if (icon == null)
        {
            return;
        }

        icon.preserveAspect = true;
        icon.raycastTarget = false;
        string address = OfferIconAddress(kind);
        Sprite cached;
        if (_offerSprites.TryGetValue(address, out cached) && cached != null)
        {
            icon.sprite = cached;
            icon.color = Color.white;
            return;
        }

        icon.sprite = null;
        icon.color = new Color(1f, 1f, 1f, 0f);
        StartCoroutine(LoadOfferIcon(_offerGeneration, slot, icon, address, kind));
    }

    private IEnumerator LoadOfferIcon(int generation, int slot, Image icon, string address, BattleUpgradeKind kind)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (yoo == null)
        {
            yield break;
        }

        Sprite loaded = null;
        yield return yoo.LoadSpriteAsync(address, sprite => loaded = sprite);
        if (loaded == null)
        {
            yield break;
        }

        _offerSprites[address] = loaded;
        if (generation != _offerGeneration || icon == null || _slotKinds[slot] != kind)
        {
            yield break;
        }

        icon.sprite = loaded;
        icon.color = Color.white;
        icon.preserveAspect = true;
    }

    /// <summary>升级卡图标地址。同组建筑共用一张图。</summary>
    public static string OfferIconAddress(BattleUpgradeKind kind)
    {
        switch (kind)
        {
            case BattleUpgradeKind.HeroRange:
            case BattleUpgradeKind.NetSpeed:
                return "ZD-DFW";
            case BattleUpgradeKind.HeroCapacity:
            case BattleUpgradeKind.NetSize:
                return "ZD-DSD";
            case BattleUpgradeKind.HeroDamage:
                return "ZD-DGJ";
            case BattleUpgradeKind.Plank:
                return "ZD-MB1";
            case BattleUpgradeKind.PlaceAttack:
            case BattleUpgradeKind.AttackRange:
            case BattleUpgradeKind.AttackDamage:
                return "ZD-DWQ1";
            case BattleUpgradeKind.PlaceAttractor:
            case BattleUpgradeKind.AttractorRange:
            case BattleUpgradeKind.AttractorFrequency:
                return "ZD-DWQ2";
            case BattleUpgradeKind.PlaceCapture:
            case BattleUpgradeKind.CaptureRange:
            case BattleUpgradeKind.CaptureCapacity:
            case BattleUpgradeKind.CaptureSize:
            case BattleUpgradeKind.CaptureSpeed:
                return "ZD-DWQ3";
            case BattleUpgradeKind.GainIron:
                return "ZD-DTK1";
            case BattleUpgradeKind.GainWood:
                return "ZD-MT";
            case BattleUpgradeKind.RestoreHealth:
                return "ZD-DXL";
            default:
                return "ZD-DFW";
        }
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
                body = "Build on an empty plank. Auto-attacks enemies in range, harder and faster than an Attack Post.";
                break;
            case BattleUpgradeKind.AttractorRange:
                title = "Attractor Range";
                body = "All Attractors reach farther.";
                break;
            case BattleUpgradeKind.AttractorFrequency:
                title = "Attractor Frequency";
                body = "Attractors attack more often.";
                break;
            case BattleUpgradeKind.GainIron:
                title = "Iron";
                body = "Gain " + BattleTuning.OfferIronAmount + " iron.";
                break;
            case BattleUpgradeKind.GainWood:
                title = "Wood";
                body = "Gain " + BattleTuning.OfferWoodAmount + " wood.";
                break;
            case BattleUpgradeKind.RestoreHealth:
                title = "Restore";
                body = "Fully restore your health.";
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

    /// <summary>放置时关掉 HUD 点击，只把点击留给场景里的木板。</summary>
    public void SetBattleInputEnabled(bool enabled)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = enabled;
        group.interactable = enabled;
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
