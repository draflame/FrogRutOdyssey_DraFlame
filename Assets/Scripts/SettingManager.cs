using UnityEngine;

public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    public bool IsMusicOn { get; private set; } = true;
    public bool IsSoundOn { get; private set; } = true;
    public bool IsHighlightOn { get; private set; } = true;

    private const string MusicKey = "Setting_Music";
    private const string SoundKey = "Setting_Sound";
    private const string HighlightKey = "Setting_Highlight";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadSettings();
            ApplyMusicSetting();
            ApplySoundSetting();
            Debug.Log($"[SettingManager] Initialized in Awake. MusicOn: {IsMusicOn}, SoundOn: {IsSoundOn}, HighlightOn: {IsHighlightOn}");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        PlayBGM();
    }

    private void InitializeAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    private void LoadSettings()
    {
        IsMusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
        IsSoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        IsHighlightOn = PlayerPrefs.GetInt(HighlightKey, 1) == 1;
    }

    public void ToggleMusic()
    {
        IsMusicOn = !IsMusicOn;
        PlayerPrefs.SetInt(MusicKey, IsMusicOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusicSetting();
        Debug.Log("[SettingManager] Music toggled. IsMusicOn = " + IsMusicOn + ", mute = " + (bgmSource != null ? bgmSource.mute.ToString() : "null"));
    }

    public void ToggleSound()
    {
        IsSoundOn = !IsSoundOn;
        PlayerPrefs.SetInt(SoundKey, IsSoundOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplySoundSetting();
        Debug.Log("[SettingManager] Sound toggled. IsSoundOn = " + IsSoundOn + ", mute = " + (sfxSource != null ? sfxSource.mute.ToString() : "null"));
    }

    public void ToggleHighlight()
    {
        IsHighlightOn = !IsHighlightOn;
        PlayerPrefs.SetInt(HighlightKey, IsHighlightOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyMusicSetting()
    {
        if (bgmSource != null)
        {
            bgmSource.mute = !IsMusicOn;
        }
    }

    private void ApplySoundSetting()
    {
        if (sfxSource != null)
        {
            sfxSource.mute = !IsSoundOn;
        }
    }

    public void PlayBGM()
    {
        if (bgmSource == null)
        {
            Debug.LogError("[SettingManager] bgmSource is null!");
            return;
        }
        if (bgmClip == null)
        {
            Debug.LogWarning("[SettingManager] bgmClip is null! Please assign game_background_music.mp3 in the inspector of SettingManager GameObject.");
            return;
        }

        bgmSource.loop = true;
        if (bgmSource.clip != bgmClip)
        {
            bgmSource.clip = bgmClip;
            bgmSource.Play();
            Debug.Log("[SettingManager] Playing BGM clip: " + bgmClip.name);
        }
        else if (!bgmSource.isPlaying)
        {
            bgmSource.Play();
            Debug.Log("[SettingManager] Resuming BGM clip: " + bgmClip.name);
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null)
        {
            Debug.LogError("[SettingManager] sfxSource is null!");
            return;
        }
        if (clip == null)
        {
            Debug.LogWarning("[SettingManager] SFX Clip is null! Please assign it in the inspector of SettingManager GameObject.");
            return;
        }
        if (IsSoundOn)
        {
            sfxSource.PlayOneShot(clip);
            Debug.Log("[SettingManager] Playing SFX: " + clip.name);
        }
        else
        {
            Debug.Log("[SettingManager] SFX " + clip.name + " not played because Sound is turned OFF.");
        }
    }

    public void PlayJumpSound()
    {
        PlaySFX(jumpClip);
    }

    public void PlayWinSound()
    {
        PlaySFX(winClip);
    }

    public void PlayLoseSound()
    {
        PlaySFX(loseClip);
    }
}

