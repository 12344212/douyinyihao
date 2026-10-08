using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 购买失败/取消的结果框。玩家可见文案为英文。和 TipPanelUI 的飘字不是同一个东西。
/// </summary>
public class PurchaseTipUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }
    }

    public void Show(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
