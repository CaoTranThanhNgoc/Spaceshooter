using UnityEngine;
using UnityEditor;
using SpaceHawk.Data;

namespace SpaceHawk.EditorTools
{
    /// <summary>Creates a small mixed set of achievements spanning enemy kills, ship upgrades and
    /// star ratings, so progression isn't just one repeated goal. Re-running this updates the
    /// existing assets in place instead of duplicating them.</summary>
    public static class AchievementContentBuilder
    {
        private const string DataFolder = "Assets/Resources/Data";
        private const string AchievementsFolder = DataFolder + "/Achievements";

        private struct Def
        {
            public int number;
            public AchievementKind kind;
            public int target;
            public int reward;
        }

        [MenuItem("Tools/Space Hawk/7. Build Achievement Database")]
        public static AchievementDatabase Build()
        {
            System.IO.Directory.CreateDirectory(AchievementsFolder);

            Def[] defs =
            {
                new Def { number = 1, kind = AchievementKind.EnemyKills, target = 25, reward = 60 },
                new Def { number = 2, kind = AchievementKind.EnemyKills, target = 100, reward = 150 },
                new Def { number = 3, kind = AchievementKind.EnemyKills, target = 300, reward = 280 },
                new Def { number = 4, kind = AchievementKind.ShipLevel, target = 3, reward = 150 },
                new Def { number = 5, kind = AchievementKind.ShipLevel, target = 5, reward = 300 },
                new Def { number = 6, kind = AchievementKind.StarsOnAnyLevel, target = 3, reward = 200 },
                new Def { number = 7, kind = AchievementKind.LevelsCleared, target = 3, reward = 150 },
                new Def { number = 8, kind = AchievementKind.LevelsCleared, target = 7, reward = 500 },
                new Def { number = 9, kind = AchievementKind.CrystalsEarned, target = 1000, reward = 200 },
                // Added alongside levels 8+ (LevelContentBuilder): #8 above was "clear everything"
                // back when there were only 7 levels - it stays as a mid-tier milestone, and #11
                // below is the new true completionist goal now that the game has 14.
                new Def { number = 10, kind = AchievementKind.EnemyKills, target = 600, reward = 450 },
                new Def { number = 11, kind = AchievementKind.LevelsCleared, target = 14, reward = 800 },
                new Def { number = 12, kind = AchievementKind.CrystalsEarned, target = 2500, reward = 400 },
                new Def { number = 13, kind = AchievementKind.EndlessWave, target = 10, reward = 250 },
                new Def { number = 14, kind = AchievementKind.EndlessWave, target = 25, reward = 600 },
            };

            AchievementData[] achievements = new AchievementData[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                Def d = defs[i];
                string id = $"ach_{d.number:00}";
                string path = $"{AchievementsFolder}/Achievement_{d.number:00}.asset";

                AchievementData data = AssetDatabase.LoadAssetAtPath<AchievementData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<AchievementData>();
                    AssetDatabase.CreateAsset(data, path);
                }

                data.achievementId = id;
                data.kind = d.kind;
                data.targetCount = d.target;
                data.crystalReward = d.reward;
                EditorUtility.SetDirty(data);

                achievements[i] = data;
            }

            string dbPath = $"{DataFolder}/AchievementDatabase.asset";
            AchievementDatabase db = AssetDatabase.LoadAssetAtPath<AchievementDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<AchievementDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            db.achievements = achievements;
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            Debug.Log($"[AchievementContentBuilder] Built {achievements.Length} achievements.");
            return db;
        }
    }
}
