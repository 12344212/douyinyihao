using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 海面高光对象池：稀疏随机淡入淡出，互不同步。
/// 通过 SpriteRenderer.color.a 控制透明 PNG；池大小固定，不会无限增长。
/// 支持 gaoliang 多切片：每次出现随机选一张。
/// </summary>
[DisallowMultipleComponent]
public class OceanHighlightController : MonoBehaviour
{
    [Header("资源")]
    [SerializeField] private Sprite[] highlightSprites;
    [SerializeField] private Sprite highlightSprite;
    [SerializeField] private Transform poolRoot;

    [Header("并发与外观")]
    [SerializeField] private int maxConcurrent = 3;
    [SerializeField] private int poolCapacity = 6;
    [SerializeField] private int sortingOrder = 3;
    [SerializeField] private Vector2 targetAlphaRange = new Vector2(0.35f, 0.8f);
    [SerializeField] private Vector2 scaleRange = new Vector2(0.18f, 0.35f);

    [Header("生命周期 (秒)")]
    [SerializeField] private Vector2 fadeInRange = new Vector2(0.2f, 0.4f);
    [SerializeField] private Vector2 holdRange = new Vector2(0.1f, 0.4f);
    [SerializeField] private Vector2 fadeOutRange = new Vector2(0.4f, 0.8f);
    [SerializeField] private Vector2 waitBetweenRange = new Vector2(0.4f, 2.2f);

    [Header("活动区域（世界单位）")]
    [SerializeField] private Vector2 areaMin = new Vector2(-10f, -6f);
    [SerializeField] private Vector2 areaMax = new Vector2(10f, 6f);

    private readonly Queue<SpriteRenderer> _pool = new Queue<SpriteRenderer>();
    private readonly HashSet<SpriteRenderer> _inPool = new HashSet<SpriteRenderer>();
    private readonly List<Coroutine> _runners = new List<Coroutine>();
    private bool _running;
    private int _activeCount;

    public void Configure(Sprite[] sprites, Vector2 min, Vector2 max, int order, int concurrent = -1)
    {
        highlightSprites = sprites;
        if (sprites != null && sprites.Length > 0)
        {
            highlightSprite = sprites[0];
        }

        areaMin = min;
        areaMax = max;
        sortingOrder = order;
        if (concurrent > 0)
        {
            maxConcurrent = concurrent;
        }

        if (poolRoot == null)
        {
            poolRoot = transform;
        }
    }

    /// <summary>兼容旧单 Sprite 调用。</summary>
    public void Configure(Sprite sprite, Vector2 min, Vector2 max, int order, int concurrent = -1)
    {
        if (sprite != null)
        {
            Configure(new[] { sprite }, min, max, order, concurrent);
        }
        else
        {
            Configure(System.Array.Empty<Sprite>(), min, max, order, concurrent);
        }
    }

    private Sprite PickSprite()
    {
        if (highlightSprites != null && highlightSprites.Length > 0)
        {
            Sprite s = highlightSprites[Random.Range(0, highlightSprites.Length)];
            if (s != null)
            {
                return s;
            }
        }

        return highlightSprite;
    }

    private void OnEnable()
    {
        EnsurePool();
        StartRunners();
    }

    private void OnDisable()
    {
        StopAllRunners();
        ReturnAllHidden();
    }

    private void OnDestroy()
    {
        StopAllRunners();
    }

    private void EnsurePool()
    {
        if (poolRoot == null)
        {
            poolRoot = transform;
        }

        int cap = Mathf.Max(maxConcurrent, poolCapacity);
        poolCapacity = cap;

        // 已有子物体计入池，避免重复创建
        if (_pool.Count == 0)
        {
            for (int i = 0; i < poolRoot.childCount; i++)
            {
                SpriteRenderer sr = poolRoot.GetChild(i).GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    HideAndEnqueue(sr);
                }
            }
        }

        while (_pool.Count < cap)
        {
            GameObject go = new GameObject("Highlight");
            go.transform.SetParent(poolRoot, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PickSprite();
            sr.sortingOrder = sortingOrder;
            HideAndEnqueue(sr);
        }
    }

    private void StartRunners()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        int n = Mathf.Max(1, maxConcurrent);
        for (int i = 0; i < n; i++)
        {
            // 错开启动，避免同时闪
            Coroutine c = StartCoroutine(HighlightLoop(Random.Range(0.05f, 1.4f)));
            _runners.Add(c);
        }
    }

    private void StopAllRunners()
    {
        _running = false;
        for (int i = 0; i < _runners.Count; i++)
        {
            if (_runners[i] != null)
            {
                StopCoroutine(_runners[i]);
            }
        }

        _runners.Clear();
        _activeCount = 0;
    }

    private IEnumerator HighlightLoop(float initialDelay)
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (_running && isActiveAndEnabled)
        {
            SpriteRenderer sr = Rent();
            if (sr == null)
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            PlaceRandom(sr);
            float targetA = Random.Range(targetAlphaRange.x, targetAlphaRange.y);
            float fadeIn = Random.Range(fadeInRange.x, fadeInRange.y);
            float hold = Random.Range(holdRange.x, holdRange.y);
            float fadeOut = Random.Range(fadeOutRange.x, fadeOutRange.y);

            yield return FadeAlpha(sr, 0f, targetA, fadeIn);
            if (!_running)
            {
                Return(sr);
                yield break;
            }

            if (hold > 0f)
            {
                yield return new WaitForSeconds(hold);
            }

            if (!_running)
            {
                Return(sr);
                yield break;
            }

            yield return FadeAlpha(sr, targetA, 0f, fadeOut);
            Return(sr);

            float wait = Random.Range(waitBetweenRange.x, waitBetweenRange.y);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }
        }
    }

    private void PlaceRandom(SpriteRenderer sr)
    {
        float x = Random.Range(areaMin.x, areaMax.x);
        float y = Random.Range(areaMin.y, areaMax.y);
        float s = Random.Range(scaleRange.x, scaleRange.y);
        sr.transform.position = new Vector3(x, y, 0f);
        sr.transform.localScale = new Vector3(s, s, 1f);
        sr.transform.localEulerAngles = new Vector3(0f, 0f, Random.Range(-8f, 8f));

        Sprite pick = PickSprite();
        if (pick != null)
        {
            sr.sprite = pick;
        }

        sr.sortingOrder = sortingOrder;
        SetAlpha(sr, 0f);
        sr.enabled = true;
        sr.gameObject.SetActive(true);
    }

    private IEnumerator FadeAlpha(SpriteRenderer sr, float from, float to, float duration)
    {
        if (sr == null)
        {
            yield break;
        }

        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        SetAlpha(sr, from);
        while (t < duration)
        {
            if (!_running || sr == null)
            {
                yield break;
            }

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // 平滑曲线，避免生硬闪烁
            u = u * u * (3f - 2f * u);
            SetAlpha(sr, Mathf.Lerp(from, to, u));
            yield return null;
        }

        SetAlpha(sr, to);
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    private SpriteRenderer Rent()
    {
        EnsurePool();
        if (_pool.Count == 0)
        {
            return null;
        }

        SpriteRenderer sr = _pool.Dequeue();
        _inPool.Remove(sr);
        _activeCount++;
        return sr;
    }

    private void Return(SpriteRenderer sr)
    {
        if (sr == null)
        {
            return;
        }

        HideAndEnqueue(sr);
        _activeCount = Mathf.Max(0, _activeCount - 1);
    }

    private void HideAndEnqueue(SpriteRenderer sr)
    {
        SetAlpha(sr, 0f);
        sr.enabled = false;
        sr.gameObject.SetActive(false);
        if (_inPool.Add(sr))
        {
            _pool.Enqueue(sr);
        }
    }

    private void ReturnAllHidden()
    {
        if (poolRoot == null)
        {
            return;
        }

        for (int i = 0; i < poolRoot.childCount; i++)
        {
            SpriteRenderer sr = poolRoot.GetChild(i).GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                SetAlpha(sr, 0f);
                sr.enabled = false;
                sr.gameObject.SetActive(false);
                if (_inPool.Add(sr))
                {
                    _pool.Enqueue(sr);
                }
            }
        }

        _activeCount = 0;
    }
}
