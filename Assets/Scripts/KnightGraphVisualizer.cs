using UnityEngine;

public class KnightGraphVisualizer : MonoBehaviour
{
    public LevelData levelData;
    public float cellSize = 1f; // T? l? cell khi v?

    private void OnDrawGizmos()
    {
        if (levelData == null) return;

        var graph = KnightGraphBuilder.BuildGraph(levelData);

        foreach (var kvp in graph)
        {
            Vector3 pos = new Vector3(kvp.Key.x * cellSize, 0.5f, kvp.Key.y * cellSize);

            // V? Node
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(pos, cellSize * 0.2f);

            // V? Edge
            Gizmos.color = Color.green;
            foreach (var neighbor in kvp.Value.neighbors)
            {
                Vector3 nPos = new Vector3(neighbor.position.x * cellSize, 0.5f, neighbor.position.y * cellSize);
                Gizmos.DrawLine(pos, nPos);
            }
        }
    }
}
