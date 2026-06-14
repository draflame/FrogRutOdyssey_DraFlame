using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelButton : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button button; // Thành phần Button UI để bật/tắt tương tác
    [SerializeField] private GameObject lockIcon; // GameObject hình khóa hiển thị khi level bị khóa (tùy chọn)

    private int levelIndex;
    private LevelData levelData;
    private void Awake()
    {
        // Tự động tìm Button component nếu chưa được kéo thả trong Inspector
        if (button == null) button = GetComponent<Button>();
    }

    public void Setup(int index, LevelData levelData, bool isUnlocked)
    {
        levelIndex = index;
        this.levelData = levelData;
        // Hiển thị tên của Level (tên file asset hoặc mặc định)
        levelText.text = levelData != null ? levelData.name : $"Level {index + 1}";

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = isUnlocked; // Bật/tắt khả năng nhấn nút dựa trên lock state
        }

        if (lockIcon != null)
        {
            lockIcon.SetActive(!isUnlocked); // Hiển thị hình khóa nếu level bị khóa
        }
    }

    public void OnClick()
    {
        PlayerPrefs.SetInt("SelectedLevel", levelIndex);
        UnityEngine.SceneManagement.SceneManager.LoadScene(levelData.sceneName);
    }
}
