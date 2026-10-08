using UnityEngine;

/// <summary>
/// 一种资源预制体，种类用序列化枚举切换三张已拖好的贴图。
/// </summary>
public class BattleResource : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [SerializeField] private Sprite woodSprite;
    [SerializeField] private Sprite leafSprite;
    [SerializeField] private Sprite scrapSprite;
    [SerializeField] private BattleResourceKind kind = BattleResourceKind.Wood;

    public BattleResourceKind Kind => kind;

    public void SetKind(BattleResourceKind value)
    {
        kind = value;
        if (rendererSprite == null)
        {
            return;
        }

        rendererSprite.color = Color.white;
        if (kind == BattleResourceKind.Leaf)
        {
            rendererSprite.sprite = leafSprite;
            transform.localScale = new Vector3(0.34f, 0.34f, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        }
        else if (kind == BattleResourceKind.Meat)
        {
            rendererSprite.sprite = scrapSprite;
            rendererSprite.color = BattleTuning.MeatColor;
            transform.localScale = new Vector3(0.28f, 0.28f, 1f);
            transform.rotation = Quaternion.identity;
        }
        else
        {
            rendererSprite.sprite = woodSprite;
            transform.localScale = new Vector3(0.4f, 0.28f, 1f);
            transform.rotation = Quaternion.identity;
        }
    }

    public void SetSortingOrder(int order)
    {
        if (rendererSprite != null)
        {
            rendererSprite.sortingOrder = order;
        }
    }
}