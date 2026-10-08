using UnityEngine;

/// <summary>
/// 螃蟹外观。血条在预制体里。
/// </summary>
public class BattleCrab : MonoBehaviour
{
    [SerializeField] private Transform bodyScale;
    [SerializeField] private SpriteRenderer barFill;

    private Vector3 _fillBaseScale;
    private Vector3 _fillBasePos;

    private void Awake()
    {
        if (barFill != null)
        {
            _fillBaseScale = barFill.transform.localScale;
            _fillBasePos = barFill.transform.localPosition;
        }
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