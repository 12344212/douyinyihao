using UnityEngine;

/// <summary>
/// 藏品 CP1–CP15 常量（地址与 YooAsset AddressByFileName 一致）。
/// </summary>
public static class CollectionDefs
{
    public const int Count = 15;
    public const int ClickGoldReward = 500;

    /// <summary>已拥有彩图地址：CP1 … CP15</summary>
    public static string OwnedAddress(int index)
    {
        return "CP" + (index + 1);
    }

    /// <summary>未拥有剪影地址：HDJ (1) … HDJ (15)</summary>
    public static string SilhouetteAddress(int index)
    {
        return "HDJ (" + (index + 1) + ")";
    }

    public static string OwnedAssetPath(int index)
    {
        return "Assets/UI/US-1010-BYDR-GM-CPDJ/CP" + (index + 1) + ".png";
    }

    public static string SilhouetteAssetPath(int index)
    {
        return "Assets/UI/US-1010-BYDR-GM-CPDJ/HDJ (" + (index + 1) + ").png";
    }

    public static bool IsValidIndex(int index)
    {
        return index >= 0 && index < Count;
    }
}
