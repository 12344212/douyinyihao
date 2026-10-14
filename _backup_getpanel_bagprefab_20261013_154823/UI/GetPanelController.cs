using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 获得奖励弹窗（挂在 MainScene 的 CanvasReward 上，排序在功能面板之上、顶栏之下）。
/// 只负责展示，不发奖：调用方在 Show 之前自己用 LocalSaveManager 加金币 / 钻石 / 道具。
/// 不走 MainUIController 的“同时只开一个面板”规则，可盖在其它面板上面。
/// </summary>
public class GetPanelController : MonoBehaviour
{
    /// <summary>一条奖励。iconLocation 是 YooAsset 地址，可空（没有图就用色块）。</summary>
    [System.Serializable]
    public struct Reward
    {
        public string iconLocation;
        public string displayName;
        public int count;
        /// <summary>为 true 时按货币显示：图标地址为空则用 Icon_Gold / Icon_Gems。</summary>
        public bool useCurrency;
        public CurrencyType currency;
    }

    public static GetPanelController Instance { get; private set; }

    [SerializeField] private UIPanel panel;
    [SerializeField] private RectTransform rowRoot;
    [SerializeField] private RewardRowView rowTemplate;

    private readonly List<RewardRowView> _rows = new List<RewardRowView>(4);
    private Coroutine _loadCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (rowTemplate != null)
        {
            rowTemplate.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>弹出奖励列表。没有实例时只打警告。</summary>
    public static void Show(params Reward[] rewards)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[GetPanelController] No instance. Enter MainScene first.");
            return;
        }

        Instance.ShowRewards(rewards);
    }

    /// <summary>展示奖励。本方法不修改存档。</summary>
    public void ShowRewards(params Reward[] rewards)
    {
        if (rewards == null || rewards.Length == 0 || panel == null || rowRoot == null || rowTemplate == null)
        {
            Debug.LogWarning("[GetPanelController] Missing rewards or references.");
            return;
        }

        // 先激活再开协程（面板默认是隐藏的）
        panel.Show();
        RebuildRows(rewards);

        if (_loadCo != null)
        {
            StopCoroutine(_loadCo);
        }

        _loadCo = StartCoroutine(LoadIcons(rewards));
    }

    private void RebuildRows(Reward[] rewards)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
            {
                Destroy(_rows[i].gameObject);
            }
        }

        _rows.Clear();
        for (int i = 0; i < rewards.Length; i++)
        {
            if (rewards[i].count <= 0)
            {
                continue;
            }

            RewardRowView row = Instantiate(rowTemplate, rowRoot);
            row.gameObject.SetActive(true);
            row.Bind(rewards[i].displayName, rewards[i].count, null, PlaceholderColor(rewards[i]));
            _rows.Add(row);
        }
    }

    private IEnumerator LoadIcons(Reward[] rewards)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        int rowIndex = 0;
        for (int i = 0; i < rewards.Length; i++)
        {
            Reward reward = rewards[i];
            if (reward.count <= 0)
            {
                continue;
            }

            RewardRowView row = rowIndex < _rows.Count ? _rows[rowIndex] : null;
            rowIndex++;
            if (row == null)
            {
                continue;
            }

            string location = ResolveIcon(reward);
            if (yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
            {
                continue;
            }

            Sprite sprite = null;
            yield return yoo.LoadSpriteAsync(location, s => sprite = s);
            if (sprite != null && row != null)
            {
                row.Bind(reward.displayName, reward.count, sprite, Color.white);
            }
        }

        _loadCo = null;
    }

    private static string ResolveIcon(Reward reward)
    {
        if (!string.IsNullOrEmpty(reward.iconLocation))
        {
            return reward.iconLocation;
        }

        if (reward.useCurrency)
        {
            return GameItemDefs.CurrencyIconLocation(reward.currency);
        }

        return null;
    }

    private static Color PlaceholderColor(Reward reward)
    {
        if (!reward.useCurrency)
        {
            return new Color(0.55f, 0.40f, 0.85f, 1f);
        }

        return reward.currency == CurrencyType.Gems
            ? new Color(0.35f, 0.85f, 1f, 1f)
            : new Color(1f, 0.80f, 0.20f, 1f);
    }
}