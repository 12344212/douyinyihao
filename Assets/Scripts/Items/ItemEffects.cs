using UnityEngine;

/// <summary>
/// 道具使用效果的统一入口（扩展点）。
/// 当前版本尚未实现任何使用效果：TryUse 对所有道具都返回 false，并且不会扣除道具。
/// 以后实现效果时：在 TryApplyEffect 的 switch 里为对应 id 编写效果并返回 true。
/// TryUse 会在效果成功后从存档扣除 1 个。
/// </summary>
public static class ItemEffects
{
    /// <summary>使用 1 个道具：效果生效后才扣除数量。成功返回 true。</summary>
    public static bool TryUse(string itemId)
    {
        if (!GameItemDefs.Exists(itemId))
        {
            Debug.LogWarning($"[ItemEffects] Unknown item id '{itemId}'");
            return false;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[ItemEffects] LocalSaveManager missing.");
            return false;
        }

        if (save.GetItemCount(itemId) <= 0)
        {
            return false;
        }

        if (!TryApplyEffect(itemId))
        {
            return false;
        }

        return save.TryConsumeItem(itemId, 1);
    }

    /// <summary>只结算效果，不改存档。</summary>
    private static bool TryApplyEffect(string itemId)
    {
        switch (itemId)
        {
            default:
                // TODO: 新增道具时，在这里补充对应的使用效果。
                return false;
        }
    }
}
