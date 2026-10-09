using UnityEngine;

/// <summary>
/// 木板外观。血条和塔挂点在预制体上拖好，不在代码里创建。
/// </summary>
public class PlankView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Transform bodyScale;
    [SerializeField] private SpriteRenderer barFill;
    [SerializeField] private Transform towerAnchor;
    [SerializeField] private int cellX;
    [SerializeField] private int cellY;

    private Vector3 _fillBaseScale;
    private Vector3 _fillBasePos;
    private Color _baseColor = Color.white;

    public int CellX => cellX;
    public int CellY => cellY;
    public Transform TowerAnchor => towerAnchor;
    public Color BaseColor => _baseColor;

    private void Awake()
    {
        if (barFill != null)
        {
            _fillBaseScale = barFill.transform.localScale;
            _fillBasePos = barFill.transform.localPosition;
        }
    }

    public void SetCell(int x, int y)
    {
        cellX = x;
        cellY = y;
    }

    public void Paint(Color plankColor)
    {
        float shade = 0.88f + (Mathf.Abs(cellX * 3 + cellY * 5) % 4) * 0.04f;
        _baseColor = plankColor * shade;
        _baseColor.a = 1f;
        SetHealth01(1f);
    }

    public float BodyRadius()
    {
        if (body != null)
        {
            Vector3 ext = body.bounds.extents;
            return Mathf.Max(ext.x, ext.y);
        }

        if (bodyScale != null)
        {
            return bodyScale.lossyScale.x * 0.5f;
        }

        return 0.81f;
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
        if (body != null)
        {
            Color dark = _baseColor * 0.35f;
            dark.a = 1f;
            body.color = Color.Lerp(dark, _baseColor, ratio);
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

    /// <summary>建造模式幽灵木板：半透明、隐藏血条、提高排序。</summary>
    public void ApplyGhostLook()
    {
        EnsureSprites();
        for (int i = 0; i < _sprites.Length; i++)
        {
            SpriteRenderer sr = _sprites[i];
            if (sr == null)
            {
                continue;
            }

            if (sr == body)
            {
                Color c = _baseColor;
                c.a = 0.4f;
                sr.color = c;
                sr.sortingOrder = 45;
            }
            else
            {
                sr.enabled = false;
            }
        }
    }

    /// <summary>建造模式抬高空木板排序，使其压在遮罩之上。</summary>
    public void BoostSorting(int baseOrder)
    {
        EnsureSprites();
        if (_savedOrders == null || _savedOrders.Length != _sprites.Length)
        {
            _savedOrders = new int[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
            {
                _savedOrders[i] = _sprites[i] != null ? _sprites[i].sortingOrder : 0;
            }
        }

        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] != null)
            {
                _sprites[i].sortingOrder = baseOrder + i;
            }
        }
    }

    public void RestoreSorting()
    {
        if (_sprites == null || _savedOrders == null)
        {
            return;
        }

        for (int i = 0; i < _sprites.Length && i < _savedOrders.Length; i++)
        {
            if (_sprites[i] != null)
            {
                _sprites[i].sortingOrder = _savedOrders[i];
            }
        }

        _savedOrders = null;
    }

    private void EnsureSprites()
    {
        if (_sprites == null || _sprites.Length == 0)
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private SpriteRenderer[] _sprites;
    private int[] _savedOrders;
}