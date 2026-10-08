using System;
using System.Collections;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 顶部货币栏（预制体 Assets/Prefab/UI/TopBarPrefab.prefab）：头像按钮 + 金币 / 钻石数量与 "+" 按钮。
/// - 订阅 LocalSaveManager.OnCurrencyChanged 自动刷新，可选 DOTween 数字滚动。
/// - 按钮只发出事件（AvatarClicked / GoldPlusClicked / GemsPlusClicked），由 MainUIController 决定打开哪个面板。
/// - 图标默认是纯色占位；收集目录里有对应图片（GameItemDefs.CurrencyIconLocation）时通过 YooAsset 自动替换。
/// - 安全区适配由同节点上的 SafeAreaTopBar 负责。
/// </summary>
public class TopBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button avatarButton;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private Button goldPlusButton;
    [SerializeField] private Button gemsPlusButton;
    [SerializeField] private Image goldIcon;
    [SerializeField] private Image gemsIcon;

    [Header("Options")]
    [SerializeField] private bool rollNumbers = true;
    [SerializeField] private float rollDuration = 0.4f;
    [SerializeField] private bool loadIconsFromYooAsset = true;

    public event Action AvatarClicked;
    public event Action GoldPlusClicked;
    public event Action GemsPlusClicked;

    private CurrencyDisplay _gold;
    private CurrencyDisplay _gems;
    private static bool _warnedMissingSave;

    private void Awake()
    {
        _gold = new CurrencyDisplay(goldText);
        _gems = new CurrencyDisplay(gemsText);

        if (avatarButton != null) avatarButton.onClick.AddListener(OnAvatarClicked);
        if (goldPlusButton != null) goldPlusButton.onClick.AddListener(OnGoldPlusClicked);
        if (gemsPlusButton != null) gemsPlusButton.onClick.AddListener(OnGemsPlusClicked);
    }

    private void OnEnable()
    {
        LocalSaveManager.OnCurrencyChanged += OnCurrencyChanged;
        Refresh(false);
    }

    private void Start()
    {
        if (loadIconsFromYooAsset)
        {
            StartCoroutine(LoadIcons());
        }
    }

    private void OnDisable()
    {
        LocalSaveManager.OnCurrencyChanged -= OnCurrencyChanged;
        _gold?.Kill();
        _gems?.Kill();
    }

    private void OnDestroy()
    {
        if (avatarButton != null) avatarButton.onClick.RemoveListener(OnAvatarClicked);
        if (goldPlusButton != null) goldPlusButton.onClick.RemoveListener(OnGoldPlusClicked);
        if (gemsPlusButton != null) gemsPlusButton.onClick.RemoveListener(OnGemsPlusClicked);
    }

    /// <summary>从 LocalSaveManager 刷新金币 / 钻石显示。</summary>
    public void Refresh(bool animate = true)
    {
        if (_gold == null)
        {
            return; // Awake 之前
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            if (!_warnedMissingSave)
            {
                _warnedMissingSave = true;
                Debug.LogWarning("[TopBarUI] LocalSaveManager not found. Start the game from LoadScene to see real currency values.");
            }

            _gold.Set(0, false, 0f);
            _gems.Set(0, false, 0f);
            return;
        }

        bool roll = animate && rollNumbers;
        _gold.Set(save.Gold, roll, rollDuration);
        _gems.Set(save.Gems, roll, rollDuration);
    }

    private void OnCurrencyChanged()
    {
        Refresh(true);
    }

    private void OnAvatarClicked() => AvatarClicked?.Invoke();
    private void OnGoldPlusClicked() => GoldPlusClicked?.Invoke();
    private void OnGemsPlusClicked() => GemsPlusClicked?.Invoke();

    private IEnumerator LoadIcons()
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (yoo == null || !yoo.IsReady)
        {
            yield break;
        }

        yield return LoadIcon(yoo, goldIcon, GameItemDefs.CurrencyIconLocation(CurrencyType.Gold));
        yield return LoadIcon(yoo, gemsIcon, GameItemDefs.CurrencyIconLocation(CurrencyType.Gems));
    }

    private static IEnumerator LoadIcon(YooAssetManager yoo, Image target, string location)
    {
        // 没有对应图片资源时保留占位色块
        if (target == null || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && target != null)
        {
            target.sprite = sprite;
            target.color = Color.white;
        }
    }

    /// <summary>单个货币数字的显示与滚动。</summary>
    private sealed class CurrencyDisplay
    {
        private readonly TMP_Text _text;
        private int _shown = -1;
        private Tween _tween;

        public CurrencyDisplay(TMP_Text text)
        {
            _text = text;
        }

        public void Set(int target, bool animate, float duration)
        {
            if (_text == null)
            {
                return;
            }

            Kill();
            if (!animate || _shown < 0 || _shown == target || duration <= 0f)
            {
                Apply(target);
                return;
            }

            int from = _shown;
            _tween = DOTween.To(() => from, v => { from = v; Apply(v); }, target, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => Apply(target));
        }

        public void Kill()
        {
            if (_tween != null && _tween.IsActive())
            {
                _tween.Kill();
            }

            _tween = null;
        }

        private void Apply(int value)
        {
            _shown = value;
            if (_text != null)
            {
                _text.text = value.ToString("N0", CultureInfo.InvariantCulture);
            }
        }
    }
}