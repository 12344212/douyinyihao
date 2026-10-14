using UnityEngine;

/// <summary>
/// 矛。玩家和攻击塔共用这一份预制体。
/// </summary>
public class BattleSpear : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [Tooltip("头部")]
    [SerializeField] private Transform headPoint;
    [Tooltip("尾部")]
    [SerializeField] private Transform tailPoint;

    public void SetTint(Color color)
    {
        if (rendererSprite != null)
        {
            rendererSprite.color = color;
        }
    }

    public void SetSprite(Sprite sprite)
    {
        if (rendererSprite != null && sprite != null)
        {
            rendererSprite.sprite = sprite;
        }
    }

    /// <summary>让头部（fire）到尾部（wei）的连线指向发射方向。</summary>
    public void AimHead(Vector3 worldDirection)
    {
        worldDirection.z = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float aim = Mathf.Atan2(worldDirection.y, worldDirection.x) * Mathf.Rad2Deg;
        float axis = 0f;
        if (headPoint != null && tailPoint != null)
        {
            Vector3 local = headPoint.localPosition - tailPoint.localPosition;
            local.z = 0f;
            if (local.sqrMagnitude > 0.0001f)
            {
                axis = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            }
        }

        transform.rotation = Quaternion.Euler(0f, 0f, aim - axis);
    }
}