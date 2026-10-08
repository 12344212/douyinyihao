using UnityEngine;

/// <summary>
/// 塔。捕获塔和攻击塔是两个预制体，种类用序列化枚举区分。
/// </summary>
public class BattleTower : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [SerializeField] private BattleRangeRing rangeRing;
    [SerializeField] private BattleBuildingKind kind = BattleBuildingKind.Capture;

    public BattleBuildingKind Kind => kind;

    public void ApplyColor(Color color)
    {
        if (rendererSprite != null)
        {
            rendererSprite.color = color;
        }
    }

    public void SetRangeRadius(float radius)
    {
        if (rangeRing != null)
        {
            rangeRing.SetRadius(radius);
        }
    }
}