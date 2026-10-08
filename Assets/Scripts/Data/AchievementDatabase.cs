using UnityEngine;

namespace SpaceHawk.Data
{
    [CreateAssetMenu(fileName = "AchievementDatabase", menuName = "Space Hawk/Achievement Database")]
    public class AchievementDatabase : ScriptableObject
    {
        public AchievementData[] achievements;
    }
}
