using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MainScene 教程目标解析：
/// - upgrade.spearItem：Equit 列表中 Iron Pike（ZB-BQ1）那一格（列表由 UpgradeEquipController 运行时生成）
/// - upgrade.alreadyUpgraded：角色已升过级（level > MinLevel）或已无法再升
/// - upgrade.spearEquipped：Iron Pike 已装备
/// 打开 Equit 前会 EnsureStarterSpear：若未拥有则发放一把 ZB-BQ1。
/// </summary>
public class MainTutorialTargets : TutorialTargetProvider
{
    [Tooltip("UpgradePanel Equit 列表根（E / listRoot），运行时生成 BagItemSlot 子节点")]
    [SerializeField] private Transform equitListRoot;

    [Tooltip("发放的第一把矛 itemId，默认 Iron Pike")]
    [SerializeField] private string starterSpearId = GameItemDefs.IronPikeId;

    private void OnEnable()
    {
        // main 完成后、upgrade 未完成时发放第一把矛，保证打开 Equit 时列表里有 ZB-BQ1
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save != null && save.IsTutorialDone(TutorialSession.MainKey) && !save.IsTutorialDone(TutorialSession.UpgradeKey))
        {
            EnsureStarterSpear();
        }
    }
    public override Transform Resolve(string key)
    {
        if (key == "upgrade.spearItem")
        {
            EnsureStarterSpear();
            return FindSpearItem();
        }

        return null;
    }

    public override bool CheckCondition(string key)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            return false;
        }

        if (key == "upgrade.alreadyUpgraded")
        {
            int level = save.GetCharacterLevel();
            if (level > CharacterUpgradeDefs.MinLevel)
            {
                return true;
            }

            if (level >= CharacterUpgradeDefs.MaxLevel)
            {
                return true;
            }

            int cost = CharacterUpgradeDefs.CostToNext(level);
            return !save.CanAfford(CurrencyType.Gold, cost);
        }

        if (key == "upgrade.spearEquipped")
        {
            string id = string.IsNullOrEmpty(starterSpearId) ? GameItemDefs.IronPikeId : starterSpearId;
            return save.IsItemEquipped(id);
        }

        return true;
    }

    /// <summary>若账号尚未拥有第一把矛，则发放一把（教程用）。</summary>
    public void EnsureStarterSpear()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            return;
        }

        string id = string.IsNullOrEmpty(starterSpearId) ? GameItemDefs.IronPikeId : starterSpearId;
        if (save.HasItem(id))
        {
            return;
        }

        if (save.AddItem(id, 1))
        {
            Debug.Log("[MainTutorialTargets] granted starter spear id=" + id);
        }
        else
        {
            Debug.LogWarning("[MainTutorialTargets] failed to grant starter spear id=" + id);
        }
    }

    private Transform FindSpearItem()
    {
        if (equitListRoot == null || !equitListRoot.gameObject.activeInHierarchy)
        {
            return null;
        }

        string id = string.IsNullOrEmpty(starterSpearId) ? GameItemDefs.IronPikeId : starterSpearId;
        for (int i = 0; i < equitListRoot.childCount; i++)
        {
            Transform child = equitListRoot.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            BagItemSlot slot = child.GetComponent<BagItemSlot>();
            if (slot != null && slot.ItemId == id)
            {
                return child;
            }
        }

        return null;
    }
}