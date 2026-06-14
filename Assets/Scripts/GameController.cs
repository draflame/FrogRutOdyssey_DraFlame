using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using TMPro;

[System.Serializable]
public struct MoveRecord
{
    public Vector2Int from;
    public Vector2Int to;
}

class SolverState
{
    public LogicTileType[] map;
    public HashSet<Vector2Int> visited;

    public SolverState(LogicTileType[] src)
    {
        map = src != null ? (LogicTileType[])src.Clone() : new LogicTileType[0];
        visited = new HashSet<Vector2Int>();
    }
}

public enum Difficulty
{
    Easy, Normal, Hard, VeryHard, Expert, Nightmare
}

public class GameController : MonoBehaviour, IGameController
{
    [Header("Level")]
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private TextMeshProUGUI levelName;
    public int currentLevelIndex;
    private LevelData currentLevel;

    [Header("Tilemap")]
    [SerializeField] private Tilemap lotusTilemap;

    [Header("Tile Packs (New System)")]
    [Tooltip("Kéo thả các TilePack ScriptableObject vào đây. GameController sẽ tra cứu tile từ các pack này.")]
    [SerializeField] private TilePack[] tilePacks;

    // Runtime lookup: packName → TilePack
    private Dictionary<string, TilePack> _packLookup;

    [Header("Highlight")]
    [SerializeField] private Tilemap highlightTilemap;
    [SerializeField] private TileBase highlightTile;
    [SerializeField] private bool showHighlight = false;

    [Header("Player")]
    [SerializeField] private FrogController frogPrefab;
    private FrogController frogInstance;

    [Header("UI")]
    [SerializeField] public GameObject gameOverUI;
    [SerializeField] public GameObject winUI;

    private HashSet<Vector2Int> visitedTiles = new();

    // Runtime map MỚI (logic only — dùng cho solver, CanMove, win condition)
    private LogicTileType[] runtimeLogicMap;

    // TileRef runtime map — dùng khi level ở TileRef format
    private TileRef[] runtimeRefMap;

    private int totalWalkableTiles;

    private Stack<MoveRecord> moveHistory = new();

    [Header("Setting Panel")]
    [SerializeField] private GameObject SettingPanel;
    [SerializeField] private GameObject OnGamePanel;
    private bool isSetting = false;

    [Header("Camera")]
    [SerializeField] private CinemachineMapFitter camFitter;

#if UNITY_EDITOR
    [ContextMenu("Check Map Solvable")]
    private void EditorCheck()
    {
        LoadLevel(currentLevelIndex);
        Debug.Log(CheckSolvable() ? "SOLVABLE" : "NOT SOLVABLE");
    }
#endif

    private readonly Vector2Int[] knightMoves =
        {
            new(1,2), new(2,1), new(-1,2), new(-2,1),
            new(1,-2), new(2,-1), new(-1,-2), new(-2,-1)
        };

    // ===================== UNITY =====================

    private void Awake()
    {
        // Build TilePack lookup
        _packLookup = new Dictionary<string, TilePack>();
        if (tilePacks != null)
        {
            foreach (var pack in tilePacks)
            {
                if (pack == null) continue;
                pack.BuildCache();
                if (!_packLookup.ContainsKey(pack.packName))
                    _packLookup[pack.packName] = pack;
                else
                    Debug.LogWarning($"[GameController] Duplicate pack name \"{pack.packName}\" — bỏ qua.");
            }
        }
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "RandomGameScene")
        {
            int level = PlayerPrefs.GetInt("SelectedLevel", 0);
            LoadLevel(level);
        }

        camFitter.FitToTilemap();
    }

    // ===================== LEVEL =====================
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }

    }

    private void HandleClick()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.nearClipPlane;
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePos);
        mouseWorld.z = 0f;

        Vector3Int cell = lotusTilemap.WorldToCell(mouseWorld);
        Vector2Int target = new Vector2Int(cell.x, cell.y);

        Debug.Log($"Clicked cell: {cell}");
        TryMoveFrog(target);
    }

    private void TryMoveFrog(Vector2Int target)
    {
        if (frogInstance == null) return;
        frogInstance.TryMove(target);
    }

    public void LoadLevel(int index)
    {
        if (levelDatabase == null || levelDatabase.levels == null || levelDatabase.levels.Count == 0)
        {
            Debug.LogError("No levels assigned in database!");
            return;
        }
        Time.timeScale = 1f;
        OnGamePanel.SetActive(true);
        currentLevelIndex = Mathf.Clamp(index, 0, levelDatabase.levels.Count - 1);
        currentLevel = levelDatabase.levels[currentLevelIndex];
        levelName.text = "Level "+(currentLevelIndex+1);
        if (currentLevel == null)
        {
            Debug.LogError("Level is NULL");
            return;
        }

        // ── Load map runtime ──────────────────────────────────
        runtimeRefMap = currentLevel.tileRefMap != null ? (TileRef[])currentLevel.tileRefMap.Clone() : new TileRef[currentLevel.width * currentLevel.height];
        runtimeLogicMap = BuildLogicMap(currentLevel, runtimeRefMap);

        // Đếm số tile phải đi qua (walkable = Lotus)
        totalWalkableTiles = 0;
        foreach (var t in runtimeLogicMap)
        {
            if (t == LogicTileType.Lotus)
                totalWalkableTiles++;
        }

        visitedTiles.Clear();

        lotusTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();

        if (gameOverUI) gameOverUI.SetActive(false);
        if (winUI) winUI.SetActive(false);

        moveHistory.Clear();

        // ── Đồng bộ trạng thái highlight từ SettingManager ──────────
        // Đảm bảo tilemap luôn khớp với setting đã lưu khi vào map mới
        if (SettingManager.Instance != null)
            showHighlight = SettingManager.Instance.IsHighlightOn;

        DrawMap();
        SpawnFrog();
        camFitter.FitToTilemap();
    }

    public void RecordMove(Vector2Int from, Vector2Int to)
    {
        moveHistory.Push(new MoveRecord { from = from, to = to });
    }

    public void Undo()
    {
        if (moveHistory.Count == 0) return;
        Debug.Log("Undoing last move...");
        Time.timeScale = 1f;
        if (winUI) winUI.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(false);

        MoveRecord last = moveHistory.Pop();

        // Khôi phục logic và re-render sprite Lotus tại ô đó
        SetLogicTile(last.to, LogicTileType.Lotus);
        RestoreTileVisual(last.to);
        visitedTiles.Remove(last.to);

        SetLogicTile(last.from, LogicTileType.Lotus);
        RestoreTileVisual(last.from);
        visitedTiles.Remove(last.from);

        frogInstance.ForceMove(last.from);
        HighlightValidMoves(last.from);
    }

    /// <summary>
    /// Vẽ lại sprite gốc của tile tại vị trí (x,y) từ dữ liệu map runtime.
    /// Dùng khi Undo: sau khi reset logic về Lotus, cần hiện lại sprite Lotus.
    /// </summary>
    private void RestoreTileVisual(Vector2Int tile)
    {
        int index = tile.y * currentLevel.width + tile.x;
        var cell = new Vector3Int(tile.x, tile.y, 0);

        if (runtimeRefMap != null && index < runtimeRefMap.Length)
        {
            TileRef tref = runtimeRefMap[index];
            TileBase tb = ResolveTileBase(tref);
            lotusTilemap.SetTile(cell, tb);
        }
    }

    private void DrawMap()
    {
        for (int x = 0; x < currentLevel.width; x++)
        {
            for (int y = 0; y < currentLevel.height; y++)
            {
                int index = y * currentLevel.width + x;
                TileBase tileBase = null;

                if (runtimeRefMap != null && index < runtimeRefMap.Length)
                {
                    TileRef tref = runtimeRefMap[index];
                    if (tref.IsEmpty) continue;
                    tileBase = ResolveTileBase(tref);
                }

                if (tileBase != null)
                    lotusTilemap.SetTile(new Vector3Int(x, y, 0), tileBase);
            }
        }
        camFitter.FitToTilemap();
    }



    // ===================== TILE DATA =====================

    /// <summary>Logic type của ô (x,y) — dùng cho solver, CanMove, win condition.</summary>
    public LogicTileType GetLogicTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= currentLevel.width || y >= currentLevel.height)
            return LogicTileType.Empty;
        int index = y * currentLevel.width + x;
        return runtimeLogicMap != null ? runtimeLogicMap[index] : LogicTileType.Empty;
    }

    // ===================== SPAWN =====================

    private void SpawnFrog()
    {
        Vector2Int start = currentLevel.startTile;

        if (GetLogicTile(start.x, start.y) == LogicTileType.Empty)
        {
            Debug.LogError("Start tile is EMPTY!");
            return;
        }

        if (frogInstance != null)
            Destroy(frogInstance.gameObject);

        Vector3 pos = GetWorldPosition(start);
        frogInstance = Instantiate(frogPrefab, pos, Quaternion.identity);
        frogInstance.Initialize(this, start);

        if (showHighlight)
            HighlightValidMoves(start);
    }

    public Vector3 GetWorldPosition(Vector2Int tile)
    {
        Vector3Int cell = new Vector3Int(tile.x, tile.y, 0);
        Vector3 pos = lotusTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        return pos;
    }

    // ===================== MOVE LOGIC =====================

    public bool CanMove(Vector2Int from, Vector2Int to)
    {
        Debug.Log($"CanMove from {from} to {to}");

        if (!IsInsideMap(to))
        {
            Debug.Log("? Outside map");
            return false;
        }

        LogicTileType tile = GetLogicTile(to.x, to.y);
        Debug.Log($"LogicTile = {tile}");

        if (tile != LogicTileType.Lotus)
        {
            Debug.Log("? Blocked tile");
            return false;
        }

        if (visitedTiles.Contains(to))
        {
            Debug.Log("? Tile already visited");
            return false;
        }

        Vector2Int d = to - from;
        int dx = Mathf.Abs(d.x);
        int dy = Mathf.Abs(d.y);
        bool isKnight = (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
        Debug.Log($"Knight move = {isKnight}");
        return isKnight;
    }

    private bool IsInsideMap(Vector2Int p)
    {
        return p.x >= 0 && p.x < currentLevel.width &&
               p.y >= 0 && p.y < currentLevel.height;
    }

    public void ConsumeTile(Vector2Int tile)
    {
        visitedTiles.Add(tile);
    }

    public void ClearTile(Vector2Int tile)
    {
        SetLogicTile(tile, LogicTileType.Water);
    }

    public void VisitTile(Vector2Int tile)
    {
        SetLogicTile(tile, LogicTileType.Water);
        visitedTiles.Add(tile);
    }

    // ===================== WIN / LOSE =====================

    private void CheckWinOrLose(Vector2Int current)
    {
        if (visitedTiles.Count == totalWalkableTiles)
        {
            Win();
            Debug.Log("You Win!");
            return;
        }

        foreach (var move in knightMoves)
        {
            if (CanMove(current, current + move))
                return;
        }
        Debug.Log("No more moves! Game Over!");
        GameOver();
    }

    private int CountWalkableTiles()
    {
        int count = 0;
        if (runtimeLogicMap != null)
            foreach (var t in runtimeLogicMap)
                if (t != LogicTileType.Empty) count++;
        return count;
    }

    private void GameOver()
    {
        if (gameOverUI) gameOverUI.SetActive(true);
        Time.timeScale = 0f;
        OnGamePanel.SetActive(false);
        camFitter.FitToTilemap();
    }

    private void Win()
    {
        if (winUI) winUI.SetActive(true);
        Time.timeScale = 0f;
        
        OnGamePanel.SetActive(false);

        // Mở khóa level tiếp theo
        int highestLevel = PlayerPrefs.GetInt("HighestLevel", 0);
        if (currentLevelIndex >= highestLevel)
        {
            PlayerPrefs.SetInt("HighestLevel", currentLevelIndex + 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Cập nhật logic tile và re-render visual.</summary>
    public void SetLogicTile(Vector2Int tile, LogicTileType newLogic)
    {
        int index = tile.y * currentLevel.width + tile.x;
        if (runtimeLogicMap != null && index < runtimeLogicMap.Length)
            runtimeLogicMap[index] = newLogic;

        var cell = new Vector3Int(tile.x, tile.y, 0);

        if (newLogic == LogicTileType.Empty)
        {
            // Empty → xóa trắng hoàn toàn
            lotusTilemap.SetTile(cell, null);
        }
        else if (newLogic == LogicTileType.Water)
        {
            // Water → hiện sprite nước (frog vừa nhảy qua làm lót biến thành nước)
            TileBase waterTile = GetWaterTileBase(index);
            lotusTilemap.SetTile(cell, waterTile);
        }
    }

    /// <summary>
    /// Lấy sprite Water để hiện khi frog nhảy qua ô.
    /// Tìm trong TilePack (entry có logicType == Water).
    /// </summary>
    private TileBase GetWaterTileBase(int mapIndex)
    {
        if (_packLookup != null)
        {
            // Ưu tiên: nếu ô đó đang là TileRef, lấy pack tương ứng và tìm entry Water
            if (runtimeRefMap != null && mapIndex < runtimeRefMap.Length)
            {
                var tref = runtimeRefMap[mapIndex];
                if (!tref.IsEmpty && _packLookup.TryGetValue(tref.packName, out var originPack))
                {
                    // Tìm entry đầu tiên có logicType == Water trong cùng pack
                    foreach (var e in originPack.entries)
                        if (e != null && e.logicType == LogicTileType.Water && e.tile != null)
                            return e.tile;
                }
            }
            // Fallback: tìm trong bất kỳ pack nào có Water entry
            foreach (var pack in _packLookup.Values)
                foreach (var e in pack.entries)
                    if (e != null && e.logicType == LogicTileType.Water && e.tile != null)
                        return e.tile;
        }

        return null; // Không tìm được → ô sẽ trống
    }

    public void ClearHighlights()
    {
        highlightTilemap.ClearAllTiles();
    }

    private bool CanHighlight(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideMap(to)) return false;
        if (GetLogicTile(to.x, to.y) != LogicTileType.Lotus) return false;
        if (visitedTiles.Contains(to)) return false;

        Vector2Int d = to - from;
        int dx = Mathf.Abs(d.x);
        int dy = Mathf.Abs(d.y);
        return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
    }

    public void ToggleHighlight()
    {
        showHighlight = !showHighlight;
        if (!showHighlight) ClearHighlights();
        else HighlightValidMoves(frogInstance.currentTile);
    }

    public void SetHighlight(bool value)
    {
        showHighlight = value;
        Debug.Log("SetHighlight: " + showHighlight);
        if (!showHighlight) ClearHighlights();
        else HighlightValidMoves(frogInstance.currentTile);
    }

    public void HighlightValidMoves(Vector2Int from)
    {
        if (!showHighlight) return;
        ClearHighlights();

        foreach (var move in knightMoves)
        {
            Vector2Int target = from + move;
            if (!CanHighlight(from, target)) continue;
            highlightTilemap.SetTile(new Vector3Int(target.x, target.y, 0), highlightTile);
        }
    }

    private bool AnyLotusRemaining()
    {
        if (runtimeLogicMap == null) return false;
        foreach (var t in runtimeLogicMap)
            if (t == LogicTileType.Lotus) return true;
        return false;
    }

    public void OnFrogMoved(Vector2Int currentTile)
    {
        HighlightValidMoves(currentTile);

        if (visitedTiles.Count + 1 == totalWalkableTiles)
        {
            Debug.Log("YOU WIN!");
            Win();
            return;
        }

        foreach (var move in knightMoves)
        {
            if (CanMove(currentTile, currentTile + move))
                return;
        }

        Debug.Log("YOU LOSE!");
        GameOver();
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        if (gameOverUI) gameOverUI.SetActive(false);
        if (winUI) winUI.SetActive(false);
        ClearHighlights();
        if (frogInstance != null)
        {
            Destroy(frogInstance.gameObject);
            frogInstance = null;
        }
        CloseSetting();
        LoadLevel(currentLevelIndex);
    }

    public void BackToLevelSelect()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LevelSelectScene");
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
    }

    public bool CheckSolvable()
    {
        Vector2Int start = currentLevel.startTile;
        SolverState state = new SolverState(runtimeLogicMap);
        return SolveDFS(start, 1, state);
    }

    private bool SolveDFS(Vector2Int pos, int visitedCount, SolverState state)
    {
        state.visited.Add(pos);

        if (visitedCount == totalWalkableTiles)
            return true;

        foreach (var move in knightMoves)
        {
            Vector2Int next = pos + move;
            if (!CanMoveSolver(pos, next, state)) continue;

            state.map[next.y * currentLevel.width + next.x] = LogicTileType.Water;
            if (SolveDFS(next, visitedCount + 1, state)) return true;
            state.map[next.y * currentLevel.width + next.x] = LogicTileType.Lotus;
            state.visited.Remove(next);
        }
        return false;
    }

    private bool CanMoveSolver(Vector2Int from, Vector2Int to, SolverState state)
    {
        if (to.x < 0 || to.y < 0 || to.x >= currentLevel.width || to.y >= currentLevel.height)
            return false;
        int index = to.y * currentLevel.width + to.x;
        if (state.map[index] != LogicTileType.Lotus) return false;
        if (state.visited.Contains(to)) return false;

        Vector2Int d = to - from;
        int dx = Mathf.Abs(d.x);
        int dy = Mathf.Abs(d.y);
        return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
    }

    public void OnCheckSolvableButton()
    {
        Debug.Log("Checking map solvability...");
        bool solvable = CheckSolvable();
        if (solvable) Debug.Log("? MAP SOLVABLE");
        else Debug.LogError("? MAP NOT SOLVABLE");
    }

    public void ToggleSetting()
    {
        if (isSetting) Time.timeScale = 1f;
        else Time.timeScale = 0f;
        SettingPanel.gameObject.SetActive(!isSetting);
        isSetting = !isSetting;
    }
    public void CloseSetting()
    {
        isSetting = false;
        Time.timeScale = 1f;
        SettingPanel.gameObject.SetActive(false);
    }

    public void OnNextLevel()
    {
        if (levelDatabase == null || levelDatabase.levels == null) return;
        int nextLevelIndex = currentLevelIndex + 1;
        if (nextLevelIndex < levelDatabase.levels.Count)
        {
            PlayerPrefs.SetInt("SelectedLevel", nextLevelIndex);
            SceneManager.LoadScene("GameScene");
        }
        else
        {
            SceneManager.LoadScene("LevelSelectScene");
        }
    }

    // ===================== TILEPACK HELPERS =====================

    private TileBase ResolveTileBase(TileRef tref)
    {
        if (tref.IsEmpty) return null;
        if (_packLookup != null && _packLookup.TryGetValue(tref.packName, out var pack))
            return pack.GetTile(tref.tileId);
        return null;
    }

    private LogicTileType[] BuildLogicMap(LevelData level, TileRef[] refMap)
    {
        var result = new LogicTileType[refMap.Length];
        for (int i = 0; i < refMap.Length; i++)
        {
            TileRef tref = refMap[i];
            if (tref.IsEmpty) { result[i] = LogicTileType.Empty; continue; }

            if (_packLookup != null && _packLookup.TryGetValue(tref.packName, out var runtimePack))
            {
                var entry = runtimePack.Get(tref.tileId);
                result[i] = entry != null ? entry.logicType : LogicTileType.Grass;
                continue;
            }
            if (level.localPacks != null)
            {
                bool found = false;
                foreach (var lp in level.localPacks)
                {
                    if (lp == null || lp.packName != tref.packName) continue;
                    var entry = lp.Get(tref.tileId);
                    result[i] = entry != null ? entry.logicType : LogicTileType.Grass;
                    found = true;
                    break;
                }
                if (found) continue;
            }
            result[i] = LogicTileType.Grass;
        }
        return result;
    }
}
