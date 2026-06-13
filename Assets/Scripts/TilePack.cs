using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Một entry trong TilePack — đại diện cho 1 tile với tên tự đặt.
/// </summary>
[System.Serializable]
public class TilePackEntry
{
    [Tooltip("Tên định danh tile, phải UNIQUE trong pack. VD: \"Lotus\", \"BigTree\", \"RedMushroom\"")]
    public string id;

    [Tooltip("Sprite tile dùng trong Unity Tilemap")]
    public TileBase tile;

    [Tooltip("Màu hiển thị trong LevelDataEditor grid")]
    public Color editorColor = Color.gray;

    [Tooltip("Category để nhóm tile trong Editor. VD: \"Walkable\", \"Decoration\", \"Water\", \"Obstacle\"")]
    public string category = "Decoration";

    [Tooltip("True nếu frog có thể đứng lên tile này (tính vào win condition)")]
    public bool isWalkable = false;

    [Tooltip("Logic tương ứng cho solver/pathfinding")]
    public LogicTileType logicType = LogicTileType.Grass;
}

/// <summary>
/// Một bộ tile theo chủ đề (pack). Tạo nhiều pack khác nhau để phân loại tile.
/// Ví dụ: TilePack_Forest, TilePack_Autumn, TilePack_Desert, TilePack_Water...
///
/// Cách tạo: Right-click trong Project → Create → KnightTour → Tile Pack
/// </summary>
[CreateAssetMenu(
    fileName = "TilePack_New",
    menuName = "KnightTour/Tile Pack"
)]
public class TilePack : ScriptableObject
{
    [Tooltip("Tên định danh pack, phải UNIQUE trong project. VD: \"Forest\", \"Autumn\", \"Desert\"")]
    public string packName;

    [Tooltip("Màu nhận dạng pack trong Editor (optional, để dễ phân biệt)")]
    public Color packColor = Color.white;

    public TilePackEntry[] entries = new TilePackEntry[0];

    // ──────────────────────────────────────────────────
    //  Runtime lookup
    // ──────────────────────────────────────────────────
    private Dictionary<string, TilePackEntry> _cache;
    private bool _built = false;

    /// <summary>Gọi lúc Awake của GameController để xây cache.</summary>
    public void BuildCache()
    {
        if (_built) return;
        _cache = new Dictionary<string, TilePackEntry>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            if (!_cache.ContainsKey(e.id))
                _cache.Add(e.id, e);
            else
                Debug.LogWarning($"[TilePack:{packName}] Duplicate tile id \"{e.id}\" — bỏ qua entry trùng.");
        }
        _built = true;
    }

    /// <summary>Trả về TilePackEntry theo id. Null nếu không tìm thấy.</summary>
    public TilePackEntry Get(string id)
    {
        if (!_built) BuildCache();
        return _cache.TryGetValue(id, out var entry) ? entry : null;
    }

    /// <summary>Trả về TileBase sprite theo id. Null nếu không tìm thấy.</summary>
    public TileBase GetTile(string id)
    {
        var entry = Get(id);
        return entry?.tile;
    }

    /// <summary>Trả về tất cả IDs trong pack (dùng cho Editor dropdown).</summary>
    public string[] GetAllIds()
    {
        if (entries == null) return new string[0];
        var ids = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            ids[i] = entries[i]?.id ?? "(null)";
        return ids;
    }

    /// <summary>Lấy category list không trùng (dùng cho Editor grouping).</summary>
    public string[] GetCategories()
    {
        var cats = new System.Collections.Generic.HashSet<string>();
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.category))
                cats.Add(e.category);
        var arr = new string[cats.Count];
        cats.CopyTo(arr);
        System.Array.Sort(arr);
        return arr;
    }

    // Invalidate cache khi asset bị edit trong Editor
    private void OnValidate()
    {
        _built = false;
        _cache = null;
    }
}
