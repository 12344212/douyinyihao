using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 奖励弹窗里的一行：图标（纯色占位，有图再换）+ 名称 + x数量。
/// 由编辑器生成模板，GetPanelController 复制使用。
/// </summary>
public class RewardRowView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;

    public void Bind(string displayName, int count, Sprite sprite, Color placeholderColor)
    {
        if (nameText != null)
        {
            nameText.text = displayName ?? string.Empty;
        }

        if (countText != null)
        {
            countText.text = "x" + Mathf.Max(0, count);
        }

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.color = sprite != null ? Color.white : placeholderColor;
        }
    }
}