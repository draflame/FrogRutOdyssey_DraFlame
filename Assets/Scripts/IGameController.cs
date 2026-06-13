using UnityEngine;

public interface IGameController
{
    Vector3 GetWorldPosition(Vector2Int tile);
    bool CanMove(Vector2Int from, Vector2Int to);
    void RecordMove(Vector2Int from, Vector2Int to);
    void ClearTile(Vector2Int tile);
    void ConsumeTile(Vector2Int tile);
    void OnFrogMoved(Vector2Int currentTile);
    void HighlightValidMoves(Vector2Int from);
}
