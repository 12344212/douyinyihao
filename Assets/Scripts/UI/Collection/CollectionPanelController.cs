using TMPro;
using UnityEngine;

/// <summary>
/// CollectionPanel 控制器：刷新 CP1–CP15 格子状态与进度文案。
/// 挂在 CollectionPanel 根节点；格子引用由 CollectionPanelSetup 拖好。
/// </summary>
[DisallowMultipleComponent]
public class CollectionPanelController : MonoBehaviour
{
    [Header("Slots (CP1–CP15)")]
    [SerializeField] private CollectionCpSlot[] slots = new CollectionCpSlot[CollectionDefs.Count];

    [Header("Progress")]
    [SerializeField] private TMP_Text progressLabel;

    [Header("Panel")]
    [SerializeField] private UIPanel uiPanel;

    private void Awake()
    {
        if (uiPanel == null)
        {
            uiPanel = GetComponent<UIPanel>();
        }
    }

    private void OnEnable()
    {
        if (uiPanel != null)
        {
            uiPanel.Shown += OnPanelShown;
        }

        LocalSaveManager.OnCollectiblesChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (uiPanel != null)
        {
            uiPanel.Shown -= OnPanelShown;
        }

        LocalSaveManager.OnCollectiblesChanged -= Refresh;
    }

    private void OnPanelShown(UIPanel panel)
    {
        Refresh();
    }

    /// <summary>按存档刷新全部格子与进度（owned/15）。</summary>
    public void Refresh()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int ownedCount = 0;

        for (int i = 0; i < CollectionDefs.Count; i++)
        {
            bool owned = save != null && save.IsCollectibleOwned(i);
            bool claimed = save != null && save.IsCollectibleRewardClaimed(i);
            if (owned)
            {
                ownedCount++;
            }

            if (slots != null && i < slots.Length && slots[i] != null)
            {
                slots[i].ApplyState(owned, claimed);
            }
        }

        if (progressLabel != null)
        {
            progressLabel.text = ownedCount + "/" + CollectionDefs.Count;
        }
    }

    // ---------- 调试：正式获得走战斗打捞 SetCollectibleOwned ----------

    [ContextMenu("Debug/Own All Collectibles")]
    public void DebugOwnAll()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[CollectionPanel] LocalSaveManager missing.");
            return;
        }

        for (int i = 0; i < CollectionDefs.Count; i++)
        {
            save.SetCollectibleOwned(i, true);
        }

        Refresh();
        Debug.Log("[CollectionPanel] Debug: owned all CP1–CP15.");
    }

    [ContextMenu("Debug/Own CP1 Only")]
    public void DebugOwnCp1()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[CollectionPanel] LocalSaveManager missing.");
            return;
        }

        save.SetCollectibleOwned(0, true);
        Refresh();
        Debug.Log("[CollectionPanel] Debug: owned CP1.");
    }

    [ContextMenu("Debug/Clear All Collectibles")]
    public void DebugClearAll()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[CollectionPanel] LocalSaveManager missing.");
            return;
        }

        for (int i = 0; i < CollectionDefs.Count; i++)
        {
            save.SetCollectibleOwned(i, false);
        }

        Refresh();
        Debug.Log("[CollectionPanel] Debug: cleared all collectibles.");
    }

#if UNITY_EDITOR
    /// <summary>仅编辑器：Setup 写入槽位与进度文本引用。</summary>
    public void EditorBind(CollectionCpSlot[] boundSlots, TMP_Text progress, UIPanel panel)
    {
        slots = boundSlots;
        progressLabel = progress;
        uiPanel = panel;
    }
#endif
}
