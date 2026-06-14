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

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
            ApplyMusicSetting();
        }
        else
        {
            Destroy(gameObject);
            return;
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
    }
    public void ToggleSound()
    {
        IsSoundOn = !IsSoundOn;
        PlayerPrefs.SetInt(SoundKey, IsSoundOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    public void ToggleHighlight()
    {
        IsHighlightOn = !IsHighlightOn;
        PlayerPrefs.SetInt(HighlightKey, IsHighlightOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void ApplyMusicSetting()
    {
        AudioListener.volume = IsMusicOn ? 1f : 0f;
    }
}
