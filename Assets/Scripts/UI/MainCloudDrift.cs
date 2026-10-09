using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主场景云朵：统一风向，远/中/近三层；池 10 朵。
/// 初次进入场景时先在屏内随机出现，再沿风向飞出并循环。
/// </summary>
public class MainCloudDrift : MonoBehaviour
{
    private enum CloudLayerKind
    {
        Far = 0,
        Mid = 1,
        Near = 2
    }

    [System.Serializable]
    private class LayerSettings
    {
        public CloudLayerKind kind = CloudLayerKind.Mid;
        [Tooltip("相对权重，越大越容易抽到该层")]
        public float spawnWeight = 1f;
        public Vector2 scaleRange = new Vector2(0.7f, 1f);
        public Vector2 alphaRange = new Vector2(0.5f, 0.9f);
        public Vector2 speedRange = new Vector2(80f, 140f);
        public Vector2 bobAmplitudeRange = new Vector2(8f, 20f);
        public Vector2 bobPeriodRange = new Vector2(2.5f, 4.5f);
        public Vector2 tiltAngleRange = new Vector2(1.5f, 5f);
        public Vector2 tiltPeriodRange = new Vector2(2.8f, 5f);
    }

    [Header("Refs")]
    [SerializeField] private RectTransform cloudRoot;
    [SerializeField] private Image[] cloudPool;
    [SerializeField] private Sprite[] cloudSprites;

    [Header("Count")]
    [SerializeField] private int minActive = 4;
    [SerializeField] private int maxActive = 10;

    [Header("Wind")]
    [Tooltip("勾选：从左飞到右；取消：从右飞到左。全体统一风向。")]
    [SerializeField] private bool flyLeftToRight = true;
    [SerializeField] private Vector2 spawnDelayRange = new Vector2(0.5f, 3.2f);
    [SerializeField] private Vector2 restDelayRange = new Vector2(0.8f, 4f);
    [SerializeField] private float edgePadding = 40f;
    [SerializeField] private float topPadding = 30f;

    [Header("First Enter")]
    [Tooltip("第一次进场景时，先在屏幕内随机铺开再开始飞。")]
    [SerializeField] private bool spawnOnScreenFirstEnter = true;
    [SerializeField] private int firstEnterCount = 10;

    [Header("Layers (Far / Mid / Near)")]
    [SerializeField] private LayerSettings farLayer = new LayerSettings
    {
        kind = CloudLayerKind.Far,
        spawnWeight = 1.1f,
        scaleRange = new Vector2(0.45f, 0.65f),
        alphaRange = new Vector2(0.28f, 0.48f),
        speedRange = new Vector2(28f, 48f),
        bobAmplitudeRange = new Vector2(1f, 4f),
        bobPeriodRange = new Vector2(4f, 7f),
        tiltAngleRange = new Vector2(0.4f, 1.5f),
        tiltPeriodRange = new Vector2(4.5f, 7.5f)
    };

    [SerializeField] private LayerSettings midLayer = new LayerSettings
    {
        kind = CloudLayerKind.Mid,
        spawnWeight = 1.3f,
        scaleRange = new Vector2(0.7f, 0.95f),
        alphaRange = new Vector2(0.5f, 0.75f),
        speedRange = new Vector2(70f, 110f),
        bobAmplitudeRange = new Vector2(6f, 14f),
        bobPeriodRange = new Vector2(2.6f, 4.2f),
        tiltAngleRange = new Vector2(1.5f, 4f),
        tiltPeriodRange = new Vector2(2.8f, 4.8f)
    };

    [SerializeField] private LayerSettings nearLayer = new LayerSettings
    {
        kind = CloudLayerKind.Near,
        spawnWeight = 0.9f,
        scaleRange = new Vector2(1.0f, 1.25f),
        alphaRange = new Vector2(0.78f, 0.95f),
        speedRange = new Vector2(120f, 170f),
        bobAmplitudeRange = new Vector2(14f, 28f),
        bobPeriodRange = new Vector2(2.0f, 3.4f),
        tiltAngleRange = new Vector2(3f, 7f),
        tiltPeriodRange = new Vector2(2.2f, 3.8f)
    };

    private Coroutine[] _loops;
    private bool[] _flying;
    private bool[] _useOnScreenStart;
    private int _flyingCount;

    private void OnEnable()
    {
        if (!Validate())
        {
            return;
        }

        PreparePool();
        _flying = new bool[cloudPool.Length];
        _useOnScreenStart = new bool[cloudPool.Length];
        _loops = new Coroutine[cloudPool.Length];

        int firstCount = spawnOnScreenFirstEnter
            ? Mathf.Clamp(firstEnterCount, 1, cloudPool.Length)
            : Mathf.Clamp(Random.Range(minActive, maxActive + 1), 1, cloudPool.Length);

        for (int i = 0; i < cloudPool.Length; i++)
        {
            bool firstWave = spawnOnScreenFirstEnter && i < firstCount;
            _useOnScreenStart[i] = firstWave;
            float initialDelay = firstWave
                ? Random.Range(0f, 0.35f)
                : Random.Range(spawnDelayRange.x, spawnDelayRange.y * 1.5f);
            _loops[i] = StartCoroutine(CloudLoop(i, initialDelay));
        }
    }

    private void OnDisable()
    {
        if (_loops == null)
        {
            return;
        }

        for (int i = 0; i < _loops.Length; i++)
        {
            if (_loops[i] != null)
            {
                StopCoroutine(_loops[i]);
            }

            if (cloudPool != null && i < cloudPool.Length && cloudPool[i] != null)
            {
                KillCloudTweens(cloudPool[i]);
                cloudPool[i].gameObject.SetActive(false);
            }
        }

        _flyingCount = 0;
    }

    private bool Validate()
    {
        if (cloudRoot == null || cloudPool == null || cloudPool.Length == 0)
        {
            Debug.LogError("[MainCloudDrift] Assign cloudRoot and cloudPool in Inspector.");
            return false;
        }

        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            Debug.LogError("[MainCloudDrift] Assign cloudSprites (zjm-y1~y3).");
            return false;
        }

        maxActive = Mathf.Clamp(maxActive, 1, cloudPool.Length);
        minActive = Mathf.Clamp(minActive, 1, maxActive);
        firstEnterCount = Mathf.Clamp(firstEnterCount, 1, cloudPool.Length);
        return true;
    }

    private void PreparePool()
    {
        for (int i = 0; i < cloudPool.Length; i++)
        {
            Image img = cloudPool[i];
            if (img == null)
            {
                continue;
            }

            img.raycastTarget = false;
            img.preserveAspect = true;
            img.gameObject.SetActive(false);
        }
    }

    private LayerSettings PickLayer()
    {
        float w0 = Mathf.Max(0.01f, farLayer.spawnWeight);
        float w1 = Mathf.Max(0.01f, midLayer.spawnWeight);
        float w2 = Mathf.Max(0.01f, nearLayer.spawnWeight);
        float total = w0 + w1 + w2;
        float r = Random.value * total;
        if (r < w0)
        {
            return farLayer;
        }

        if (r < w0 + w1)
        {
            return midLayer;
        }

        return nearLayer;
    }

    private IEnumerator CloudLoop(int index, float initialDelay)
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        Image img = cloudPool[index];
        RectTransform rt = img.rectTransform;

        // 等 Canvas 算出 CloudLayer 尺寸，避免半宽为 0 时云飞出屏外立刻结束。
        while (enabled && cloudRoot != null && cloudRoot.rect.width < 8f)
        {
            yield return null;
        }

        while (enabled)
        {
            while (_flyingCount >= maxActive)
            {
                yield return new WaitForSeconds(0.25f);
            }

            bool onScreenStart = _useOnScreenStart != null && _useOnScreenStart[index];
            if (!onScreenStart)
            {
                float spawnWait = Random.Range(spawnDelayRange.x, spawnDelayRange.y);
                if (_flyingCount < minActive)
                {
                    spawnWait *= 0.35f;
                }

                if (spawnWait > 0f)
                {
                    yield return new WaitForSeconds(spawnWait);
                }
            }

            while (_flyingCount >= maxActive)
            {
                yield return new WaitForSeconds(0.2f);
            }

            LayerSettings layer = PickLayer();

            Sprite sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
            img.sprite = sprite;
            img.SetNativeSize();

            float scale = Random.Range(layer.scaleRange.x, layer.scaleRange.y);
            rt.localScale = Vector3.one * scale;

            float alpha = Random.Range(layer.alphaRange.x, layer.alphaRange.y);
            Color color = img.color;
            color.a = alpha;
            img.color = color;

            int sibling = layer.kind == CloudLayerKind.Far ? 0
                : layer.kind == CloudLayerKind.Mid ? cloudRoot.childCount / 2
                : cloudRoot.childCount - 1;
            rt.SetSiblingIndex(Mathf.Clamp(sibling, 0, cloudRoot.childCount - 1));

            float halfW = cloudRoot.rect.width * 0.5f;
            float halfH = cloudRoot.rect.height * 0.5f;
            float cloudHalfW = rt.rect.width * scale * 0.5f;
            float cloudHalfH = rt.rect.height * scale * 0.5f;

            float yMin = layer.kind == CloudLayerKind.Near ? 0f
                : layer.kind == CloudLayerKind.Mid ? halfH * 0.15f
                : halfH * 0.35f;
            float yMax = Mathf.Max(yMin, halfH - cloudHalfH - topPadding);
            float baseY = Random.Range(yMin, yMax);

            float endX = flyLeftToRight
                ? halfW + cloudHalfW + edgePadding
                : -halfW - cloudHalfW - edgePadding;

            float startX;
            if (onScreenStart)
            {
                // 初次进入：直接出现在屏幕内随机位置
                float xMin = -halfW + cloudHalfW + edgePadding * 0.25f;
                float xMax = halfW - cloudHalfW - edgePadding * 0.25f;
                if (xMax < xMin)
                {
                    xMin = -halfW * 0.4f;
                    xMax = halfW * 0.4f;
                }

                startX = Random.Range(xMin, xMax);
                _useOnScreenStart[index] = false;
            }
            else
            {
                startX = flyLeftToRight
                    ? -halfW - cloudHalfW - edgePadding
                    : halfW + cloudHalfW + edgePadding;
            }

            rt.anchoredPosition = new Vector2(startX, baseY);
            rt.localEulerAngles = Vector3.zero;
            img.gameObject.SetActive(true);

            _flying[index] = true;
            _flyingCount++;

            float speed = Random.Range(layer.speedRange.x, layer.speedRange.y);
            float distance = Mathf.Abs(endX - startX);
            float duration = Mathf.Max(0.8f, distance / Mathf.Max(1f, speed));

            float bobAmp = Random.Range(layer.bobAmplitudeRange.x, layer.bobAmplitudeRange.y);
            float bobPeriod = Random.Range(layer.bobPeriodRange.x, layer.bobPeriodRange.y);
            float bobPhase = Random.Range(0f, Mathf.PI * 2f);
            float bobOmega = (Mathf.PI * 2f) / Mathf.Max(0.2f, bobPeriod);

            float tilt = Random.Range(layer.tiltAngleRange.x, layer.tiltAngleRange.y);
            if (Random.value > 0.5f)
            {
                tilt = -tilt;
            }

            float tiltPeriod = Random.Range(layer.tiltPeriodRange.x, layer.tiltPeriodRange.y);

            KillCloudTweens(img);

            bool finished = false;
            float x = startX;

            DOTween.To(() => x, v =>
                {
                    x = v;
                    float bob = Mathf.Sin(Time.unscaledTime * bobOmega + bobPhase) * bobAmp;
                    rt.anchoredPosition = new Vector2(x, baseY + bob);
                }, endX, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetTarget(rt)
                .OnComplete(() => { finished = true; });

            if (Mathf.Abs(tilt) > 0.05f)
            {
                rt.DOLocalRotate(new Vector3(0f, 0f, tilt), tiltPeriod * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetTarget(rt);
            }

            while (!finished)
            {
                yield return null;
            }

            KillCloudTweens(img);
            rt.localEulerAngles = Vector3.zero;
            img.gameObject.SetActive(false);
            _flying[index] = false;
            _flyingCount = Mathf.Max(0, _flyingCount - 1);

            float rest = Random.Range(restDelayRange.x, restDelayRange.y);
            yield return new WaitForSeconds(rest);
        }
    }

    private static void KillCloudTweens(Image img)
    {
        if (img == null)
        {
            return;
        }

        img.rectTransform.DOKill();
        img.DOKill();
    }
}
