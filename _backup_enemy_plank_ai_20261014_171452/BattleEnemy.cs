using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enemy view shared by melee and ranged. Bind Firepoint for ranged.
/// Hpbg/Image (Filled) shows HP via hpFill; legacy barFill kept for old Sprite bars.
/// </summary>
public class BattleEnemy : MonoBehaviour
{
    [SerializeField] private Transform bodyScale;
    [SerializeField] private SpriteRenderer barFill;
    [Tooltip("Ranged enemy: fire Bulie3 from Firepoint")]
    [SerializeField] private bool ranged;
    [Tooltip("Drag Firepoint child here for ranged")]
    [SerializeField] private Transform firePoint;
    [Tooltip("Drag Canvas/Hpbg/Image (Filled) here")]
    [SerializeField] private Image hpFill;

    private Vector3 _fillBaseScale;
    private Vector3 _fillBasePos;

    private void Awake()
    {
        if (barFill != null)
        {
            _fillBaseScale = barFill.transform.localScale;
            _fillBasePos = barFill.transform.localPosition;
        }

        // 未拖引用时尝试自动绑定 Hpbg 下的填充 Image
        if (hpFill == null)
        {
            Transform canvas = transform.Find("Canvas");
            Transform hpbg = canvas != null ? canvas.Find("Hpbg") : null;
            Transform fill = hpbg != null ? hpbg.Find("Image") : null;
            if (fill != null)
            {
                hpFill = fill.GetComponent<Image>();
            }
        }
    }

    public bool IsRanged => ranged;

    public Vector3 MuzzleWorldPos()
    {
        if (firePoint != null)
        {
            return firePoint.position;
        }

        return transform.position;
    }

    public float BodyRadius()
    {
        if (bodyScale == null)
        {
            return 0.31f;
        }

        return bodyScale.lossyScale.x * 0.5f;
    }

    public void SetBodyScale(float size)
    {
        if (bodyScale != null)
        {
            bodyScale.localScale = new Vector3(size, size, 1f);
        }
    }

    public void SetHealth01(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
        }

        if (barFill == null)
        {
            return;
        }

        Vector3 scale = _fillBaseScale;
        scale.x = Mathf.Max(0.001f, _fillBaseScale.x * ratio);
        barFill.transform.localScale = scale;
        Vector3 pos = _fillBasePos;
        pos.x = _fillBasePos.x + _fillBaseScale.x * (ratio - 1f) * 0.5f;
        barFill.transform.localPosition = pos;
    }
}
