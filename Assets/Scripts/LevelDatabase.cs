using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "LevelDatabase", menuName = "KnightTour/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public List<LevelData> levels;
}
