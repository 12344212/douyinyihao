using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// mainScenes 的教程目标解析：
/// - role.weaponItem：角色面板装备列表里第一件武器格子（列表由 RolePanelController 运行时生成）
/// 列表还没刷新完（数量与已拥有装备不一致）时返回 null，TutorialManager 会继续等待。
/// 条件（重放教程时避免重复 / 反向操作）：
/// - role.cannotUpgrade：第一件武器已满级或金币不足升级 → 跳过升级步
/// - role.weaponEquipped：第一件武器已装备（再点 ZB 会卸下）→ 跳过装备步
/// </summary>
public class MainTutorialTargets : TutorialTargetProvider
{
    [Tooltip("RolePaenl/Dwon/Scroll View/Viewport/Content")]
    [SerializeField] private Transform roleContent;

    private readonly List<RectTransform> _slots = new List<RectTransform>(8);

    public override Transform Resolve(string key)
    {
        if (key == "role.weaponItem")
        {
            return FindWeaponItem();
        }

        return null;
    }

    public override bool CheckCondition(string key)
    {
        if (key == "role.cannotUpgrade" || key == "role.weaponEquipped")
        {
            LocalSaveManager save = LocalSaveManager.Instance;
            int id = FirstWeaponId();
            if (save == null || id <= 0)
            {
                // 配置未加载：不跳过，按正常步骤走
                return false;
            }

            if (key == "role.weaponEquipped")
            {
                return save.IsEquipped(id);
            }

            int level = save.GetEquipLevel(id);
            return level >= EquipConfig.MaxLevel || save.Gold < EquipConfig.UpgradeGoldCost(EquipConfig.Get(id), level);
        }

        return true;
    }

    private static int FirstWeaponId()
    {
        if (LocalSaveManager.Instance == null || EquipConfig.Current == null)
        {
            return 0;
        }

        List<int> owned = LocalSaveManager.Instance.OwnedEquipIds;
        for (int i = 0; i < owned.Count; i++)
        {
            EquipConfig.Entry entry = EquipConfig.Get(owned[i]);
            if (entry != null && entry.slot == EquipConfig.Slot.Weapon)
            {
                return owned[i];
            }
        }

        return 0;
    }

    private Transform FindWeaponItem()
    {
        if (roleContent == null || !roleContent.gameObject.activeInHierarchy
            || LocalSaveManager.Instance == null || EquipConfig.Current == null)
        {
            return null;
        }

        // 按 RolePanelController.RebuildList 的顺序：已拥有且配置存在的装备依次生成格子
        List<int> owned = LocalSaveManager.Instance.OwnedEquipIds;
        int valid = 0;
        int weaponIndex = -1;
        for (int i = 0; i < owned.Count; i++)
        {
            EquipConfig.Entry entry = EquipConfig.Get(owned[i]);
            if (entry == null)
            {
                continue;
            }

            if (weaponIndex < 0 && entry.slot == EquipConfig.Slot.Weapon)
            {
                weaponIndex = valid;
            }

            valid++;
        }

        if (weaponIndex < 0)
        {
            return null;
        }

        _slots.Clear();
        for (int i = 0; i < roleContent.childCount; i++)
        {
            Transform child = roleContent.GetChild(i);
            if (child.gameObject.activeSelf && child.GetComponent<EquipItemSlot>() != null)
            {
                _slots.Add(child as RectTransform);
            }
        }

        if (_slots.Count != valid)
        {
            return null;
        }

        return _slots[weaponIndex];
    }
}
