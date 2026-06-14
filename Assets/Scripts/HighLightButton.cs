using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Nút toggle Highlight gắn trực tiếp trên màn chơi.
/// Chỉ chịu trách nhiệm: thay đổi sprite + thông báo cho GameController.
/// KHÔNG tự cập nhật SettingManager (để tránh vòng lặp gọi nhau).
/// </summary>
public class HighLightButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isHighLight = true;

    [Header("Sprites (kéo thả 2 ảnh vào đây)")]
    [SerializeField] private Sprite highLightSprite; // ảnh khi BẬT highlight
    [SerializeField] private Sprite normalSprite;    // ảnh khi TẮT highlight

    [Header("Component References")]
    [SerializeField] private Image buttonImage;

    private IGameController _gameController;

    private void Start()
    {
        // Tìm Image trên chính GameObject hoặc các child
        if (buttonImage == null) buttonImage = GetComponentInChildren<Image>();

        _gameController = Object.FindAnyObjectByType<GameController>();
        if (_gameController == null)
            _gameController = Object.FindAnyObjectByType<RandomGameController>();
        if (_gameController == null)
            _gameController = Object.FindAnyObjectByType<TutorialGameController>();

        // Đọc trạng thái từ SettingManager một lần duy nhất khi khởi tạo
        if (SettingManager.Instance != null)
            isHighLight = SettingManager.Instance.IsHighlightOn;

        UpdateVisual();
    }

    /// <summary>
    /// Gọi hàm này từ OnClick của Button trong Unity Inspector.
    /// Toggle trạng thái, đổi sprite, cập nhật GameController.
    /// </summary>
    public void ToggleHighLight()
    {
        isHighLight = !isHighLight;
        UpdateVisual();
        ApplyToGameController();
    }

    /// <summary>
    /// Gọi từ bên ngoài (SettingUIController, SettingManager, v.v.)
    /// khi muốn đặt trạng thái cụ thể mà KHÔNG gây vòng lặp.
    /// </summary>
    public void SetHighLight(bool value)
    {
        if (isHighLight == value) return; // không làm gì nếu đã đúng rồi
        isHighLight = value;
        UpdateVisual();
        ApplyToGameController();
    }

    public bool GetHighLight() => isHighLight;

    private void UpdateVisual()
    {
        if (buttonImage == null) return;
        buttonImage.sprite = isHighLight ? highLightSprite : normalSprite;
    }

    private void ApplyToGameController()
    {
        if (_gameController != null)
            _gameController.SetHighlight(isHighLight);
    }
}
