using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class RandomGameController : MonoBehaviour, IGameController
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap lotusTilemap;

    [Header("Tile Packs")]
    [Tooltip("Kéo thả các TilePack ScriptableObject vào đây.")]
    [SerializeField] private TilePack[] tilePacks;

    // Runtime lookup: packName → TilePack
    private Dictionary<string, TilePack> _packLookup;

    [Header("Highlight")]
    [SerializeField] private Tilemap highlightTilemap;
    [SerializeField] private TileBase highlightTile;
    [SerializeField] private bool showHighlight = true;

    [Header("Player")]
    [SerializeField] private FrogController frogPrefab;
    private FrogController frogInstance;

    [Header("UI")]
    [SerializeField] public GameObject gameOverUI;
    [SerializeField] public GameObject winUI;

    [Header("Setting Panel")]
    [SerializeField] private GameObject SettingPanel;
    [SerializeField] private GameObject OnGamePanel;
    private bool isSetting = false;



    private HashSet<Vector2Int> visitedTiles = new();
    private LogicTileType[] runtimeLogicMap;
    private TileRef[] runtimeRefMap;
    private int totalWalkableTiles;
    private Stack<MoveRecord> moveHistory = new();

    private LevelData currentLevel;
    private TileRef[] originalRefMap;
    private Vector2Int originalStart;
    private int originalWalkable;

    private Difficulty difficulty = Difficulty.Normal;
    private int size = 8;

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
                    Debug.LogWarning($"[RandomGameController] Duplicate pack name \"{pack.packName}\" — bỏ qua.");
            }
        }
    }

    private void Start()
    {
        if (SettingManager.Instance == null)
        {
            Debug.LogWarning("[RandomGameController] SettingManager.Instance is null! Vui lòng chạy game từ scene 'MenuScene' để SettingManager được khởi tạo và truyền nhạc/âm thanh.");
        }

        size = PlayerPrefs.GetInt("RandomMapSize", 8);
        difficulty = (Difficulty)PlayerPrefs.GetInt("RandomMapDifficulty", 0);

        int min = Mathf.RoundToInt(size * size * 0.4f);
        int max = Mathf.RoundToInt(size * size * 0.65f);

        LoadRandomLevel(size, size, min, max);
    }

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

    // ===================== GENERATOR & LEVEL LOAD =====================

    public void LoadRandomLevel(int w, int h, int minLotus, int maxLotus)
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

        int lotusCount = Mathf.Clamp(Mathf.RoundToInt(w * h * lotusRatio), minLotus, maxLotus);

        if (!TryGenerateRandomPath(w, h, lotusCount, out var path))
        {
            Debug.LogError("? Failed to generate random map");
            return;
        }

        runtimeRefMap = new TileRef[w * h];
        TileRef waterRef = FindTileRefByLogic(LogicTileType.Water);
        TileRef lotusRef = FindTileRefByLogic(LogicTileType.Lotus);

        for (int i = 0; i < runtimeRefMap.Length; i++)
            runtimeRefMap[i] = waterRef;

        foreach (var p in path)
            runtimeRefMap[p.y * w + p.x] = lotusRef;

        int extra = lotusCount - path.Count;
        int tries = 0;
        while (extra > 0 && tries < 5000)
        {
            tries++;
            int x = Random.Range(0, w);
            int y = Random.Range(0, h);
            int idx = y * w + x;
            if (runtimeRefMap[idx].IsEmpty || (waterRef.IsEmpty ? false : runtimeRefMap[idx].tileId == waterRef.tileId))
            {
                runtimeRefMap[idx] = lotusRef;
                extra--;
            }
        }

        currentLevel = ScriptableObject.CreateInstance<LevelData>();
        currentLevel.width = w;
        currentLevel.height = h;
        currentLevel.startTile = path[0];
        currentLevel.localPacks = tilePacks;
        currentLevel.tileRefMap = (TileRef[])runtimeRefMap.Clone();

        runtimeLogicMap = BuildLogicMap(currentLevel, runtimeRefMap);

        totalWalkableTiles = 0;
        foreach (var t in runtimeLogicMap)
            if (t == LogicTileType.Lotus) totalWalkableTiles++;

        visitedTiles.Clear();

        lotusTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();

        if (gameOverUI) gameOverUI.SetActive(false);
        if (winUI) winUI.SetActive(false);

        moveHistory.Clear();

        DrawMap();
        SpawnFrog();

        originalRefMap = (TileRef[])runtimeRefMap.Clone();
        originalStart = currentLevel.startTile;
        originalWalkable = totalWalkableTiles;
    }

    public void ReloadRandomLevel()
    {
        int size = PlayerPrefs.GetInt("RandomMapSize", 8);
        int min = Mathf.RoundToInt(size * size * 0.4f);
        int max = Mathf.RoundToInt(size * size * 0.65f);
        if (winUI) winUI.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(false);
        LoadRandomLevel(size, size, min, max);
    }

    public void OnRetry()
    {
        Time.timeScale = 1f;
        if (winUI) winUI.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(false);
        OnGamePanel.SetActive(true);
        if (originalRefMap == null) { Debug.LogError("No snapshot to retry"); return; }

        runtimeRefMap = (TileRef[])originalRefMap.Clone();
        runtimeLogicMap = BuildLogicMap(currentLevel, runtimeRefMap);
        totalWalkableTiles = originalWalkable;

        visitedTiles.Clear();
        moveHistory.Clear();

        currentLevel = ScriptableObject.CreateInstance<LevelData>();
        currentLevel.width = size;
        currentLevel.height = size;
        currentLevel.startTile = originalStart;

        lotusTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();

        DrawMap();
        SpawnFrog();
    }

    // ===================== IGameController IMPLEMENTATION =====================

    public Vector3 GetWorldPosition(Vector2Int tile)
    {
        Vector3Int cell = new Vector3Int(tile.x, tile.y, 0);
        Vector3 pos = lotusTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        return pos;
    }

    public bool CanMove(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideMap(to)) return false;

        LogicTileType tile = GetLogicTile(to.x, to.y);
        if (tile != LogicTileType.Lotus) return false;
        if (visitedTiles.Contains(to)) return false;

        Vector2Int d = to - from;
        int dx = Mathf.Abs(d.x);
        int dy = Mathf.Abs(d.y);
        return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
    }

    public void RecordMove(Vector2Int from, Vector2Int to)
    {
        moveHistory.Push(new MoveRecord { from = from, to = to });
    }

    public void ClearTile(Vector2Int tile)
    {
        SetLogicTile(tile, LogicTileType.Water);
    }

    public void ConsumeTile(Vector2Int tile)
    {
        visitedTiles.Add(tile);
    }

    public void OnFrogMoved(Vector2Int currentTile)
    {
        HighlightValidMoves(currentTile);

        if (visitedTiles.Count + 1 == totalWalkableTiles)
        {
            Win();
            return;
        }

        foreach (var move in knightMoves)
        {
            if (CanMove(currentTile, currentTile + move))
                return;
        }

        GameOver();
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

    // ===================== GAMEPLAY LOOP HELPER METHODS =====================

    private bool IsInsideMap(Vector2Int p)
    {
        return currentLevel != null &&
               p.x >= 0 && p.x < currentLevel.width &&
               p.y >= 0 && p.y < currentLevel.height;
    }

    public LogicTileType GetLogicTile(int x, int y)
    {
        if (!IsInsideMap(new Vector2Int(x, y))) return LogicTileType.Empty;
        int index = y * currentLevel.width + x;
        return runtimeLogicMap != null ? runtimeLogicMap[index] : LogicTileType.Empty;
    }

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
    }



    public void Undo()
    {
        if (moveHistory.Count == 0) return;
        Debug.Log("Undoing last move...");
        Time.timeScale = 1f;
        if (winUI) winUI.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(false);

        MoveRecord last = moveHistory.Pop();

        SetLogicTile(last.to, LogicTileType.Lotus);
        RestoreTileVisual(last.to);
        visitedTiles.Remove(last.to);

        SetLogicTile(last.from, LogicTileType.Lotus);
        RestoreTileVisual(last.from);
        visitedTiles.Remove(last.from);

        frogInstance.ForceMove(last.from);
        HighlightValidMoves(last.from);
    }

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

    private void GameOver()
    {
        if (gameOverUI) gameOverUI.SetActive(true);
        Time.timeScale = 0f;
        OnGamePanel.SetActive(false);
        if (SettingManager.Instance != null)
        {
            SettingManager.Instance.PlayLoseSound();
        }
    }

    private void Win()
    {
        if (winUI) winUI.SetActive(true);
        Time.timeScale = 0f;
        OnGamePanel.SetActive(false);
        if (SettingManager.Instance != null)
        {
            SettingManager.Instance.PlayWinSound();
        }
    }

    public void SetLogicTile(Vector2Int tile, LogicTileType newLogic)
    {
        int index = tile.y * currentLevel.width + tile.x;
        if (runtimeLogicMap != null && index < runtimeLogicMap.Length)
            runtimeLogicMap[index] = newLogic;

        var cell = new Vector3Int(tile.x, tile.y, 0);

        if (newLogic == LogicTileType.Empty)
        {
            lotusTilemap.SetTile(cell, null);
        }
        else if (newLogic == LogicTileType.Water)
        {
            TileBase waterTile = GetWaterTileBase(index);
            lotusTilemap.SetTile(cell, waterTile);
        }
    }

    private TileBase GetWaterTileBase(int mapIndex)
    {
        if (_packLookup != null)
        {
            if (runtimeRefMap != null && mapIndex < runtimeRefMap.Length)
            {
                var tref = runtimeRefMap[mapIndex];
                if (!tref.IsEmpty && _packLookup.TryGetValue(tref.packName, out var originPack))
                {
                    foreach (var e in originPack.entries)
                        if (e != null && e.logicType == LogicTileType.Water && e.tile != null)
                            return e.tile;
                }
            }
            foreach (var pack in _packLookup.Values)
                foreach (var e in pack.entries)
                    if (e != null && e.logicType == LogicTileType.Water && e.tile != null)
                        return e.tile;
        }
        return null;
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
        if (!showHighlight) ClearHighlights();
        else HighlightValidMoves(frogInstance.currentTile);
    }

    public void ToggleSetting()
    {
        if (isSetting) Time.timeScale = 1f;
        else Time.timeScale = 0f;
        SettingPanel.gameObject.SetActive(!isSetting);
        isSetting = !isSetting;
    }

    public void BackToLevelSelect()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LevelSelectScene");
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MenuScene");
    }

    // ===================== GENERATOR INNER METHODS =====================

    private bool TryGenerateRandomPath(int w, int h, int steps, out List<Vector2Int> path)
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

    private bool GenerateDFS(Vector2Int pos, int steps, int w, int h, List<Vector2Int> path, HashSet<Vector2Int> used)
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

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    private TileRef FindTileRefByLogic(LogicTileType logic)
    {
        if (_packLookup != null)
        {
            foreach (var pack in _packLookup.Values)
            {
                if (pack == null) continue;
                foreach (var entry in pack.entries)
                {
                    if (entry != null && entry.logicType == logic && !string.IsNullOrEmpty(entry.id))
                        return new TileRef(pack.packName, entry.id);
                }
            }
        }
        return TileRef.Empty;
    }

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
