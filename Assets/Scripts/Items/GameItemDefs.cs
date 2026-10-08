using System;
using System.Collections.Generic;

/// <summary>
/// 道具定义静态表（以后商店 / 背包共用）。当前只有一个道具：治疗药水。
/// 新增道具：加一个 id 常量，并在 Defs 数组里加一条 Def。
/// 图标走 YooAsset（收集规则 AddressByFileName：地址 = 图片文件名，不带扩展名），
/// 用 YooAssetManager.Instance.LoadSpriteAsync(def.iconLocation, ...) 加载，不要写死资源路径。
/// </summary>
public static class GameItemDefs
{
    public const string HealPotionId = "heal_potion";

    /// <summary>单个道具的定义（只读）。displayName / description 为玩家可见英文文案。</summary>
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

    private static readonly Def[] Defs =
    {
        new Def(HealPotionId, "Healing Potion", "Restores 30% of max HP.", 150, CurrencyType.Gold, "Item_HealPotion"),
    };

    private static Dictionary<string, Def> _byId;

    /// <summary>全部道具（按表中顺序，供商店 / 背包列表使用）。</summary>
    public static IReadOnlyList<Def> All => Defs;

    public static int Count => Defs.Length;

    public static bool Exists(string id)
    {
        return Get(id) != null;
    }

    public static bool TryGet(string id, out Def def)
    {
        def = Get(id);
        return def != null;
    }

    /// <summary>按 id 查找，找不到返回 null。</summary>
    public static Def Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (_byId == null)
        {
            Dictionary<string, Def> map = new Dictionary<string, Def>(StringComparer.Ordinal);
            for (int i = 0; i < Defs.Length; i++)
            {
                map[Defs[i].id] = Defs[i];
            }

            _byId = map;
        }

        Def def;
        return _byId.TryGetValue(id, out def) ? def : null;
    }

    /// <summary>货币图标的 YooAsset 地址（图片放进收集目录后按文件名寻址）。</summary>
    public static string CurrencyIconLocation(CurrencyType currency)
    {
        return currency == CurrencyType.Gems ? "Icon_Gems" : "Icon_Gold";
    }
}