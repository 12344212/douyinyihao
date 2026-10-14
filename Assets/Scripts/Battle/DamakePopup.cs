using TMPro;
using UnityEngine;

/// <summary>
/// 伤害飘字：显示数值、按伤害类型上色，向上飘并淡出后销毁。
/// Damake 预制体无 Canvas 时运行时补 World Space Canvas。
/// </summary>
[DisallowMultipleComponent]
public class DamakePopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float lifetime = 0.85f;
    [SerializeField] private float riseSpeed = 1.35f;
    [SerializeField] private float startScale = 0.85f;
    [SerializeField] private float peakScale = 1.15f;

    private float _age;
    private Color _color = Color.white;
    private bool _playing;
    private Canvas _canvas;

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponent<TextMeshProUGUI>();
        }

        EnsureWorldCanvas();
    }

    private void EnsureWorldCanvas()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
        {
            return;
        }

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 60;

        RectTransform rt = transform as RectTransform;
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(5.4f, 1.2f);
        }
    }

    /// <summary>显示伤害数字并开始飘字动画。</summary>
    public void Play(float amount, Color color)
    {
        _color = color;
        _age = 0f;
        _playing = true;

        if (label == null)
        {
            label = GetComponent<TextMeshProUGUI>();
        }

        if (label != null)
        {
            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
            label.text = shown.ToString();
            label.color = color;
            label.raycastTarget = false;
        }

        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        if (!_playing)
        {
            return;
        }

        _age += Time.deltaTime;
        float t = lifetime <= 0.01f ? 1f : Mathf.Clamp01(_age / lifetime);

        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        float scale;
        if (t < 0.2f)
        {
            scale = Mathf.Lerp(startScale, peakScale, t / 0.2f);
        }
        else
        {
            scale = Mathf.Lerp(peakScale, 1f, (t - 0.2f) / 0.8f);
        }

        transform.localScale = Vector3.one * scale;

        if (label != null)
        {
            Color c = _color;
            c.a = 1f - t;
            label.color = c;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
