using System;
using UnityEngine;

/// <summary>
/// 充值档配置。在 Inspector 里增删条目即可，充值界面和 IAP 商品注册都读这份表。
/// 地址：GemPackConfig（YooAsset AddressByFileName）。
/// </summary>
[CreateAssetMenu(fileName = "GemPackConfig", menuName = "Template/Gem Pack Config")]
public class GemPackConfig : ScriptableObject
{
    [SerializeField] private PackEntry[] packs = new PackEntry[0];

    public PackEntry[] Packs => packs != null ? packs : new PackEntry[0];

    [Serializable]
    public class PackEntry
    {
        public int index;
        public string displayName;
        public string productId;
        public int gems;
        public float priceUsd;
        public string iconLocation;
    }
}
