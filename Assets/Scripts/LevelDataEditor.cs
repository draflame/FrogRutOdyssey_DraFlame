#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    public enum PaintMode
    {
        Grass,      // Vẽ tile Grass (walkable — lót chính mà frog cần đi qua)
        Empty,      // Xóa tile
        Start,      // Đặt vị trí bắt đầu
        TilePack    // Chọn tile từ TilePack (format mới)
    }

    private PaintMode currentMode = PaintMode.Grass;
    private int cellSize = 28;

    // ── TilePack selection state ──
    private int selectedPackIndex = 0;
    private int selectedTileIndex = 0;
    private string selectedPackName = "";
    private string selectedTileId = "";

    // Cache của pack names và tile IDs hiện tại
    private string[] _cachedPackNames = new string[0];
    private string[] _cachedTileIds = new string[0];
    private Color[] _cachedTileColors = new Color[0];

    public override void OnInspectorGUI()
    {
        LevelData level = (LevelData)target;

        DrawDefaultInspector();

        GUILayout.Space(10);
        GUILayout.Label("🗺 LEVEL EDITOR", EditorStyles.boldLabel);

        // ── Mode toolbar ──
        string[] modeLabels = { "Grass", "Empty", "Start", "TilePack" };
        currentMode = (PaintMode)GUILayout.Toolbar((int)currentMode, modeLabels);

        // ── TilePack brush picker ──
        if (currentMode == PaintMode.TilePack)
        {
            GUILayout.Space(5);
            DrawTilePackPicker(level);
        }

        GUILayout.Space(10);

        // ── Buttons ──
        if (GUILayout.Button("Resize Map"))
        {
            Resize(level, level.width, level.height);
        }

        if (GUILayout.Button("Clear Map"))
        {
            Undo.RecordObject(level, "Clear Map");
            if (level.UsesTileRefFormat)
            {
                level.tileRefMap = new TileRef[level.width * level.height];
            }
            else
            {
                for (int i = 0; i < level.map.Length; i++)
                    level.map[i] = TileType.Empty;
            }
        }

        if (GUILayout.Button("Check Solvable"))
        {
            if (!LevelValidator.IsStartValid(level))
                Debug.LogWarning("[CheckSolvable] Start tile is not valid!");
            else
            {
                bool solvable = KnightTourGenerator.IsSolvable(level);
                if (solvable) Debug.Log("[CheckSolvable] ✅ Level CAN be solved!");
                else Debug.LogWarning("[CheckSolvable] ❌ Level CANNOT be solved.");
            }
        }

        if (GUILayout.Button("Fill Interior"))
        {
            FillInterior(level);
        }

        if (GUILayout.Button("Generate Tour"))
        {
            if (LevelValidator.IsStartValid(level))
            {
                Undo.RecordObject(level, "Generate Tour");
                var path = KnightTourGenerator.Generate(level);
                level.solutionPath = path.ToArray();

                // Đổi các tile trong path thành Lotus (kể cả start)
                ApplyPathAsLotus(level, path);

                EditorUtility.SetDirty(level);
                Debug.Log($"[GenerateTour] Path length: {path.Count}. Đã gán Lotus cho toàn bộ path.");
            }
            else
            {
                Debug.LogWarning("Start tile is not valid!");
            }
        }

        GUILayout.Space(8);

        // ── Convert button ──
        DrawConvertSection(level);

        GUILayout.Space(10);

        DrawGrid(level);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(level);
        }
    }

    // ─────────────────────────────────────────────────────
    //  TilePack Picker
    // ─────────────────────────────────────────────────────

    private void DrawTilePackPicker(LevelData level)
    {
        if (level.localPacks == null || level.localPacks.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Không có TilePack nào. Thêm TilePack vào mục 'Local Packs' của LevelData.",
                MessageType.Warning);
            return;
        }

        // Collect valid packs
        var validPacks = new List<TilePack>();
        var validPackNames = new List<string>();
        foreach (var p in level.localPacks)
        {
            if (p == null || string.IsNullOrEmpty(p.packName)) continue;
            validPacks.Add(p);
            validPackNames.Add(p.packName);
        }

        if (validPacks.Count == 0)
        {
            EditorGUILayout.HelpBox("Các TilePack chưa có tên. Điền packName vào mỗi pack.", MessageType.Warning);
            return;
        }

        _cachedPackNames = validPackNames.ToArray();

        // Clamp selected pack index
        selectedPackIndex = Mathf.Clamp(selectedPackIndex, 0, validPacks.Count - 1);
        int newPackIdx = EditorGUILayout.Popup("Pack", selectedPackIndex, _cachedPackNames);
        if (newPackIdx != selectedPackIndex)
        {
            selectedPackIndex = newPackIdx;
            selectedTileIndex = 0;
        }

        TilePack chosenPack = validPacks[selectedPackIndex];
        selectedPackName = chosenPack.packName;

        // Collect tile IDs from pack (grouped by category)
        if (chosenPack.entries != null && chosenPack.entries.Length > 0)
        {
            var ids = new List<string>();
            var colors = new List<Color>();
            foreach (var e in chosenPack.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                ids.Add($"[{e.category}] {e.id}");
                colors.Add(e.editorColor);
            }
            _cachedTileIds = ids.ToArray();
            _cachedTileColors = colors.ToArray();

            selectedTileIndex = Mathf.Clamp(selectedTileIndex, 0, ids.Count - 1);
            selectedTileIndex = EditorGUILayout.Popup("Tile", selectedTileIndex, _cachedTileIds);

            // Extract plain tileId (remove "[Category] " prefix)
            if (selectedTileIndex < chosenPack.entries.Length)
            {
                var rawEntry = GetEntryAtFilteredIndex(chosenPack, selectedTileIndex);
                selectedTileId = rawEntry?.id ?? "";
            }

            // Color preview
            if (_cachedTileColors.Length > 0 && selectedTileIndex < _cachedTileColors.Length)
            {
                var previewRect = EditorGUILayout.GetControlRect(GUILayout.Height(18));
                EditorGUI.DrawRect(previewRect, _cachedTileColors[selectedTileIndex]);
                EditorGUI.LabelField(previewRect, $"  {selectedPackName}/{selectedTileId}",
                    new GUIStyle(EditorStyles.label) { normal = { textColor = Color.white } });
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"Pack '{chosenPack.packName}' chưa có tile nào.", MessageType.Info);
        }
    }

    private TilePackEntry GetEntryAtFilteredIndex(TilePack pack, int filteredIndex)
    {
        int count = 0;
        foreach (var e in pack.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            if (count == filteredIndex) return e;
            count++;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────
    //  Migration: Convert Old → TileRef
    // ─────────────────────────────────────────────────────

    private void DrawConvertSection(LevelData level)
    {
        GUILayout.Label("── Migration ──", EditorStyles.miniLabel);

        if (!level.UsesTileRefFormat)
        {
            EditorGUILayout.HelpBox(
                "Level này đang dùng format CŨ (TileType enum).\n" +
                "Nhấn 'Convert → TileRef' để migrate sang format mới.",
                MessageType.Info);

            if (GUILayout.Button("Convert → TileRef Format"))
            {
                ConvertToTileRef(level);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "✅ Level đang dùng TileRef format (mới).",
                MessageType.None);
        }
    }

    /// <summary>
    /// Convert TileType[] map cũ sang TileRef[] tileRefMap.
    /// Logic/Walkable tiles (Lotus/Grass/Water) → cần pack có tile tương ứng.
    /// Tile "Empty" → TileRef.Empty.
    /// </summary>
    private void ConvertToTileRef(LevelData level)
    {
        if (level.map == null || level.map.Length == 0)
        {
            Debug.LogWarning("[Convert] Map cũ trống, không có gì để convert.");
            return;
        }

        if (level.localPacks == null || level.localPacks.Length == 0)
        {
            Debug.LogWarning("[Convert] Cần ít nhất 1 TilePack trong 'Local Packs' để map tile sang pack.");
            return;
        }

        Undo.RecordObject(level, "Convert to TileRef");
        level.tileRefMap = new TileRef[level.map.Length];

        // Build reverse lookup: TileType name → (packName, tileId) từ localPacks
        var legacyMap = new Dictionary<string, (string pack, string id)>();
        foreach (var pack in level.localPacks)
        {
            if (pack == null) continue;
            pack.BuildCache();
            foreach (var entry in pack.entries)
            {
                if (entry == null) continue;
                // Tên entry khớp với tên TileType → map được
                string key = entry.id.ToLower();
                if (!legacyMap.ContainsKey(key))
                    legacyMap[key] = (pack.packName, entry.id);
            }
        }

        int mapped = 0, unmapped = 0;
        for (int i = 0; i < level.map.Length; i++)
        {
            TileType t = level.map[i];
            if (t == TileType.Empty)
            {
                level.tileRefMap[i] = TileRef.Empty;
                continue;
            }

            string key = t.ToString().ToLower();
            if (legacyMap.TryGetValue(key, out var match))
            {
                level.tileRefMap[i] = new TileRef(match.pack, match.id);
                mapped++;
            }
            else
            {
                // Không tìm được pack tương ứng → để trống và warn
                level.tileRefMap[i] = TileRef.Empty;
                unmapped++;
            }
        }

        EditorUtility.SetDirty(level);
        Debug.Log($"[Convert] ✅ Xong. Mapped: {mapped}, Unmapped (→ Empty): {unmapped}");
        Debug.Log("[Convert] Kiểm tra các ô bị unmapped và gán thủ công trong Editor nếu cần.");
    }

    // ─────────────────────────────────────────────────────
    //  Grid Drawing
    // ─────────────────────────────────────────────────────

    void Resize(LevelData level, int newW, int newH)
    {
        Undo.RecordObject(level, "Resize Map");
        level.Resize(newW, newH);
    }

    void FillInterior(LevelData level)
    {
        Undo.RecordObject(level, "Fill Interior");

        // Thuật toán flood-fill từ biên (exterior) → những ô không đủ rộng là interior
        // Bước 1: Mark tất cả ô kết nối với biên (exterior flood-fill)
        bool[,] isExterior = new bool[level.width, level.height];
        var queue = new Queue<Vector2Int>();

        // Seed: tất cả ô rộng (đưa vào Empty) nằm TRÊN BIÊN
        for (int x = 0; x < level.width; x++)
        {
            TrySeedExterior(level, x, 0, isExterior, queue);
            TrySeedExterior(level, x, level.height - 1, isExterior, queue);
        }
        for (int y = 1; y < level.height - 1; y++)
        {
            TrySeedExterior(level, 0, y, isExterior, queue);
            TrySeedExterior(level, level.width - 1, y, isExterior, queue);
        }

        // Bước 2: BFS flood-fill ra ngoài
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + dx[d];
                int ny = cur.y + dy[d];
                if (nx < 0 || ny < 0 || nx >= level.width || ny >= level.height) continue;
                if (isExterior[nx, ny]) continue;

                LogicTileType logic = level.GetLogic(nx, ny);
                // Chỉ spread qua ô rộng (Empty) — không vượt qua Grass/Water/Lotus
                if (logic != LogicTileType.Empty) continue;

                isExterior[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // Bước 3: Fill Water vào tất cả ô Interior (Empty + không phải exterior)
        TileRef waterRef = FindWaterTileRef(level);

        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                if (isExterior[x, y]) continue;

                LogicTileType logic = level.GetLogic(x, y);
                if (logic != LogicTileType.Empty) continue; // đã có tile

                if (level.UsesTileRefFormat)
                    level.SetRef(x, y, waterRef);
                else
                    level.Set(x, y, TileType.Water);
            }
        }

        Debug.Log("[FillInterior] Đã fill Water vào phần interior của map.");
    }

    /// <summary>Seed một ô vào exterior flood-fill nếu nó là Empty.</summary>
    private void TrySeedExterior(LevelData level, int x, int y, bool[,] isExterior, Queue<Vector2Int> queue)
    {
        if (isExterior[x, y]) return;
        LogicTileType logic = level.GetLogic(x, y);
        if (logic != LogicTileType.Empty) return; // ô Grass/Water/Lotus làm biên — không seed
        isExterior[x, y] = true;
        queue.Enqueue(new Vector2Int(x, y));
    }

    // ─────────────────────────────────────────────────────
    //  Apply Path as Lotus
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Gán Lotus cho tất cả tile trong path (kể cả start tile).
    /// Hỗ trợ cả format cũ (TileType) và mới (TileRef).
    /// </summary>
    private void ApplyPathAsLotus(LevelData level, List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) return;

        // Đảm bảo start tile cũng là Lotus
        var allLotus = new HashSet<Vector2Int>(path);
        allLotus.Add(level.startTile);

        if (level.UsesTileRefFormat)
        {
            TileRef lotusRef = FindLotusRef(level);
            if (lotusRef.IsEmpty)
            {
                Debug.LogWarning("[GenerateTour] Không tìm thấy Lotus TileRef trong localPacks. "
                    + "Thêm entry có logicType=Lotus vào một pack trong 'Local Packs'.");
                return;
            }
            foreach (var pos in allLotus)
                level.SetRef(pos.x, pos.y, lotusRef);
        }
        else
        {
            foreach (var pos in allLotus)
                level.Set(pos.x, pos.y, TileType.Lotus);
        }
    }

    /// <summary>Tìm TileRef đầu tiên có logicType == Lotus trong localPacks.</summary>
    private TileRef FindLotusRef(LevelData level)
    {
        if (level.localPacks == null) return TileRef.Empty;
        foreach (var pack in level.localPacks)
        {
            if (pack == null) continue;
            foreach (var entry in pack.entries)
                if (entry != null && entry.logicType == LogicTileType.Lotus && !string.IsNullOrEmpty(entry.id))
                    return new TileRef(pack.packName, entry.id);
        }
        return TileRef.Empty;
    }

    /// <summary>Tìm TileRef đầu tiên có logicType == Water trong localPacks (dùng cho FillInterior).</summary>
    private TileRef FindWaterTileRef(LevelData level)
    {
        if (level.localPacks == null) return TileRef.Empty;
        foreach (var pack in level.localPacks)
        {
            if (pack == null) continue;
            foreach (var entry in pack.entries)
                if (entry != null && entry.logicType == LogicTileType.Water && !string.IsNullOrEmpty(entry.id))
                    return new TileRef(pack.packName, entry.id);
        }
        return TileRef.Empty;
    }

    void DrawGrid(LevelData level)
    {
        bool usesNewFormat = level.UsesTileRefFormat;

        if (usesNewFormat && (level.tileRefMap == null || level.tileRefMap.Length != level.width * level.height))
            level.tileRefMap = new TileRef[level.width * level.height];

        if (!usesNewFormat && (level.map == null || level.map.Length != level.width * level.height))
            level.map = new TileType[level.width * level.height];

        Event e = Event.current;

        for (int y = level.height - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();

            for (int x = 0; x < level.width; x++)
            {
                Rect rect = GUILayoutUtility.GetRect(cellSize, cellSize);

                // ── Lấy màu nền tile ──
                Color bgColor;
                string labelText = "";
                if (usesNewFormat)
                {
                    TileRef tref = level.GetRef(x, y);
                    bgColor = ResolveTileColor(tref, level);
                    if (!tref.IsEmpty) labelText = tref.tileId;
                }
                else
                {
                    TileType tile = level.Get(x, y);
                    bgColor = GetLegacyColor(tile);
                }

                EditorGUI.DrawRect(rect, bgColor);

                // ── Viền đen ──
                Handles.color = Color.black;
                Handles.DrawAAPolyLine(2, new Vector3[]
                {
                    new Vector3(rect.x, rect.y),
                    new Vector3(rect.x + rect.width, rect.y),
                    new Vector3(rect.x + rect.width, rect.y + rect.height),
                    new Vector3(rect.x, rect.y + rect.height),
                    new Vector3(rect.x, rect.y)
                });

                // ── Start tile highlight ──
                if (level.startTile.x == x && level.startTile.y == y)
                {
                    EditorGUI.DrawRect(rect, new Color(1f, 0f, 0f, 0.35f));
                    Handles.color = Color.white;
                    Handles.DrawAAPolyLine(3, new Vector3[]
                    {
                        new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y),
                        new Vector3(rect.xMax, rect.yMax), new Vector3(rect.x, rect.yMax),
                        new Vector3(rect.x, rect.y)
                    });
                    GUIStyle sStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white },
                        fontSize = 9
                    };
                    GUI.Label(rect, "S", sStyle);
                }
                else if (!string.IsNullOrEmpty(labelText))
                {
                    // Hiện tên tile viết tắt (3 ký tự đầu)
                    GUIStyle lStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white },
                        fontSize = 7
                    };
                    string abbr = labelText.Length > 4 ? labelText.Substring(0, 4) : labelText;
                    GUI.Label(rect, abbr, lStyle);
                }

                // ── Solution path numbers ──
                if (level.solutionPath != null && level.solutionPath.Length > 0)
                {
                    int pathIndex = System.Array.IndexOf(level.solutionPath, new Vector2Int(x, y));
                    if (pathIndex >= 0)
                    {
                        GUIStyle pathStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.yellow },
                            fontSize = 8
                        };
                        GUI.Label(rect, pathIndex.ToString(), pathStyle);
                    }
                }

                // ── Input handling ──
                if (rect.Contains(e.mousePosition))
                {
                    if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                    {
                        Undo.RecordObject(level, "Paint Tile");
                        HandleLeftClick(level, x, y);
                        GUI.changed = true;
                        e.Use();
                    }
                    if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 1)
                    {
                        Undo.RecordObject(level, "Erase Tile");
                        EraseTile(level, x, y);
                        GUI.changed = true;
                        e.Use();
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        GUI.color = Color.white;
    }

    private void HandleLeftClick(LevelData level, int x, int y)
    {
        switch (currentMode)
        {
            case PaintMode.Start:
                level.startTile = new Vector2Int(x, y);
                break;

            case PaintMode.Empty:
                EraseTile(level, x, y);
                break;

            case PaintMode.Grass:
                // Paint Grass walkable: format mới dùng TileRef từ pack;
                // format cũ set TileType.Grass
                if (level.UsesTileRefFormat)
                {
                    TileRef grassRef = FindFirstGrassInPacks(level);
                    level.SetRef(x, y, grassRef.IsEmpty ? TileRef.Empty : grassRef);
                }
                else
                {
                    level.Set(x, y, TileType.Grass);
                }
                break;

            case PaintMode.TilePack:
                if (!string.IsNullOrEmpty(selectedPackName) && !string.IsNullOrEmpty(selectedTileId))
                {
                    // Đảm bảo tileRefMap được khởi tạo
                    if (level.tileRefMap == null || level.tileRefMap.Length != level.width * level.height)
                        level.tileRefMap = new TileRef[level.width * level.height];
                    level.SetRef(x, y, new TileRef(selectedPackName, selectedTileId));
                }
                break;
        }
    }

    private void EraseTile(LevelData level, int x, int y)
    {
        if (level.UsesTileRefFormat)
            level.SetRef(x, y, TileRef.Empty);
        else
            level.Set(x, y, TileType.Empty);
    }

    // ─────────────────────────────────────────────────────
    //  Color helpers
    // ─────────────────────────────────────────────────────

    private Color ResolveTileColor(TileRef tref, LevelData level)
    {
        if (tref.IsEmpty) return new Color(0.08f, 0.08f, 0.08f);

        if (level.localPacks != null)
        {
            foreach (var pack in level.localPacks)
            {
                if (pack == null || pack.packName != tref.packName) continue;
                var entry = pack.Get(tref.tileId);
                if (entry != null) return entry.editorColor;
            }
        }

        // Fallback màu theo logic
        return new Color(0.5f, 0.5f, 0.5f);
    }

    private TileRef FindFirstLotusInPacks(LevelData level)
    {
        if (level.localPacks == null) return TileRef.Empty;
        foreach (var pack in level.localPacks)
        {
            if (pack == null) continue;
            foreach (var entry in pack.entries)
            {
                if (entry != null && entry.logicType == LogicTileType.Lotus)
                    return new TileRef(pack.packName, entry.id);
            }
        }
        return TileRef.Empty;
    }

    /// <summary>
    /// Tìm tile đầu tiên có logicType == Grass (hoặc Lotus) trong localPacks.
    /// Dùng cho PaintMode.Grass — vẽ tile nền đất chính của map.
    /// </summary>
    private TileRef FindFirstGrassInPacks(LevelData level)
    {
        if (level.localPacks == null) return TileRef.Empty;
        // Ưu tiên tìm Grass trước
        foreach (var pack in level.localPacks)
        {
            if (pack == null) continue;
            foreach (var entry in pack.entries)
                if (entry != null && entry.logicType == LogicTileType.Grass)
                    return new TileRef(pack.packName, entry.id);
        }
        // Fallback: Lotus cũng là walkable
        return FindFirstLotusInPacks(level);
    }

    private Color GetLegacyColor(TileType tile)
    {
        switch (tile)
        {
            case TileType.Empty:   return new Color(0.08f, 0.08f, 0.08f);
            case TileType.Lotus:   return new Color(0.4f, 1f, 0.6f);
            case TileType.Water:   return new Color(0.2f, 0.5f, 0.95f);
            case TileType.Grass:
            case TileType.Grass2:
            case TileType.SmallGrass:
            case TileType.SmallGrass2:
            case TileType.GrassLeaf:
            case TileType.TopLeftGrass:
            case TileType.TopStoneGrass:
            case TileType.MudWithGrass:
                return new Color(0.2f, 0.7f, 0.3f);
            case TileType.Tree:
            case TileType.TallTree:
            case TileType.LargeTree:
            case TileType.Bush:
            case TileType.Stump:
                return new Color(0.1f, 0.45f, 0.2f);
            case TileType.Wave:
            case TileType.WaterFall:
            case TileType.WaterFallBottom:
            case TileType.StoneWater:
            case TileType.FishingBobber:
            case TileType.Jetty:
            case TileType.LotusFlower:
            case TileType.SmallLeaf:
                return new Color(0.3f, 0.6f, 0.95f);
            case TileType.SmallStone:
            case TileType.SmallStone2:
            case TileType.StoneGrass:
            case TileType.Cattail:
            case TileType.Reed:
            case TileType.BlueFlower:
            case TileType.RedMushroom:
            case TileType.BrownMushroom:
            case TileType.Pumkin:
                return new Color(0.7f, 0.7f, 0.7f);
            case TileType.PathVertical:
            case TileType.PathHorizontal:
            case TileType.PathCornerBL:
            case TileType.PathCornerBR:
            case TileType.PathCornerTL:
            case TileType.PathCornerTR:
            case TileType.PathIntersection:
            case TileType.PathTJunctionUp:
            case TileType.PathTJunctionDown:
            case TileType.PathTJunctionLeft:
            case TileType.PathTJunctionRight:
            case TileType.RuinedPath:
                return new Color(0.6f, 0.4f, 0.2f);
            default:
                return new Color(0.8f, 0.3f, 0.9f);
        }
    }
}
#endif