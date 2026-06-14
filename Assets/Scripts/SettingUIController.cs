using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Controller cho bảng Setting.
/// Chỉ có class này được phép gọi SettingManager.Toggle*().
/// Sau đó nó sync lại state cho HighLightButton và GameController.
/// </summary>
public class SettingUIController : MonoBehaviour
{
    [Header("Toggle Buttons")]
    [SerializeField] private Button MusicToggle;
    [SerializeField] private Button SoundToggle;
    [SerializeField] private Button HighlightToggle;

    [Header("Visual Indicators — GameObject (tùy chọn)")]
    [SerializeField] private GameObject musicOnVisual;
    [SerializeField] private GameObject musicOffVisual;
    [SerializeField] private GameObject soundOnVisual;
    [SerializeField] private GameObject soundOffVisual;
    [SerializeField] private GameObject highlightOnVisual;
    [SerializeField] private GameObject highlightOffVisual;

    [Header("Visual Indicators — Text (tùy chọn)")]
    [SerializeField] private TMP_Text musicText;
    [SerializeField] private TMP_Text soundText;
    [SerializeField] private TMP_Text highlightText;

    [Header("Visual Indicators — Sprite Swap cho từng button (tùy chọn)")]
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    [SerializeField] private Sprite highlightOnSprite;
    [SerializeField] private Sprite highlightOffSprite;

    private void Start()
    {
        if (MusicToggle != null) MusicToggle.onClick.AddListener(OnMusicClick);
        if (SoundToggle != null) SoundToggle.onClick.AddListener(OnSoundClick);
        if (HighlightToggle != null) HighlightToggle.onClick.AddListener(OnHighlightClick);

        UpdateUIState();
    }

    private void OnEnable()
    {
        // Mỗi lần bảng mở ra, đồng bộ UI
        UpdateUIState();
    }

    // ──────────── Click Handlers ────────────

    private void OnMusicClick()
    {
        if (SettingManager.Instance == null) return;
        SettingManager.Instance.ToggleMusic();
        UpdateUIState();
    }

    private void OnSoundClick()
    {
        if (SettingManager.Instance == null) return;
        SettingManager.Instance.ToggleSound();
        UpdateUIState();
    }

    private void OnHighlightClick()
    {
        if (SettingManager.Instance == null) return;

        // 1. Toggle trong SettingManager (nguồn sự thật duy nhất)
        SettingManager.Instance.ToggleHighlight();
        bool newValue = SettingManager.Instance.IsHighlightOn;

        // 2. Cập nhật UI bảng setting
        UpdateUIState();

        // 3. Đẩy giá trị mới xuống scene (GameController + HighLightButton ngoài màn)
        SyncHighlightToScene(newValue);
    }

    // ──────────── UI Sync ────────────

    public void UpdateUIState()
    {
        if (SettingManager.Instance == null) return;

        UpdateToggleVisual(MusicToggle,
            musicOnVisual, musicOffVisual,
            musicText, "Music",
            musicOnSprite, musicOffSprite,
            SettingManager.Instance.IsMusicOn);

        UpdateToggleVisual(SoundToggle,
            soundOnVisual, soundOffVisual,
            soundText, "Sound",
            soundOnSprite, soundOffSprite,
            SettingManager.Instance.IsSoundOn);

        UpdateToggleVisual(HighlightToggle,
            highlightOnVisual, highlightOffVisual,
            highlightText, "Highlight",
            highlightOnSprite, highlightOffSprite,
            SettingManager.Instance.IsHighlightOn);
    }

    private void UpdateToggleVisual(
        Button btn,
        GameObject onVisual, GameObject offVisual,
        TMP_Text textComp, string label,
        Sprite onSprite, Sprite offSprite,
        bool isOn)
    {
        if (onVisual != null)  onVisual.SetActive(isOn);
        if (offVisual != null) offVisual.SetActive(!isOn);

        if (textComp != null)
            textComp.text = $"{label}: {(isOn ? "ON" : "OFF")}";

        if (btn != null && onSprite != null && offSprite != null)
        {
            Image img = btn.GetComponent<Image>();
            if (img != null) img.sprite = isOn ? onSprite : offSprite;
        }
    }

    // ──────────── Scene Sync ────────────

    private void SyncHighlightToScene(bool value)
    {
        // Cập nhật GameController (ẩn/hiện ô gợi ý)
        IGameController gc = Object.FindAnyObjectByType<GameController>();
        if (gc == null) gc = Object.FindAnyObjectByType<RandomGameController>();
        if (gc == null) gc = Object.FindAnyObjectByType<TutorialGameController>();
        gc?.SetHighlight(value);

        // Cập nhật sprite nút mắt trên màn chơi (nếu có)
        HighLightButton hBtn = Object.FindAnyObjectByType<HighLightButton>();
        hBtn?.SetHighLight(value);
    }
}
