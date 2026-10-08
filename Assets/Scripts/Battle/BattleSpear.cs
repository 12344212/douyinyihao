using UnityEngine;

/// <summary>
/// 矛。玩家和攻击塔共用这一份预制体。
/// </summary>
public class BattleSpear : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;

    public void SetTint(Color color)
    {
        if (rendererSprite != null)
        {
            rendererSprite.color = color;
        }
    }
}