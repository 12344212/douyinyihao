using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Item lookup. Data lives in ItemConfig; LoadScene binds via YooAsset address ItemConfig.
/// </summary>
public static class GameItemDefs
{
    public const string ConfigAddress = "ItemConfig";
    public const string HealPotionId = "heal_potion";
    public const string GoldPouchId = "gold_pouch";

    public const string IronPikeId = "ZB-BQ1";
    public const string CoralLanceId = "ZB-BQ2";
    public const string TideHoodId = "ZB-MZ1";
    public const string DeepveilMaskId = "ZB-MZ2";
    public const string ReefBootsId = "ZB-XZ1";
    public const string GaleSolesId = "ZB-XZ2";
    public const string ShellPlateId = "ZB-YF1";
    public const string AbyssMailId = "ZB-YF2";

    /// <summary>Single item (copied from config for shop / bag).</summary>
    public sealed class Def
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string description;
        public readonly int price;
        public readonly CurrencyType currency;
        public readonly string iconLocation;
        public readonly CurrencyType grantCurrency;
        public readonly int grantAmount;
        public readonly EquipItemType equipType;
        public readonly int attackBonus;
        public readonly string statBonusLabel;
        public readonly float statBonusValue;
        public readonly string specialEffectId;
        public readonly float specialValue;
        public readonly string specialDescription;

        public Def(
            string id,
            string displayName,
            string description,
            int price,
            CurrencyType currency,
            string iconLocation,
            CurrencyType grantCurrency = CurrencyType.Gold,
            int grantAmount = 0,
            EquipItemType equipType = EquipItemType.None,
            int attackBonus = 0,
            string statBonusLabel = null,
            float statBonusValue = 0f,
            string specialEffectId = null,
            float specialValue = 0f,
            string specialDescription = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.price = price;
            this.currency = currency;
            this.iconLocation = iconLocation;
            this.grantCurrency = grantCurrency;
            this.grantAmount = grantAmount;
            this.equipType = equipType;
            this.attackBonus = attackBonus;
            this.statBonusLabel = statBonusLabel ?? string.Empty;
            this.statBonusValue = statBonusValue;
            this.specialEffectId = specialEffectId ?? string.Empty;
            this.specialValue = specialValue;
            this.specialDescription = specialDescription ?? string.Empty;
        }

        public bool IsEquipment => equipType != EquipItemType.None;

        /// <summary>True when purchase grants currency immediately instead of a bag stack.</summary>
        public bool IsCurrencyPack => grantAmount > 0;

        /// <summary>English type label for UI.</summary>
        public string EquipTypeLabel
        {
            get
            {
                switch (equipType)
                {
                    case EquipItemType.Spear: return "Spear";
                    case EquipItemType.Hood: return "Hood";
                    case EquipItemType.Shoes: return "Shoes";
                    case EquipItemType.Armor: return "Armor";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>Description plus type / stat / special lines for ItemData panels.</summary>
        public string FormatDetailText()
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(description))
            {
                sb.Append(description);
            }

            if (equipType == EquipItemType.None)
            {
                return sb.ToString();
            }

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append("Type: ").Append(EquipTypeLabel);
            if (!string.IsNullOrEmpty(statBonusLabel))
            {
                sb.Append("\nStat: ").Append(statBonusLabel);
            }
            else if (attackBonus > 0)
            {
                sb.Append("\nStat: Attack +").Append(attackBonus);
            }

            if (!string.IsNullOrEmpty(specialDescription))
            {
                sb.Append("\nSpecial: ").Append(specialDescription);
            }

            return sb.ToString();
        }
    }

    private static ItemConfig _config;

    public static ItemConfig Current => _config;

    public static void Bind(ItemConfig config)
    {
        if (config != null)
        {
            _config = config;
        }
    }

    public static IReadOnlyList<Def> All
    {
        get
        {
            ItemConfig.ItemEntry[] entries = Entries();
            Def[] defs = new Def[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                defs[i] = ToDef(entries[i]);
            }

            return defs;
        }
    }

    public static int Count => Entries().Length;

    public static bool Exists(string id)
    {
        return Get(id) != null;
    }

    public static bool TryGet(string id, out Def def)
    {
        def = Get(id);
        return def != null;
    }

    public static Def Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        ItemConfig.ItemEntry[] entries = Entries();
        for (int i = 0; i < entries.Length; i++)
        {
            ItemConfig.ItemEntry entry = entries[i];
            if (entry != null && entry.id == id)
            {
                return ToDef(entry);
            }
        }

        return null;
    }

    /// <summary>Currency icon address. Not an item-table entry.</summary>
    public static string CurrencyIconLocation(CurrencyType currency)
    {
        return currency == CurrencyType.Gems ? "Icon_Gems" : "Icon_Gold";
    }

    private static ItemConfig.ItemEntry[] Entries()
    {
        if (_config == null)
        {
            return new ItemConfig.ItemEntry[0];
        }

        return _config.Items;
    }

    private static Def ToDef(ItemConfig.ItemEntry entry)
    {
        if (entry == null)
        {
            return new Def(string.Empty, string.Empty, string.Empty, 0, CurrencyType.Gold, string.Empty);
        }

        return new Def(
            entry.id,
            entry.displayName,
            entry.description,
            entry.price,
            entry.currency,
            entry.iconLocation,
            entry.grantCurrency,
            entry.grantAmount,
            entry.equipType,
            entry.attackBonus,
            entry.statBonusLabel,
            entry.statBonusValue,
            entry.specialEffectId,
            entry.specialValue,
            entry.specialDescription);
    }
}
