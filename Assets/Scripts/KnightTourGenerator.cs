using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

public class KnightTourGenerator
{
    private static System.Random rng = new System.Random();

    public static List<Vector2Int> Generate(LevelData level)
    {
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
            case Difficulty.Easy: percentLotuses = 0.35f; break; // 35%
            case Difficulty.Normal: percentLotuses = 0.50f; break; // 50%
            case Difficulty.Hard: percentLotuses = 0.65f; break; // 65%
            case Difficulty.VeryHard: percentLotuses = 0.80f; break; // 80%
            case Difficulty.Expert: percentLotuses = 0.90f; break; // 90%
            case Difficulty.Nightmare: percentLotuses = 1.0f; break; // 100%
        }

        int maxNodes = Mathf.Max(5, Mathf.CeilToInt(totalValid * percentLotuses));
        int mainPathLength = Mathf.Max(3, Mathf.CeilToInt(maxNodes * 0.6f));

        var mainPath = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        bool DFS(KnightNode node)
        {
            mainPath.Add(node.position);
            visited.Add(node.position);

            if (mainPath.Count >= mainPathLength) return true;

            var neighbors = new List<KnightNode>(node.neighbors);
            Shuffle(neighbors);

            foreach (var n in neighbors)
            {
                if (!visited.Contains(n.position))
                {
                    if (DFS(n)) return true;
                }
            }

            mainPath.RemoveAt(mainPath.Count - 1);
            visited.Remove(node.position);
            return false;
        }

        DFS(graph[level.startTile]);

        // Thêm nhánh r? ?? làm ng??i ch?i r?i
        var allUsedNodes = new HashSet<Vector2Int>(visited);
        var activeNodes = new List<KnightNode>();
        foreach (var pos in mainPath) activeNodes.Add(graph[pos]);

        while (allUsedNodes.Count < maxNodes && activeNodes.Count > 0)
        {
            int idx = rng.Next(activeNodes.Count);
            KnightNode node = activeNodes[idx];

            var unvisited = new List<KnightNode>();
            foreach (var n in node.neighbors)
            {
                if (!allUsedNodes.Contains(n.position)) unvisited.Add(n);
            }

            if (unvisited.Count > 0)
            {
                KnightNode chosen = unvisited[rng.Next(unvisited.Count)];
                allUsedNodes.Add(chosen.position);
                activeNodes.Add(chosen);
            }
            else
            {
                activeNodes.RemoveAt(idx);
            }
        }

        // T? ??ng set Lotus cho map cho t?t c? các ?i?m bao g?m map chính + nhánh r?
        foreach (var pos in allUsedNodes)
        {
            if (level.Get(pos.x, pos.y) == TileType.Water)
            {
                level.Set(pos.x, pos.y, TileType.Lotus);
            }
        }

        return mainPath;
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
