using UnityEngine;
using UnityEngine.UI;

public class LevelSelectController : MonoBehaviour
{
    [SerializeField] private LevelButton levelButtonPrefab;
    [SerializeField] private Transform gridParent;
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private int pageSize = 10;

    [Header("Navigation Buttons (Optional)")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    private int currentPage = 0;

    void Start()
    {
        UpdatePage();
    }

    private void UpdatePage()
    {
        if (levelDatabase == null || levelDatabase.levels == null || levelDatabase.levels.Count == 0) return;

        // Xóa các button cũ trong gridParent
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        int totalLevels = levelDatabase.levels.Count;
        int startIndex = currentPage * pageSize;
        int endIndex = Mathf.Min(startIndex + pageSize, totalLevels);

        // Lấy level cao nhất đã mở khóa (mặc định là level 0)
        int highestLevel = PlayerPrefs.GetInt("HighestLevel", 0);

        for (int i = startIndex; i < endIndex; i++)
        {
            LevelButton btn = Instantiate(levelButtonPrefab, gridParent);
            
            // Level được mở khóa nếu là level đầu tiên (0) hoặc nhỏ hơn/bằng level cao nhất đã đạt được
            bool isUnlocked = (i == 0 || i <= highestLevel);
            
            btn.Setup(i, levelDatabase.levels[i], isUnlocked);
        }

        // Cập nhật trạng thái của các nút điều hướng trang
        if (prevButton != null)
        {
            prevButton.interactable = currentPage > 0;
        }
        if (nextButton != null)
        {
            int maxPage = Mathf.CeilToInt((float)totalLevels / pageSize) - 1;
            nextButton.interactable = currentPage < maxPage;
        }
    }

    public void NextPage()
    {
        if (levelDatabase == null || levelDatabase.levels == null) return;
        int totalLevels = levelDatabase.levels.Count;
        int maxPage = Mathf.CeilToInt((float)totalLevels / pageSize) - 1;
        if (currentPage < maxPage)
        {
            currentPage++;
            UpdatePage();
        }
    }

    public void PrevPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePage();
        }
    }

    public void OnBack()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
    }
}
