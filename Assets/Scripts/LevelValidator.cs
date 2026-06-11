using UnityEngine;

public static class LevelValidator
{
    public static bool IsStartValid(LevelData level)
    {
        if (level.startTile.x < 0 || level.startTile.x >= level.width || level.startTile.y < 0 || level.startTile.y >= level.height)
            return false;

        TileType tile = level.Get(level.startTile.x, level.startTile.y);
        return tile == TileType.Grass || tile == TileType.Lotus || tile == TileType.Water;
    }

    public static bool HasEnoughGrass(LevelData level, int minGrassCount = 10)
    {
        int grassCount = 0;
        for (int i = 0; i < level.map.Length; i++)
        {
            if (level.map[i] == TileType.Grass || level.map[i] == TileType.Lotus || level.map[i] == TileType.Water)
            {
                grassCount++;
            }
        }
        return grassCount >= minGrassCount;
    }
}
