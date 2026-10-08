using UnityEngine;

namespace SpaceHawk.Data
{
    public enum AchievementKind
    {
        EnemyKills,       // lifetime enemies destroyed
        ShipLevel,        // Inventory ship upgrade level reached
        StarsOnAnyLevel,  // best star rating earned on any single level
        LevelsCleared,    // count of levels beaten (highestUnlockedLevelIndex)
        CrystalsEarned,   // lifetime Crystals earned, not the current spendable balance
        EndlessWave,      // best wave reached in Endless mode
    }

    [CreateAssetMenu(fileName = "Achievement_", menuName = "Space Hawk/Achievement Data")]
    public class AchievementData : ScriptableObject
    {
        [Tooltip("Stable id used as the save-file key. Do not rename after players have progress.")]
        public string achievementId = "ach_01";

        public AchievementKind kind = AchievementKind.EnemyKills;
        public int targetCount = 10;
        public int crystalReward = 100;
    }
}
