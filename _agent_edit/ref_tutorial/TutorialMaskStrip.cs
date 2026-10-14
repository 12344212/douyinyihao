using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 四向遮罩条之一：挡住射线；被点击时通知 TutorialOverlay（用于纯文本步骤的“点任意处继续”）。
/// </summary>
public class TutorialMaskStrip : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TutorialOverlay overlay;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (overlay != null)
        {
            overlay.NotifyMaskTapped();
        }
    }
}
