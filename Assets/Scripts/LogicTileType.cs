/// <summary>
/// Enum nhỏ chỉ dùng cho GAME LOGIC (solver, pathfinding, win condition).
/// KHÔNG dùng cho visual / render tilemap.
/// Visual được quản lý bởi TilePack ScriptableObject.
/// </summary>
public enum LogicTileType
{
    Empty  = 0,   // Ô trống, không thể đứng, không render
    Lotus  = 1,   // Ô frog phải đi qua (walkable, counted toward win)
    Water  = 2,   // Ô đã bị đi qua (frog biến nó thành nước)
    Grass  = 3,   // Ô nền / chướng ngại vật (không phải walkable)
}
