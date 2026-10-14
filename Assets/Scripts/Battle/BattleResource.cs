using UnityEngine;

/// <summary>
/// 一种漂流资源。外观和缩放写在各自预制体上：ZD-MTP 木头、ZD-STP 生铁、ZD-SRP 生肉。图片地址仍是 ZD-MT、ZD-ST、ZD-SR。
/// </summary>
public class BattleResource : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [SerializeField] private BattleResourceKind kind = BattleResourceKind.Wood;

    public BattleResourceKind Kind => kind;

    public void SetKind(BattleResourceKind value)
    {
        kind = value;
    }

    public void SetSortingOrder(int order)
    {
        if (rendererSprite != null)
        {
            rendererSprite.sortingOrder = order;
        }
    }
}
