using System.Collections.Generic;
using UnityEngine;

public class KnightNode
{
    public Vector2Int position;
    public List<KnightNode> neighbors = new List<KnightNode>();
}

public class KnightGraphBuilder
{
    private static readonly Vector2Int[] Moves =
    {
        new Vector2Int(1, 2),
        new Vector2Int(2, 1),
        new Vector2Int(-1, 2),
        new Vector2Int(-2, 1),
        new Vector2Int(1, -2),
        new Vector2Int(2, -1),
        new Vector2Int(-1, -2),
        new Vector2Int(-2, -1)
    };

    public static Dictionary<Vector2Int, KnightNode> BuildGraph(LevelData level)
    {
        var graph = new Dictionary<Vector2Int, KnightNode>();

        // L?c t?t c? các ô
        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                TileType tile = level.Get(x, y);
                bool isStartTile = (level.startTile.x == x && level.startTile.y == y);

                if (tile == TileType.Water || tile == TileType.Lotus || (isStartTile && tile == TileType.Grass))
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    graph[pos] = new KnightNode { position = pos };
                }
            }
        }

        // T?o c?nh (edges) gi?a các ô Grass d?a vào n??c ?i Knight
        foreach (var kvp in graph)
        {
            Vector2Int pos = kvp.Key;
            KnightNode node = kvp.Value;

            foreach (var move in Moves)
            {
                Vector2Int targetPos = pos + move;

                if (graph.TryGetValue(targetPos, out KnightNode targetNode))
                {
                    node.neighbors.Add(targetNode);
                }
            }
        }

        return graph;
    }
}
