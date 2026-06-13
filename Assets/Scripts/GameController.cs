using NUnit.Framework.Internal;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

[System.Serializable]
public struct MoveRecord
{
    public Vector2Int from;
    public Vector2Int to;
}

class SolverState
{
    public TileType[] map;
    public HashSet<Vector2Int> visited;

    public SolverState(TileType[] src)
    {
        map = src != null ? (TileType[])src.Clone() : new TileType[0];
        visited = new HashSet<Vector2Int>();
    }
}

public enum Difficulty
{
    Easy, Normal, Hard, VeryHard, Expert, Nightmare
}

public class GameController : MonoBehaviour
{
    [Header("Level")]
    public LevelData[] allLevels;
    public int currentLevelIndex;
    private LevelData currentLevel;

    [Header("Tilemap")]
    [SerializeField] private Tilemap lotusTilemap;

    [Header("Tile Packs (New System)")]
    [Tooltip("Kéo thả các TilePack ScriptableObject vào đây. GameController sẽ tra cứu tile từ các pack này.")]
    [SerializeField] private TilePack[] tilePacks;

    [Header("Legacy Tile Visual (Old System)")]
    [Tooltip("TileVisualData cũ — giữ lại để render các level chưa migrate sang TileRef format.")]
    [SerializeField] private TileVisualData tileSet;

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

    // Runtime map CŨ — giữ lại để backward compat với level dùng TileType[]
    private TileType[] runtimeMap;

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

    private TileType[] originalMap;
    private Vector2Int originalStart;
    private int originalWalkable;

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

    private Difficulty difficulty = Difficulty.Normal;

    // ===================== UNITY =====================

    private void Awake()
    {
        // Build legacy tileset lookup
        if (tileSet != null)
            tileSet.BuildLookup();

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
        if (allLevels == null || allLevels.Length == 0)
        {
            Debug.LogError("No levels assigned!");
            return;
        }
        Time.timeScale = 1f;
        OnGamePanel.SetActive(true);
        currentLevelIndex = Mathf.Clamp(index, 0, allLevels.Length - 1);
        currentLevel = allLevels[currentLevelIndex];

        if (currentLevel == null)
        {
            Debug.LogError("Level is NULL");
            return;
        }

        // ── Load map runtime ──────────────────────────────────
        if (currentLevel.UsesTileRefFormat)
        {
            // Format MỚI: TileRef
            runtimeRefMap = (TileRef[])currentLevel.tileRefMap.Clone();
            runtimeLogicMap = BuildLogicMap(currentLevel, runtimeRefMap);
            runtimeMap = null;
        }
        else
        {
            // Format CŨ: TileType
            if (currentLevel.map == null)
            {
                Debug.LogError("Level map is NULL");
                return;
            }
            runtimeMap = new TileType[currentLevel.map.Length];
            currentLevel.map.CopyTo(runtimeMap, 0);
            runtimeLogicMap = BuildLogicMapLegacy(runtimeMap);
            runtimeRefMap = null;
        }

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

        DrawMap();
        SpawnFrog();
        AfterMapGenerated();
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

        if (runtimeRefMap != null)
        {
            // Format mới: lấy TileRef từ snapshot ban đầu
            // runtimeRefMap không thay đổi khi frog di chuyển → vẫn chứa ref gốc
            TileRef tref = runtimeRefMap[index];
            TileBase tb = ResolveTileBase(tref);
            lotusTilemap.SetTile(cell, tb);
        }
        else if (runtimeMap != null)
        {
            // Format cũ: dùng TileType trong runtimeMap (vừa được set lại)
            TileType t = runtimeMap[index];
            if (tileSet != null && tileSet._lookup != null &&
                tileSet._lookup.TryGetValue(t, out var tb))
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

                if (runtimeRefMap != null)
                {
                    // Format MỚI: lookup từ TilePack
                    TileRef tref = runtimeRefMap[index];
                    if (tref.IsEmpty) continue;
                    tileBase = ResolveTileBase(tref);
                }
                else if (runtimeMap != null)
                {
                    // Format CŨ: dùng tileSet legacy
                    TileType type = runtimeMap[index];
                    if (type == TileType.Empty) continue;
                    if (tileSet != null && tileSet._lookup != null &&
                        tileSet._lookup.TryGetValue(type, out var tb))
                        tileBase = tb;
                }

                if (tileBase != null)
                    lotusTilemap.SetTile(new Vector3Int(x, y, 0), tileBase);
            }
        }
    }

    void AfterMapGenerated()
    {
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

    /// <summary>Backward compat — trả về TileType cũ (chỉ dùng nếu cần).</summary>
    public TileType GetTileType(int x, int y)
    {
        if (x < 0 || y < 0 || x >= currentLevel.width || y >= currentLevel.height)
            return TileType.Empty;
        if (runtimeMap != null)
        {
            int index = y * currentLevel.width + x;
            return runtimeMap[index];
        }
        return LogicToLegacy(GetLogicTile(x, y));
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
    }

    private void Win()
    {
        if (winUI) winUI.SetActive(true);
        Time.timeScale = 0f;
        OnGamePanel.SetActive(false);
    }

    /// <summary>Cập nhật logic tile và re-render visual (hỗ trợ cả 2 format).</summary>
    public void SetLogicTile(Vector2Int tile, LogicTileType newLogic)
    {
        int index = tile.y * currentLevel.width + tile.x;
        if (runtimeLogicMap != null)
            runtimeLogicMap[index] = newLogic;

        var cell = new Vector3Int(tile.x, tile.y, 0);

        if (newLogic == LogicTileType.Empty)
        {
            // Empty → xóa trắng hoàn toàn
            lotusTilemap.SetTile(cell, null);
            if (runtimeMap != null)
                runtimeMap[index] = TileType.Empty;
        }
        else if (newLogic == LogicTileType.Water)
        {
            // Water → hiện sprite nước (frog vừa nhảy qua làm lót biến thành nước)
            TileBase waterTile = GetWaterTileBase(index);
            lotusTilemap.SetTile(cell, waterTile);
            if (runtimeMap != null)
                runtimeMap[index] = TileType.Water;
        }
        // Lotus / Grass → giữ nguyên visual hiện tại (tile đã được draw lúc DrawMap)
    }

    /// <summary>
    /// Lấy sprite Water để hiện khi frog nhảy qua ô.
    /// Tìm trong TilePack (entry có logicType == Water), fallback sang tileSet legacy.
    /// </summary>
    private TileBase GetWaterTileBase(int mapIndex)
    {
        // Tìm trong pack cướt runtime
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
        // Fallback cuối: legacy tileSet
        if (tileSet != null && tileSet._lookup != null &&
            tileSet._lookup.TryGetValue(TileType.Water, out var legacyWater))
            return legacyWater;

        return null; // Không tìm được → ô sẽ trống
    }

    /// <summary>Backward compat — dùng TileType cũ.</summary>
    public void SetTileType(Vector2Int tile, TileType newType)
    {
        LogicTileType logic = LevelData.LegacyToLogic(newType);
        SetLogicTile(tile, logic);

        if (runtimeMap != null)
        {
            int index = tile.y * currentLevel.width + tile.x;
            runtimeMap[index] = newType;
            var cell = new Vector3Int(tile.x, tile.y, 0);
            if (newType == TileType.Empty)
            {
                lotusTilemap.SetTile(cell, null);
            }
            else if (tileSet != null && tileSet._lookup != null &&
                     tileSet._lookup.TryGetValue(newType, out var tb))
            {
                lotusTilemap.SetTile(cell, tb);
            }
        }
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
        // Solver vẫn dùng TileType array — nếu format mới, tạo legacy array tạm
        TileType[] solverMap = runtimeMap ?? CreateLegacyMapFromLogic();
        SolverState state = new SolverState(solverMap);
        return SolveDFS(start, 1, state);
    }

    private TileType[] CreateLegacyMapFromLogic()
    {
        if (runtimeLogicMap == null) return new TileType[0];
        var arr = new TileType[runtimeLogicMap.Length];
        for (int i = 0; i < arr.Length; i++)
            arr[i] = LogicToLegacy(runtimeLogicMap[i]);
        return arr;
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

            state.map[next.y * currentLevel.width + next.x] = TileType.Water;
            if (SolveDFS(next, visitedCount + 1, state)) return true;
            state.map[next.y * currentLevel.width + next.x] = TileType.Lotus;
            state.visited.Remove(next);
        }
        return false;
    }

    private bool CanMoveSolver(Vector2Int from, Vector2Int to, SolverState state)
    {
        if (to.x < 0 || to.y < 0 || to.x >= currentLevel.width || to.y >= currentLevel.height)
            return false;
        int index = to.y * currentLevel.width + to.x;
        if (state.map[index] != TileType.Lotus) return false;
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

    public void OnNextLevel()
    {
        PlayerPrefs.SetInt("SelectedLevel", currentLevelIndex + 1);
        SceneManager.LoadScene("GameScene");
    }

    // ===================== RANDOM LEVEL =====================

    bool TryGenerateRandomPath(int w, int h, int steps, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector2Int start = new Vector2Int(Random.Range(0, w), Random.Range(0, h));
            var used = new HashSet<Vector2Int>();
            var tempPath = new List<Vector2Int>();
            if (GenerateDFS(start, steps, w, h, tempPath, used))
            {
                path = tempPath;
                return true;
            }
        }
        return false;
    }

    bool GenerateDFS(Vector2Int pos, int steps, int w, int h, List<Vector2Int> path, HashSet<Vector2Int> used)
    {
        path.Add(pos);
        used.Add(pos);
        if (path.Count == steps) return true;

        List<Vector2Int> moves = new(knightMoves);
        Shuffle(moves);

        foreach (var m in moves)
        {
            Vector2Int next = pos + m;
            if (next.x < 0 || next.y < 0 || next.x >= w || next.y >= h) continue;
            if (used.Contains(next)) continue;
            if (GenerateDFS(next, steps, w, h, path, used)) return true;
        }

        path.RemoveAt(path.Count - 1);
        used.Remove(pos);
        return false;
    }

    void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    public void LoadRandomLevel(int width, int height, int minLotus, int maxLotus)
    {
        OnGamePanel.SetActive(true);
        Time.timeScale = 1f;
        float lotusRatio = 0.5f;

        switch (difficulty)
        {
            case Difficulty.Easy:   lotusRatio = 0.45f; break;
            case Difficulty.Normal: lotusRatio = 0.55f; break;
            case Difficulty.Hard:   lotusRatio = 0.6f;  break;
        }

        int lotusCount = Mathf.Clamp(Mathf.RoundToInt(width * height * lotusRatio), minLotus, maxLotus);

        if (!TryGenerateRandomPath(width, height, lotusCount, out var path))
        {
            Debug.LogError("? Failed to generate random map");
            return;
        }

        runtimeMap = new TileType[width * height];
        for (int i = 0; i < runtimeMap.Length; i++) runtimeMap[i] = TileType.Water;
        foreach (var p in path) runtimeMap[p.y * width + p.x] = TileType.Lotus;

        int extra = lotusCount - path.Count;
        int tries = 0;
        while (extra > 0 && tries < 5000)
        {
            tries++;
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            int idx = y * width + x;
            if (runtimeMap[idx] == TileType.Water) { runtimeMap[idx] = TileType.Lotus; extra--; }
        }

        runtimeLogicMap = BuildLogicMapLegacy(runtimeMap);
        runtimeRefMap = null;

        totalWalkableTiles = 0;
        foreach (var t in runtimeLogicMap)
            if (t == LogicTileType.Lotus) totalWalkableTiles++;

        visitedTiles.Clear();

        currentLevel = ScriptableObject.CreateInstance<LevelData>();
        currentLevel.width = width;
        currentLevel.height = height;
        currentLevel.startTile = path[0];

        lotusTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();

        DrawMap();
        SpawnFrog();
        originalMap = (TileType[])runtimeMap.Clone();
        originalStart = currentLevel.startTile;
        originalWalkable = totalWalkableTiles;
        AfterMapGenerated();
    }

    public void RetrySameRandomMap(int randomSize)
    {
        Time.timeScale = 1f;
        if (winUI) winUI.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(false);
        OnGamePanel.SetActive(true);
        if (originalMap == null) { Debug.LogError("No snapshot to retry"); return; }

        runtimeMap = (TileType[])originalMap.Clone();
        runtimeLogicMap = BuildLogicMapLegacy(runtimeMap);
        runtimeRefMap = null;
        totalWalkableTiles = originalWalkable;

        visitedTiles.Clear();
        moveHistory.Clear();

        currentLevel = ScriptableObject.CreateInstance<LevelData>();
        currentLevel.width = randomSize;
        currentLevel.height = randomSize;
        currentLevel.startTile = originalStart;

        lotusTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();

        DrawMap();
        SpawnFrog();
        AfterMapGenerated();
    }

    // ===================== TILEPACK HELPERS =====================

    /// <summary>Resolve TileRef → TileBase từ TilePack runtime.</summary>
    private TileBase ResolveTileBase(TileRef tref)
    {
        if (tref.IsEmpty) return null;
        if (_packLookup != null && _packLookup.TryGetValue(tref.packName, out var pack))
            return pack.GetTile(tref.tileId);
        return null;
    }

    /// <summary>Build LogicTileType[] từ TileRef[] (format mới).</summary>
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

    /// <summary>Build LogicTileType[] từ TileType[] (format cũ).</summary>
    private LogicTileType[] BuildLogicMapLegacy(TileType[] oldMap)
    {
        var result = new LogicTileType[oldMap.Length];
        for (int i = 0; i < oldMap.Length; i++)
            result[i] = LevelData.LegacyToLogic(oldMap[i]);
        return result;
    }

    /// <summary>Convert LogicTileType → TileType cũ (backward compat).</summary>
    private static TileType LogicToLegacy(LogicTileType logic)
    {
        switch (logic)
        {
            case LogicTileType.Lotus: return TileType.Lotus;
            case LogicTileType.Water: return TileType.Water;
            case LogicTileType.Grass: return TileType.Grass;
            default:                  return TileType.Empty;
        }
    }
}
