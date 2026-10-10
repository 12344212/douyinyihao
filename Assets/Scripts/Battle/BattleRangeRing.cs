using UnityEngine;

/// <summary>
/// 预制体上的范围圈。外径跟随半径；厚度/颜色/透明度/排序在 Inspector 调，不随半径变粗。
/// </summary>
public class BattleRangeRing : MonoBehaviour
{
    private const int DefaultSegments = 72;

    [Header("Ring Look (edit on prefab)")]
    [Tooltip("描边厚度（世界单位），不随半径放大")]
    [SerializeField] private float strokeWidth = 0.1f;
    [Tooltip("圈颜色，含透明度 Alpha")]
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField] private int sortingOrder = 25;
    [SerializeField] private int segments = DefaultSegments;

    [Header("Optional")]
    [Tooltip("旧版精灵圈，仅作引用；运行时禁用，外观以 ringColor/strokeWidth 为准")]
    [SerializeField] private SpriteRenderer ringSprite;

    private LineRenderer _line;
    private Material _lineMaterial;
    private bool _ready;

    private void Awake()
    {
        // 若预制体尚未写 ringColor，则从旧精灵同步一次
        if (ringSprite != null && ringColor.a <= 0.001f)
        {
            ringColor = ringSprite.color;
            sortingOrder = ringSprite.sortingOrder;
        }

        EnsureLine();
        ApplyLook();
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
            _lineMaterial = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_line != null)
        {
            ApplyLook();
        }
    }
#endif

    public void SetRadius(float radius)
    {
        if (radius < 0f)
        {
            radius = 0f;
        }

        EnsureLine();
        ApplyLook();

        // 不再整体缩放 Transform，否则描边会随半径变粗
        transform.localScale = Vector3.one;

        if (ringSprite != null)
        {
            ringSprite.enabled = false;
        }

        if (radius <= 0.001f)
        {
            _line.enabled = false;
            return;
        }

        _line.enabled = true;
        float parent = 1f;
        if (transform.parent != null)
        {
            parent = Mathf.Abs(transform.parent.lossyScale.x);
        }

        if (parent < 0.0001f)
        {
            parent = 1f;
        }

        float localR = radius / parent;
        int count = segments > 8 ? segments : DefaultSegments;
        _line.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float a = (Mathf.PI * 2f) * i / count;
            _line.SetPosition(i, new Vector3(Mathf.Cos(a) * localR, Mathf.Sin(a) * localR, 0f));
        }
    }

    private void ApplyLook()
    {
        if (_line == null)
        {
            return;
        }

        float width = Mathf.Max(0.01f, strokeWidth);
        _line.startWidth = width;
        _line.endWidth = width;
        _line.startColor = ringColor;
        _line.endColor = ringColor;
        _line.sortingOrder = sortingOrder;
        if (_lineMaterial != null)
        {
            _lineMaterial.color = ringColor;
        }
    }

    private void EnsureLine()
    {
        if (_ready && _line != null)
        {
            return;
        }

        if (ringSprite != null)
        {
            ringSprite.enabled = false;
        }

        _line = GetComponent<LineRenderer>();
        if (_line == null)
        {
            _line = gameObject.AddComponent<LineRenderer>();
        }

        if (_lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _lineMaterial = new Material(shader);
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        _line.sharedMaterial = _lineMaterial;
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.textureMode = LineTextureMode.Stretch;
        _line.alignment = LineAlignment.View;
        _line.numCapVertices = 2;
        _line.numCornerVertices = 2;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.allowOcclusionWhenDynamic = false;
        int count = segments > 8 ? segments : DefaultSegments;
        _line.positionCount = count;
        _ready = true;
        ApplyLook();
    }
}