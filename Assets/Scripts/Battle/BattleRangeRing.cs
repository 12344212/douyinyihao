using UnityEngine;

/// <summary>
/// 预制体上的范围圈。直径按父节点缩放换算，使世界直径等于半径的两倍。
/// </summary>
public class BattleRangeRing : MonoBehaviour
{
    [SerializeField] private SpriteRenderer ringSprite;

    public void SetRadius(float radius)
    {
        if (radius < 0f)
        {
            radius = 0f;
        }

        float parent = 1f;
        if (transform.parent != null)
        {
            parent = Mathf.Abs(transform.parent.lossyScale.x);
        }

        if (parent < 0.0001f)
        {
            parent = 1f;
        }

        float local = radius * 2f / parent;
        transform.localScale = new Vector3(local, local, 1f);
        if (ringSprite != null)
        {
            ringSprite.enabled = true;
        }
    }
}