using System;
using UnityEngine;

/// <summary>
/// Item config. Add/remove entries in Inspector; shop and bag read this table.
/// Address: ItemConfig (YooAsset AddressByFileName).
/// Equipment entries carry type / attack / special fields for display + stub battle hooks.
/// </summary>
[CreateAssetMenu(fileName = "ItemConfig", menuName = "Template/Item Config")]
public class ItemConfig : ScriptableObject
{
    [SerializeField] private ItemEntry[] items = new ItemEntry[0];

    public ItemEntry[] Items => items != null ? items : new ItemEntry[0];

    [Serializable]
    public class ItemEntry
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public int price;
        public CurrencyType currency;
        public string iconLocation;

        [Header("Equipment (optional)")]
        public EquipItemType equipType;
        public int attackBonus;
        [Tooltip("Short stat line shown in shop/bag, e.g. Attack +2 or Attack Range +0.2.")]
        public string statBonusLabel;
        [Tooltip("Numeric base bonus by type: spear unused (use attackBonus); hood attack range; shoes net speed mul; armor HP.")]
        public float statBonusValue;
        [Tooltip("Stable id for battle stub, e.g. crit_chance.")]
        public string specialEffectId;
        [Tooltip("Numeric special param: crit/lifesteal/dodge chance, EXP amount, capacity, retrieve levels, dmg reduce.")]
        public float specialValue;
        [TextArea] public string specialDescription;
    }
}
