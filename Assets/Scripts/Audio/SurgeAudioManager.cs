// ============================================================================
// SURGE — SurgeAudioManager
//
// Drives the audio landscape for Surge:
//   - Dynamic synthwave music track that smoothly scales tempo/pitch with combo multiplier
//   - Ascending musical semitone ticks on path connection (the arcade "Peggle" feel)
//   - Punchy SFX for clears, combos, Surge mode activation, and Color Purge
// ============================================================================

using UnityEngine;

[DisallowMultipleComponent]
public sealed class SurgeAudioManager : MonoBehaviour
{
    public static SurgeAudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioClip musicTrack;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.65f;
    [SerializeField] private float basePitch = 1.0f;
    [SerializeField] private float maxPitch = 1.25f;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip nodeTickClip;
    [SerializeField] private AudioClip rejectClip;
    [SerializeField] private AudioClip comboClip;
    [SerializeField] private AudioClip heavyClearClip;
    [SerializeField] private AudioClip surgeActiveClip;
    [SerializeField] private AudioClip purgeClip;

    [Header("Volumes")]
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 0.85f;

    private AudioSource _musicSource;
    private AudioSource _sfxSource;
    private float _targetPitch = 1.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupAudioSources();
        LoadClipsIfUnset();
    }

    private void Start()
    {
        StartMusic();
    }

    private void Update()
    {
        // Smoothly lerp music pitch towards target combo pitch
        if (_musicSource != null && _musicSource.isPlaying)
        {
            _musicSource.pitch = Mathf.MoveTowards(_musicSource.pitch, _targetPitch, 0.4f * Time.unscaledDeltaTime);
        }
    }

    private void SetupAudioSources()
    {
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.volume = musicVolume;
        _musicSource.pitch = basePitch;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
        _sfxSource.volume = sfxVolume;
    }

    private void LoadClipsIfUnset()
    {
#if UNITY_EDITOR
        if (musicTrack == null)
            musicTrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/music_synthwave_loop.ogg");
        if (nodeTickClip == null)
            nodeTickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_node_tick.wav");
        if (rejectClip == null)
            rejectClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_reject.wav");
        if (comboClip == null)
            comboClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_combo.wav");
        if (heavyClearClip == null)
            heavyClearClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_heavy_clear.wav");
        if (surgeActiveClip == null)
            surgeActiveClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_surge_active.wav");
        if (purgeClip == null)
            purgeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_purge.wav");
#endif
    }

    public void StartMusic()
    {
        if (_musicSource == null || musicTrack == null) return;
        _musicSource.clip = musicTrack;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        if (_musicSource != null) _musicSource.Stop();
    }

    /// <summary>
    /// Updates music pitch dynamically according to current combo chain (1x .. 10x).
    /// </summary>
    public void SetComboMultiplier(int chainMult)
    {
        float t = Mathf.Clamp01((chainMult - 1) / 9f);
        _targetPitch = Mathf.Lerp(basePitch, maxPitch, t);
    }

    /// <summary>
    /// Plays an ascending semitone click as each node is connected in the path.
    /// </summary>
    public void PlayNodeTick(int stepIndex)
    {
        if (_sfxSource == null || nodeTickClip == null) return;
        // Ascend by half-steps up to 1 octave
        float pitch = Mathf.Pow(1.059463f, Mathf.Clamp(stepIndex, 0, 12));
        _sfxSource.pitch = pitch;
        _sfxSource.PlayOneShot(nodeTickClip, sfxVolume);
        _sfxSource.pitch = 1.0f;
    }

    public void PlayClear(int nodeCount, bool isPurge)
    {
        if (_sfxSource == null) return;

        if (isPurge && purgeClip != null)
        {
            _sfxSource.PlayOneShot(purgeClip, sfxVolume);
        }
        else if (nodeCount >= 5 && heavyClearClip != null)
        {
            _sfxSource.PlayOneShot(heavyClearClip, sfxVolume);
        }
        else if (nodeTickClip != null)
        {
            _sfxSource.PlayOneShot(nodeTickClip, sfxVolume * 0.9f);
        }
    }

    public void PlayCombo(int chainMult)
    {
        if (_sfxSource == null || comboClip == null) return;
        float pitch = 1.0f + Mathf.Clamp01((chainMult - 1) / 10f) * 0.3f;
        _sfxSource.pitch = pitch;
        _sfxSource.PlayOneShot(comboClip, sfxVolume);
        _sfxSource.pitch = 1.0f;
    }

    public void PlaySurgeActivation()
    {
        if (_sfxSource == null || surgeActiveClip == null) return;
        _sfxSource.PlayOneShot(surgeActiveClip, sfxVolume * 1.1f);
    }

    public void PlayReject()
    {
        if (_sfxSource == null || rejectClip == null) return;
        _sfxSource.PlayOneShot(rejectClip, sfxVolume * 0.7f);
    }
}
