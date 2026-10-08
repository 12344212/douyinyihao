using UnityEngine;

/// <summary>
/// 购买过程中的挡板。显示时让序列化的转圈节点绕 Z 轴转。
/// </summary>
public class PurchaseLoadingUI : MonoBehaviour
{
    [SerializeField] private RectTransform spinTarget;
    [SerializeField] private float spinSpeed = 180f;

    private bool _spinning;

    private void Update()
    {
        if (!_spinning || spinTarget == null)
        {
            return;
        }

        spinTarget.Rotate(0f, 0f, -spinSpeed * Time.unscaledDeltaTime, Space.Self);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        _spinning = true;
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        _spinning = false;
        gameObject.SetActive(false);
        if (spinTarget != null)
        {
            spinTarget.localEulerAngles = Vector3.zero;
        }
    }
}
