/// <summary>
/// 充值钻石档。与参考项目相同的 6 档：展示名 Package 1~6，价格美元，图标地址 HB1~HB6。
/// </summary>
public static class GemPackDefs
{
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

    public static readonly Pack[] Packs =
    {
        new Pack { index = 1, displayName = "Package 1", productId = "com.demontw.id2254.pay30", gems = 30, priceUsd = 0.49f, iconLocation = "HB1" },
        new Pack { index = 2, displayName = "Package 2", productId = "com.demontw.id2254.pay120", gems = 120, priceUsd = 1.99f, iconLocation = "HB2" },
        new Pack { index = 3, displayName = "Package 3", productId = "com.demontw.id2254.pay300", gems = 300, priceUsd = 4.99f, iconLocation = "HB3" },
        new Pack { index = 4, displayName = "Package 4", productId = "com.demontw.id2254.pay1200", gems = 1280, priceUsd = 19.99f, iconLocation = "HB4" },
        new Pack { index = 5, displayName = "Package 5", productId = "com.demontw.id2254.pay3000", gems = 3280, priceUsd = 49.99f, iconLocation = "HB5" },
        new Pack { index = 6, displayName = "Package 6", productId = "com.demontw.id2254.pay6000", gems = 6480, priceUsd = 99.99f, iconLocation = "HB6" }
    };

    public static Pack GetByProductId(string productId)
    {
        for (int i = 0; i < Packs.Length; i++)
        {
            if (Packs[i].productId == productId)
            {
                return Packs[i];
            }
        }

        return default;
    }
}
