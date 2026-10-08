using TMPro;
using UnityEngine;

/// <summary>
/// 网圈预制体。右上角三枚数量只显示当前网里的木头、叶子、废料，为 0 时隐藏。
/// </summary>
public class BattleNet : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [SerializeField] private TMP_Text woodCount;
    [SerializeField] private TMP_Text leafCount;
    [SerializeField] private TMP_Text scrapCount;
    [SerializeField] private GameObject woodFrame;
    [SerializeField] private GameObject leafFrame;
    [SerializeField] private GameObject scrapFrame;

    private void Awake()
    {
        if (woodCount != null)
        {
            woodCount.color = BattleTuning.WoodColor;
        }

        if (leafCount != null)
        {
            leafCount.color = BattleTuning.LeafColor;
        }

        if (scrapCount != null)
        {
            scrapCount.color = BattleTuning.MeatColor;
        }

        SetHeldCounts(0, 0, 0);
    }

    public void SetTint(Color color)
    {
        if (rendererSprite != null)
        {
            rendererSprite.color = color;
        }
    }

    public void SetHeldCounts(int wood, int leaf, int scrap)
    {
        ShowCount(woodFrame, woodCount, wood);
        ShowCount(leafFrame, leafCount, leaf);
        ShowCount(scrapFrame, scrapCount, scrap);
    }

    private static void ShowCount(GameObject frame, TMP_Text label, int count)
    {
        bool visible = count > 0;
        if (frame != null && frame.activeSelf != visible)
        {
            frame.SetActive(visible);
        }

        if (label == null)
        {
            return;
        }

        if (frame == null && label.gameObject.activeSelf != visible)
        {
            label.gameObject.SetActive(visible);
        }
        else if (visible && !label.gameObject.activeSelf)
        {
            label.gameObject.SetActive(true);
        }

        if (visible)
        {
            label.text = count.ToString();
        }
    }
}