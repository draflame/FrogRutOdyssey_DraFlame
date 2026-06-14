using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TutorialGameController : MonoBehaviour, IGameController
{
    [Header("Level")]
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private ToturialManager tutorialManager;
    
    private int currentLevelIndex;
    private LevelData currentLevel;

    [Header("Tilemap")]
    [SerializeField] private Tilemap lotusTilemap;

    [Header("Tile Packs")]
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

    [Header("Camera")]
    [SerializeField] private CinemachineMapFitter camFitter;

    private HashSet<Vector2Int> visitedTiles = new();
    private LogicTileType[] runtimeLogicMap;
    private TileRef[] runtimeRefMap;
    private int totalWalkableTiles;
    private Stack<MoveRecord> moveHistory = new();

    [Header("Setting Panel")]
    [SerializeField] private GameObject SettingPanel;
    [SerializeField] private GameObject OnGamePanel;
    private bool isSetting = false;

    public bool IsPlayable { get; set; } = false;

    private readonly Vector2Int[] knightMoves =
    {
        new(1,2), new(2,1), new(-1,2), new(-2,1),
        new(1,-2), new(2,-1), new(-1,-2), new(-2,-1)
    };

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
            }
        }
    }

    private void Start()
    {
        if (tutorialManager == null)
        {
            tutorialManager = Object.FindAnyObjectByType<ToturialManager>();
        }

        // Đồng bộ highlight với SettingManager nếu có
        if (SettingManager.Instance != null)
        {
            showHighlight = SettingManager.Instance.IsHighlightOn;
        }
        
        if (camFitter != null)
        {
            camFitter.FitToTilemap();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[TutorialGameController] Click detected. IsPlayable={IsPlayable}");
        }

        if (IsPlayable && Input.GetMouseButtonDown(0))
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

        Debug.Log($"[TutorialGameController] Trying to move frog to tile={target}");
        TryMoveFrog(target);
    }

    private void TryMoveFrog(Vector2Int target)
    {
        if (frogInstance == null)
        {
            Debug.LogWarning("[TutorialGameController] Cannot move frog: frogInstance is null!");
            return;
        }
        frogInstance.TryMove(target);
    }

    public void ToggleSetting()
    {
        if (isSetting) Time.timeScale = 1f;
        else Time.timeScale = 0f;

        if (SettingPanel != null)
            SettingPanel.SetActive(!isSetting);
        
        isSetting = !isSetting;
        Debug.Log($"[TutorialGameController] ToggleSetting settingPanel active={isSetting}");
    }

    public void CloseSetting()
    {
        isSetting = false;
        Time.timeScale = 1f;
        if (SettingPanel != null)
            SettingPanel.SetActive(false);
        Debug.Log("[TutorialGameController] CloseSetting called");
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

    public void LoadLevel(int index)
    {
        if (levelDatabase == null || levelDatabase.levels == null || levelDatabase.levels.Count == 0)
        {
            Debug.LogError("No levels assigned in database!");
            return;
        }

        Time.timeScale = 1f;
        currentLevelIndex = Mathf.Clamp(index, 0, levelDatabase.levels.Count - 1);
        currentLevel = levelDatabase.levels[currentLevelIndex];

        // ── Load map runtime ──────────────────────────────────
        runtimeRefMap = currentLevel.tileRefMap != null ? (TileRef[])currentLevel.tileRefMap.Clone() : new TileRef[currentLevel.width * currentLevel.height];
        runtimeLogicMap = BuildLogicMap(currentLevel, runtimeRefMap);

        // Đếm số tile phải đi qua (Lotus)
        totalWalkableTiles = 0;
        foreach (var t in runtimeLogicMap)
        {
            if (t == LogicTileType.Lotus)
                totalWalkableTiles++;
        }

        visitedTiles.Clear();
        lotusTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();
        moveHistory.Clear();

        // Đồng bộ highlight từ SettingManager
        if (SettingManager.Instance != null)
            showHighlight = SettingManager.Instance.IsHighlightOn;

        DrawMap();
        SpawnFrog();
        
        if (camFitter != null)
            camFitter.FitToTilemap();

        IsPlayable = true;
    }

    public void RecordMove(Vector2Int from, Vector2Int to)
    {
        moveHistory.Push(new MoveRecord { from = from, to = to });
    }

    public void Undo()
    {
        if (moveHistory.Count == 0 || !IsPlayable) return;
        Time.timeScale = 1f;

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

    public LogicTileType GetLogicTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= currentLevel.width || y >= currentLevel.height)
            return LogicTileType.Empty;
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

    public Vector3 GetWorldPosition(Vector2Int tile)
    {
        Vector3Int cell = new Vector3Int(tile.x, tile.y, 0);
        Vector3 pos = lotusTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        return pos;
    }

    public bool CanMove(Vector2Int from, Vector2Int to)
    {
        if (!IsPlayable) return false;
        if (!IsInsideMap(to)) return false;

        LogicTileType tile = GetLogicTile(to.x, to.y);
        if (tile != LogicTileType.Lotus) return false;
        if (visitedTiles.Contains(to)) return false;

        Vector2Int d = to - from;
        int dx = Mathf.Abs(d.x);
        int dy = Mathf.Abs(d.y);
        return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
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
        else if (frogInstance != null) HighlightValidMoves(frogInstance.currentTile);

        // Đồng bộ ngược lại cho HighLightButton ngoài màn chơi (nếu có)
        HighLightButton hBtn = Object.FindAnyObjectByType<HighLightButton>();
        if (hBtn != null) hBtn.SetHighLight(showHighlight);
    }

    public void SetHighlight(bool value)
    {
        showHighlight = value;
        if (!showHighlight) ClearHighlights();
        else if (frogInstance != null) HighlightValidMoves(frogInstance.currentTile);

        // Đồng bộ ngược lại cho HighLightButton ngoài màn chơi (nếu có)
        HighLightButton hBtn = Object.FindAnyObjectByType<HighLightButton>();
        if (hBtn != null) hBtn.SetHighLight(showHighlight);
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

    public void OnFrogMoved(Vector2Int currentTile)
    {
        HighlightValidMoves(currentTile);

        // Win check
        if (visitedTiles.Count + 1 == totalWalkableTiles)
        {
            Win();
            return;
        }

        // Lose check
        foreach (var move in knightMoves)
        {
            if (CanMove(currentTile, currentTile + move))
                return;
        }

        GameOver();
    }

    private void Win()
    {
        IsPlayable = false;
        ClearHighlights();
        if (tutorialManager != null)
        {
            tutorialManager.OnLevelCompleted();
        }
    }

    private void GameOver()
    {
        IsPlayable = false;
        if (tutorialManager != null)
        {
            tutorialManager.OnLevelFailed();
        }
        else
        {
            PlayAgain();
        }
    }

    public void PlayAgain()
    {
        ClearHighlights();
        if (frogInstance != null)
        {
            Destroy(frogInstance.gameObject);
            frogInstance = null;
        }
        LoadLevel(currentLevelIndex);
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
