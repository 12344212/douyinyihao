using UnityEngine;

/// <summary>
/// Looping BGM player. Hang on LoadScene root BgmRuntime; DontDestroyOnLoad singleton so music
/// continues through MainScene / BattleScene. Clip is assigned by BgmSceneSetup.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class BgmManager : MonoBehaviour
{
    public static BgmManager Instance { get; private set; }

    [SerializeField] private AudioClip bgmClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.7f;

    private AudioSource _source;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        if (_source == null)
        {
            _source = gameObject.AddComponent<AudioSource>();
        }

        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.volume = Mathf.Clamp01(volume);

        if (bgmClip == null)
        {
            Debug.LogWarning("[BgmManager] BGM clip is not assigned.");
            return;
        }

        _source.clip = bgmClip;
        if (!_source.isPlaying)
        {
            _source.Play();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (_source != null)
        {
            _source.volume = volume;
        }
    }
}