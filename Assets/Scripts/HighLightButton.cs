using UnityEngine;
using UnityEngine.UI;

public class HighLightButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isHighLight = true;
    [SerializeField] private Sprite highLightSprite; // Ảnh hiển thị khi BẬT highlight (mắt mở - eye open)
    [SerializeField] private Sprite normalSprite;    // Ảnh hiển thị khi TẮT highlight (mắt nhắm - eye close)

    [Header("Component References")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private IGameController gameController;

    private void Start()
    {
        // Tự động tìm component nếu chưa được kéo thả trong Inspector
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // Tìm Game Controller tương ứng trong scene
        gameController = Object.FindAnyObjectByType<GameController>();
        if (gameController == null)
        {
            gameController = Object.FindAnyObjectByType<RandomGameController>();
        }

        // Cập nhật ảnh hiển thị ban đầu dựa trên trạng thái
        UpdateVisual();
    }

    // Hàm toggle, khi nhấn sẽ đổi trạng thái isHighLight và cập nhật ảnh tương ứng
    public void ToggleHighLight()
    {
        isHighLight = !isHighLight;
        UpdateVisual();

        if (gameController != null)
        {
            gameController.ToggleHighlight();
        }
    }

    // Hàm set highlight, đặt trạng thái cụ thể và cập nhật ảnh tương ứng
    public void SetHighLight(bool value)
    {
        isHighLight = value;
        UpdateVisual();

        if (gameController != null)
        {
            gameController.SetHighlight(value);
        }
    }

    // Hàm get highlight, trả về trạng thái hiện tại
    public bool GetHighLight()
    {
        return isHighLight;
    }

    // Cập nhật ảnh hiển thị của Image hoặc SpriteRenderer
    private void UpdateVisual()
    {
        Sprite targetSprite = isHighLight ? highLightSprite : normalSprite;

        if (buttonImage != null)
        {
            buttonImage.sprite = targetSprite;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = targetSprite;
        }
    }
}


