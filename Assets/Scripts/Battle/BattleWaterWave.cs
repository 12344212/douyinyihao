using UnityEngine;

/// <summary>
/// Battle water background: subtle UV ripple via Battle/WaterWave shader.
/// Mesh scale stays fixed (prefab scale 400) so camera never sees blank edges.
/// Texture Clamp + small wave amp keeps samples on water pixels.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class BattleWaterWave : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Material waveMaterial;

    [Header("Wave (mild)")]
    [SerializeField] [Range(0f, 0.05f)] private float waveAmp = 0.012f;
    [SerializeField] private float waveFreq = 5.5f;
    [SerializeField] private float waveSpeed = 1.1f;
    [SerializeField] [Range(0f, 0.05f)] private float waveAmp2 = 0.007f;
    [SerializeField] private float waveFreq2 = 3.2f;
    [SerializeField] private float waveSpeed2 = 0.75f;

    private static readonly int WaveAmpId = Shader.PropertyToID("_WaveAmp");
    private static readonly int WaveFreqId = Shader.PropertyToID("_WaveFreq");
    private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveAmp2Id = Shader.PropertyToID("_WaveAmp2");
    private static readonly int WaveFreq2Id = Shader.PropertyToID("_WaveFreq2");
    private static readonly int WaveSpeed2Id = Shader.PropertyToID("_WaveSpeed2");

    private MaterialPropertyBlock _block;
    private bool _ready;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<SpriteRenderer>();
        }

        EnsureMaterial();
        ApplyWaveParams();
    }

    private void OnEnable()
    {
        ApplyWaveParams();
    }

    private void OnValidate()
    {
        if (target == null)
        {
            target = GetComponent<SpriteRenderer>();
        }

        ApplyWaveParams();
    }

    private void EnsureMaterial()
    {
        if (target == null)
        {
            return;
        }

        if (waveMaterial == null)
        {
            Shader shader = Shader.Find("Battle/WaterWave");
            if (shader == null)
            {
                Debug.LogWarning("[BattleWaterWave] Shader Battle/WaterWave not found. Assign waveMaterial.");
                return;
            }

            waveMaterial = new Material(shader);
            waveMaterial.name = "BattleWaterWave (Runtime)";
        }

        // Shared material is fine: params go through MPB; sprite injects _MainTex.
        if (target.sharedMaterial != waveMaterial)
        {
            target.sharedMaterial = waveMaterial;
        }

        _ready = true;
    }

    private void ApplyWaveParams()
    {
        if (target == null)
        {
            return;
        }

        if (!_ready)
        {
            EnsureMaterial();
        }

        if (target.sharedMaterial == null)
        {
            return;
        }

        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }

        target.GetPropertyBlock(_block);
        _block.SetFloat(WaveAmpId, waveAmp);
        _block.SetFloat(WaveFreqId, waveFreq);
        _block.SetFloat(WaveSpeedId, waveSpeed);
        _block.SetFloat(WaveAmp2Id, waveAmp2);
        _block.SetFloat(WaveFreq2Id, waveFreq2);
        _block.SetFloat(WaveSpeed2Id, waveSpeed2);
        target.SetPropertyBlock(_block);
    }
}