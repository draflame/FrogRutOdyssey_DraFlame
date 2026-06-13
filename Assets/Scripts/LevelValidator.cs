using UnityEngine;

public static class LevelValidator
{
    public static bool IsStartValid(LevelData level)
    {
        if (level.startTile.x < 0 || level.startTile.x >= level.width ||
            level.startTile.y < 0 || level.startTile.y >= level.height)
            return false;

        // Dùng GetLogic() — hoạt động với cả format cũ lẫn mới
        LogicTileType logic = level.GetLogic(level.startTile.x, level.startTile.y);
        return logic == LogicTileType.Grass || logic == LogicTileType.Lotus || logic == LogicTileType.Water;
    }

    public static bool HasEnoughGrass(LevelData level, int minGrassCount = 10)
    {
        int count = 0;
        for (int y = 0; y < level.height; y++)
            for (int x = 0; x < level.width; x++)
            {
                LogicTileType logic = level.GetLogic(x, y);
                if (logic == LogicTileType.Grass || logic == LogicTileType.Lotus || logic == LogicTileType.Water)
                    count++;
            }
        return count >= minGrassCount;
    }
}
