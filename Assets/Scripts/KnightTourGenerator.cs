using System.Collections.Generic;
using UnityEngine;

public class KnightTourGenerator
{
    private static System.Random rng = new System.Random();

    private static readonly Vector2Int[] KnightMoves =
    {
        new Vector2Int(1, 2), new Vector2Int(2, 1),
        new Vector2Int(-1, 2), new Vector2Int(-2, 1),
        new Vector2Int(1, -2), new Vector2Int(2, -1),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1)
    };

    public static List<Vector2Int> Generate(LevelData level)
    {
        // --- Bước 1: Reset Lotus cũ → Water (clear slate) ---
        // Hỗ trợ cả 2 format: TileRef (mới) và TileType (cũ)
        if (level.UsesTileRefFormat)
        {
            // Format mới: dùng GetLogic để detect Lotus, SetRef để đặt Water
            // (Water TileRef sẽ được tìm từ localPacks nếu có, nếu không → Empty)
            TileRef waterRef = FindTileRefByLogic(level, LogicTileType.Water);
            for (int y = 0; y < level.height; y++)
                for (int x = 0; x < level.width; x++)
                    if (level.GetLogic(x, y) == LogicTileType.Lotus)
                        level.SetRef(x, y, waterRef); // reset Lotus → Water/Empty
        }
        else
        {
            // Format cũ: reset trực tiếp qua TileType array
            for (int i = 0; i < level.map.Length; i++)
            {
                TileType t = level.map[i];
                if (t == TileType.Lotus || t == TileType.LotusFlower || t == TileType.SmallLeaf)
                    level.map[i] = TileType.Water;
            }
        }

        // --- Buoc 2: Build graph ---
        var graph = KnightGraphBuilder.BuildGraph(level);
        if (!graph.ContainsKey(level.startTile))
        {
            Debug.LogError("Start tile not found in Knight Graph.");
            return new List<Vector2Int>();
        }

        int totalValid = graph.Count;
        float percentLotuses = 1f;

        switch (level.difficulty)
        {
            case Difficulty.Easy:      percentLotuses = 0.35f; break;
            case Difficulty.Normal:    percentLotuses = 0.50f; break;
            case Difficulty.Hard:      percentLotuses = 0.65f; break;
            case Difficulty.VeryHard:  percentLotuses = 0.80f; break;
            case Difficulty.Expert:    percentLotuses = 0.90f; break;
            case Difficulty.Nightmare: percentLotuses = 1.0f;  break;
        }

        int targetPathLength = Mathf.Max(3, Mathf.CeilToInt(totalValid * percentLotuses));

        // --- Buoc 3: Tim main path bang DFS co backtrack dam bao do dai ---
        var bestPath = new List<Vector2Int>();
        var mainPath = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        bool DFS(KnightNode node)
        {
            mainPath.Add(node.position);
            visited.Add(node.position);

            if (mainPath.Count > bestPath.Count)
                bestPath = new List<Vector2Int>(mainPath);

            if (mainPath.Count >= targetPathLength)
            {
                mainPath.RemoveAt(mainPath.Count - 1);
                visited.Remove(node.position);
                return true;
            }

            var neighbors = new List<KnightNode>(node.neighbors);
            Shuffle(neighbors);

            foreach (var n in neighbors)
            {
                if (!visited.Contains(n.position))
                {
                    if (DFS(n))
                    {
                        mainPath.RemoveAt(mainPath.Count - 1);
                        visited.Remove(node.position);
                        return true;
                    }
                }
            }

            mainPath.RemoveAt(mainPath.Count - 1);
            visited.Remove(node.position);
            return false;
        }

        DFS(graph[level.startTile]);

        var finalPath = bestPath;

        if (finalPath.Count < 2)
        {
            Debug.LogWarning("Could not find a valid path of sufficient length.");
            return finalPath;
        }

        // --- Buoc 4: Chi gan Lotus cho cac o TRONG finalPath (chi Water tiles) ---
        if (level.UsesTileRefFormat)
        {
            TileRef lotusRef = FindTileRefByLogic(level, LogicTileType.Lotus);
            if (!lotusRef.IsEmpty)
            {
                foreach (var pos in finalPath)
                {
                    if (level.GetLogic(pos.x, pos.y) == LogicTileType.Water)
                        level.SetRef(pos.x, pos.y, lotusRef);
                }
            }
        }
        else
        {
            foreach (var pos in finalPath)
            {
                if (level.Get(pos.x, pos.y) == TileType.Water)
                    level.Set(pos.x, pos.y, TileType.Lotus);
            }
        }

        Debug.Log($"[KnightTourGenerator] Generated path of length {finalPath.Count} / target {targetPathLength}");
        return finalPath;
    }

    /// <summary>
    /// Kiem tra solvability theo dung pattern cua GameController.CheckSolvable():
    /// - Chi di qua Lotus tiles (khong di qua Water/Grass)
    /// - Clone map, danh dau o da di = Water, backtrack = Lotus lai
    /// - Nhanh hon nhieu vi khong build KnightGraph, khong traverse Water
    /// </summary>
    public static bool IsSolvable(LevelData level)
    {
        // Đếm tổng số Lotus cần đi qua — dùng GetLogic() thay vì check TileType cứng
        int totalLotus = 0;
        for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
                if (level.GetLogic(x, y) == LogicTileType.Lotus)
                    totalLotus++;

        if (totalLotus == 0)
        {
            Debug.LogWarning("[IsSolvable] No lotus tiles found on map.");
            return false;
        }

        Vector2Int start = level.startTile;
        LogicTileType startLogic = level.GetLogic(start.x, start.y);

        bool startIsLotus = startLogic == LogicTileType.Lotus;
        bool startIsGrass = startLogic == LogicTileType.Grass;

        if (!startIsLotus && !startIsGrass)
        {
            Debug.LogWarning("[IsSolvable] Start tile is not Lotus or Grass.");
            return false;
        }

        // Clone map để không ảnh hưởng dữ liệu gốc
        // Dùng LogicTileType array cho solver
        var logicMap = new LogicTileType[level.width * level.height];
        for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
                logicMap[y * level.width + x] = level.GetLogic(x, y);

        var visitedSet = new HashSet<Vector2Int>();
        int startLotusCredit = startIsLotus ? 1 : 0;

        if (startIsLotus)
            logicMap[start.y * level.width + start.x] = LogicTileType.Water;

        bool SolveDFS(Vector2Int pos, int visitedCount)
        {
            visitedSet.Add(pos);
            if (visitedCount == totalLotus) return true;

            foreach (var move in KnightMoves)
            {
                Vector2Int next = pos + move;
                if (next.x < 0 || next.y < 0 || next.x >= level.width || next.y >= level.height)
                    continue;

                int idx = next.y * level.width + next.x;
                if (logicMap[idx] != LogicTileType.Lotus) continue;
                if (visitedSet.Contains(next)) continue;

                logicMap[idx] = LogicTileType.Water;
                if (SolveDFS(next, visitedCount + 1)) return true;
                logicMap[idx] = LogicTileType.Lotus;
                visitedSet.Remove(next);
            }
            return false;
        }

        bool result = SolveDFS(start, startLotusCredit);
        Debug.Log($"[IsSolvable] {(result ? "SOLVABLE" : "NOT SOLVABLE")} | Lotus count: {totalLotus}");
        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    /// <summary>
    /// Tìm TileRef Water đầu tiên trong localPacks của level.
    /// Dùng để reset Lotus → Water khi Generate() clear slate.
    /// Trả về TileRef.Empty nếu không tìm thấy (ô sẽ về Empty).
    /// </summary>
    private static TileRef FindTileRefByLogic(LevelData level, LogicTileType targetLogic)
    {
        if (level.localPacks == null) return TileRef.Empty;
        foreach (var pack in level.localPacks)
        {
            if (pack == null) continue;
            pack.BuildCache();
            foreach (var entry in pack.entries)
                if (entry != null && entry.logicType == targetLogic && !string.IsNullOrEmpty(entry.id))
                    return new TileRef(pack.packName, entry.id);
        }
        return TileRef.Empty;
    }
}
