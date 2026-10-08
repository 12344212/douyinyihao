using UnityEngine;

/// <summary>
/// 充值档查询。数据在 GemPackConfig 资源里。
/// LoadScene 用 YooAsset 地址 GemPackConfig 加载后 Bind；IAP 注册商品 id 也读这份表。
/// </summary>
public static class GemPackDefs
{
    public const string ConfigAddress = "GemPackConfig";

    public struct Pack
    {
        public int index;
        public string displayName;
        public string productId;
        public int gems;
        public float priceUsd;
        public string iconLocation;

        public string PriceLabel
        {
            get { return "$" + priceUsd.ToString("0.00"); }
        }

        public string GemLabel
        {
            get { return "+" + gems; }
        }
    }

    private static GemPackConfig _config;

    public static GemPackConfig Current => _config;

    public static void Bind(GemPackConfig config)
    {
        if (config != null)
        {
            _config = config;
        }
    }

    public static Pack[] Packs
    {
        get
        {
            GemPackConfig.PackEntry[] entries = Entries();
            Pack[] packs = new Pack[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                packs[i] = ToPack(entries[i], i);
            }

            return packs;
        }
    }

    public static Pack GetByProductId(string productId)
    {
        if (string.IsNullOrEmpty(productId))
        {
            return default;
        }

        Pack[] packs = Packs;
        for (int i = 0; i < packs.Length; i++)
        {
            if (packs[i].productId == productId)
            {
                return packs[i];
            }
        }

        return default;
    }

    private static GemPackConfig.PackEntry[] Entries()
    {
        if (_config == null)
        {
            return new GemPackConfig.PackEntry[0];
        }

        return _config.Packs;
    }

    private static Pack ToPack(GemPackConfig.PackEntry entry, int fallbackIndex)
    {
        if (entry == null)
        {
            return new Pack { index = fallbackIndex + 1 };
        }

        return new Pack
        {
            index = entry.index > 0 ? entry.index : fallbackIndex + 1,
            displayName = entry.displayName,
            productId = entry.productId,
            gems = entry.gems,
            priceUsd = entry.priceUsd,
            iconLocation = entry.iconLocation
        };
    }
}
