using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包详情：名字、介绍、数量、Use。
/// Use 只调用 ItemEffects.TryUse。效果还没做时返回 false，这里弹出提示，不自己扣道具。
/// </summary>
public class BagDetailView : MonoBehaviour
{
    [SerializeField] private UIPanel panel;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descLabel;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private Button useButton;
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private string _itemId;

    private void Awake()
    {
        if (useButton != null)
        {
            useButton.onClick.AddListener(OnUse);
        }
    }

    private void OnDestroy()
    {
        if (useButton != null)
        {
            useButton.onClick.RemoveListener(OnUse);
        }
    }

    public void Show(string itemId, string displayName, string description, int count)
    {
        _itemId = itemId;
        if (panel != null)
        {
            panel.Show();
        }
        else
        {
            gameObject.SetActive(true);
        }

        if (nameLabel != null) nameLabel.text = displayName ?? string.Empty;
        if (descLabel != null) descLabel.text = description ?? string.Empty;
        if (countLabel != null) countLabel.text = "x" + Mathf.Max(0, count);
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = placeholderColor;
        }
    }

    public void Hide()
    {
        _itemId = null;
        if (panel != null)
        {
            panel.Hide();
        }
        else if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnUse()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            return;
        }

        // 治疗等效果尚未实现，TryUse 目前返回 false，不扣除道具。
        if (!ItemEffects.TryUse(_itemId))
        {
            TipPanelUI.ShowTip("Can't use this yet.");
        }
    }
}