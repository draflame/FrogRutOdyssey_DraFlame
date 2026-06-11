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
        // --- Buoc 1: Reset Lotus cu -> Water de clear slate ---
        for (int i = 0; i < level.map.Length; i++)
        {
            TileType t = level.map[i];
            if (t == TileType.Lotus || t == TileType.LotusFlower || t == TileType.SmallLeaf)
                level.map[i] = TileType.Water;
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
        foreach (var pos in finalPath)
        {
            if (level.Get(pos.x, pos.y) == TileType.Water)
                level.Set(pos.x, pos.y, TileType.Lotus);
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
        // Dem tong so Lotus can di qua
        int totalLotus = 0;
        for (int i = 0; i < level.map.Length; i++)
        {
            TileType t = level.map[i];
            if (t == TileType.Lotus || t == TileType.LotusFlower || t == TileType.SmallLeaf)
                totalLotus++;
        }

        if (totalLotus == 0)
        {
            Debug.LogWarning("[IsSolvable] No lotus tiles found on map.");
            return false;
        }

        Vector2Int start = level.startTile;
        TileType startTile = level.Get(start.x, start.y);

        // Start phai la Lotus (hoac Grass duoc dung lam start) de co the bat dau
        bool startIsLotus = (startTile == TileType.Lotus ||
                             startTile == TileType.LotusFlower ||
                             startTile == TileType.SmallLeaf);
        bool startIsGrass = (startTile == TileType.Grass);

        if (!startIsLotus && !startIsGrass)
        {
            Debug.LogWarning("[IsSolvable] Start tile is not Lotus or Grass.");
            return false;
        }

        // Clone map de khong anh huong du lieu goc
        TileType[] mapClone = (TileType[])level.map.Clone();
        var visitedSet = new HashSet<Vector2Int>();

        // Neu start la Lotus, tinh no vao count
        int startLotusCredit = startIsLotus ? 1 : 0;

        // Danh dau start da di
        if (startIsLotus)
            mapClone[start.y * level.width + start.x] = TileType.Water;

        bool SolveDFS(Vector2Int pos, int visitedCount)
        {
            visitedSet.Add(pos);

            if (visitedCount == totalLotus)
                return true;

            foreach (var move in KnightMoves)
            {
                Vector2Int next = pos + move;

                if (next.x < 0 || next.y < 0 || next.x >= level.width || next.y >= level.height)
                    continue;

                int idx = next.y * level.width + next.x;
                TileType nextTile = mapClone[idx];

                // Chi di qua Lotus (chua duoc danh dau)
                bool isLotus = (nextTile == TileType.Lotus ||
                                nextTile == TileType.LotusFlower ||
                                nextTile == TileType.SmallLeaf);
                if (!isLotus) continue;
                if (visitedSet.Contains(next)) continue;

                // CHOOSE: danh dau da di
                mapClone[idx] = TileType.Water;

                // EXPLORE
                if (SolveDFS(next, visitedCount + 1))
                    return true;

                // BACKTRACK: tra lai
                mapClone[idx] = nextTile;
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
}
