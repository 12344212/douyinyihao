using System;
using UnityEngine;

/// <summary>
/// 道具配置。在 Inspector 里增删条目即可，商店和背包按这份表显示，不用改代码。
/// 地址：ItemConfig（YooAsset AddressByFileName）。
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
    }
}
