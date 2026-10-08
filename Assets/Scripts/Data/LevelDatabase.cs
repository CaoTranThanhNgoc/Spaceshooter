using UnityEngine;

namespace SpaceHawk.Data
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Space Hawk/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        public LevelData[] levels;

        public int Count => levels?.Length ?? 0;

        public LevelData GetByIndex(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length) return null;
            return levels[index];
        }

        public int IndexOf(LevelData level)
        {
            if (levels == null || level == null) return -1;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] == level) return i;
            return -1;
        }
    }
}
