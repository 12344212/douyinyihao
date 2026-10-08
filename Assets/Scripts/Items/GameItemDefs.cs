using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具查询。数据在 ItemConfig 资源里，不在代码里写死。
/// LoadScene 用 YooAsset 地址 ItemConfig 加载后 Bind；没加载到时用场景上的序列化引用。
/// </summary>
public static class GameItemDefs
{
    public const string ConfigAddress = "ItemConfig";
    public const string HealPotionId = "heal_potion";

    /// <summary>单个道具（从配置条目拷出来，给商店 / 背包用）。</summary>
    public sealed class Def
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string description;
        public readonly int price;
        public readonly CurrencyType currency;
        public readonly string iconLocation;

        public Def(string id, string displayName, string description, int price, CurrencyType currency, string iconLocation)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.price = price;
            this.currency = currency;
            this.iconLocation = iconLocation;
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

    /// <summary>货币图标地址。这不是道具表里的条目。</summary>
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

        return new Def(entry.id, entry.displayName, entry.description, entry.price, entry.currency, entry.iconLocation);
    }
}
