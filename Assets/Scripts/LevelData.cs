using UnityEngine;

/// <summary>
/// ScriptableObject chứa dữ liệu một màn chơi.
///
/// MIGRATION NOTE:
///   - `map` (TileType[])  — format CŨ, giữ lại cho backward-compatibility với level đã tạo.
///   - `tileRefMap` (TileRef[]) — format MỚI dùng TilePack system.
///   Gọi nút "Convert → TileRef" trong Inspector để chuyển level cũ sang format mới.
///   Khi `tileRefMap` đã được populate, GameController sẽ ưu tiên dùng nó.
/// </summary>
[CreateAssetMenu(
    fileName = "LevelData",
    menuName = "KnightTour/Level Data"
)]
public class LevelData : ScriptableObject
{
    [Header("Grid")]
    public int width;
    public int height;

    // ──────────────────────────────────────────────────
    //  FORMAT MỚI — TilePack System
    // ──────────────────────────────────────────────────
    [Header("Map — TilePack Format (New)")]
    [Tooltip("Map dữ liệu mới dùng TilePack system. Nếu đã populate, GameController sẽ dùng cái này.")]
    public TileRef[] tileRefMap;

    [Tooltip("Danh sách các TilePack được sử dụng trong level này (gợi ý cho Editor).")]
    public TilePack[] localPacks;



    // ──────────────────────────────────────────────────
    //  Thông tin chung
    // ──────────────────────────────────────────────────
    [Header("Start Position")]
    public Vector2Int startTile;

    [Header("Generator")]
    public Difficulty difficulty;
    public Vector2Int[] solutionPath;

#if UNITY_EDITOR
    private void OnEnable()
    {
        // Khởi tạo mặc định cho level mới tạo
        if (width == 0) width = 8;
        if (height == 0) height = 8;

        if (tileRefMap == null || tileRefMap.Length == 0)
        {
            tileRefMap = new TileRef[width * height];
        }

        // Tự động tải tất cả các TilePack trong Project vào localPacks nếu chưa có
        if (localPacks == null || localPacks.Length == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TilePack");
            if (guids != null && guids.Length > 0)
            {
                var list = new System.Collections.Generic.List<TilePack>();
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var pack = UnityEditor.AssetDatabase.LoadAssetAtPath<TilePack>(path);
                    if (pack != null)
                    {
                        list.Add(pack);
                    }
                }
                localPacks = list.ToArray();
            }
        }
    }
#endif

    // ──────────────────────────────────────────────────
    //  Helpers — TileRef Map Methods
    // ──────────────────────────────────────────────────

    public TileRef GetRef(int x, int y)
    {
        if (tileRefMap == null) return TileRef.Empty;
        return tileRefMap[y * width + x];
    }

    public void SetRef(int x, int y, TileRef tref)
    {
        if (tileRefMap == null || tileRefMap.Length != width * height)
            tileRefMap = new TileRef[width * height];
        tileRefMap[y * width + x] = tref;
    }

    /// <summary>
    /// Trả về LogicTileType của ô (x,y) — dùng cho solver, pathfinding.
    /// </summary>
    public LogicTileType GetLogic(int x, int y)
    {
        TileRef tref = GetRef(x, y);
        if (tref.IsEmpty) return LogicTileType.Empty;

        // Tra trong localPacks để lấy logicType
        if (localPacks != null)
        {
            foreach (var pack in localPacks)
            {
                if (pack == null || pack.packName != tref.packName) continue;
                var entry = pack.Get(tref.tileId);
                if (entry != null) return entry.logicType;
            }
        }
        // Không tìm được pack – giả định là Grass (không walkable)
        return LogicTileType.Grass;
    }

    // ──────────────────────────────────────────────────
    //  Resize helper (dùng bởi Editor)
    // ──────────────────────────────────────────────────

    public void Resize(int newW, int newH)
    {
        int oldW = width, oldH = height;

        width = newW;
        height = newH;

        // Resize tileRefMap nếu có
        if (tileRefMap != null && tileRefMap.Length > 0)
        {
            var oldRef = tileRefMap;
            tileRefMap = new TileRef[newW * newH];
            for (int y = 0; y < Mathf.Min(oldH, newH); y++)
                for (int x = 0; x < Mathf.Min(oldW, newW); x++)
                    tileRefMap[y * newW + x] = oldRef[y * oldW + x];
        }
        else
        {
            tileRefMap = new TileRef[newW * newH];
        }
    }
}

// ──────────────────────────────────────────────────────────────────
//  TileType enum — GIỮ NGUYÊN để không break các level .asset cũ
//  Không thêm giá trị mới vào đây nữa — dùng TilePack thay thế.
// ──────────────────────────────────────────────────────────────────
public enum TileType
{
    Empty = 0,
    Lotus = 1,
    Grass = 2,
    Water = 3,
    StoneWater = 4,
    Tree = 5,
    LotusFlower = 6,
    SmallLeaf = 7,
    StoneGrass = 8,
    Cattail = 9,
    Bush = 10,
    Wave = 11,
    SmallStone = 12,
    Stump = 13,
    GrassLeaf = 14,
    RuinedFence = 15,
    TallTree = 16,
    LargeTree = 17,
    TopStoneGrass = 18,
    SmallGrass = 19,
    TopLeftGrass = 20,
    Pumkin = 21,
    Sword = 22,
    RedMushroom = 23,
    BrownMushroom = 24,
    CampFire = 25,
    CampFireWithLogs = 26,
    RuinedPath = 27,
    BlueFlower = 28,
    Reed = 29,
    Grass2 = 30,
    SmallStone2 = 31,
    SmallGrass2 = 32,
    Mud = 33,
    CrackedGround = 34,
    MudWithGrass = 35,
    Catus = 36,
    CactusFlower = 37,
    WaterFall = 38,
    WaterFallBottom = 39,
    Jetty = 40,
    FishingBobber = 41,
    PathVertical = 42,
    PathHorizontal = 43,
    PathCornerBL = 44,
    PathCornerBR = 45,
    PathCornerTL = 46,
    PathCornerTR = 47,
    PathIntersection = 48,
    PathTJunctionUp = 49,
    PathTJunctionDown = 50,
    PathTJunctionLeft = 51,
    PathTJunctionRight = 52,
    Autumn = 53,
    AutumnBush = 54,
    AutumnSmallStone = 55,
    AutumnStone = 56,
    AutumnStump = 57,
    AutumnLeaf = 58,
    AutumnSmallBush = 59,
    AutumnBerryBush = 60,
    AutumnFlowerBush = 61,
    AutumnTree = 62,
    AutumnFence = 63,
    AutumnFence2 = 64,
    WhiteLotus = 65,
    Mooring = 66,
    SankBoat = 67,
    Weed = 68,
    Coral = 69,
    Driftwood = 70,
    Driftwood2 = 71,
    Jetty2 = 72,
    Jetty3 = 73,
    WoodenFence = 74,
    SmallStone3 = 75,
    Apple = 76,
    Snail = 77,
    WhiteDaisy = 78,
    WoodenBench = 79,
    Daisy = 80,
    WoodenBench2 = 81,
    WoodenFence2 = 82,
    LargeStone = 83,
    DessetCactus = 84,
    Dune = 85,
    Dune2 = 86,
}
