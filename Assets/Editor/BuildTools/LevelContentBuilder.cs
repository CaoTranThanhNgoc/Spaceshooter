using UnityEngine;
using UnityEditor;
using SpaceHawk.Data;

namespace SpaceHawk.EditorTools
{
    /// <summary>Creates the level set with an increasing difficulty curve: levels 1-6 ramp up
    /// steadily, level 7 is the first boss fight, levels 8-13 continue the climb on a second page
    /// of the map, level 14 is a second, tougher boss. Re-running this updates the existing
    /// assets in place instead of duplicating them.</summary>
    public static class LevelContentBuilder
    {
        private const string DataFolder = "Assets/Resources/Data";
        private const string LevelsFolder = DataFolder + "/Levels";

        private struct Def
        {
            public int number;
            public int energyCost;
            public int reward;
            public int mapPage;
            public Vector2 mapAnchor;
            public EnemyWave[] waves;
        }

        // Every level tints its own enemy explosions a different color (see
        // LevelData.levelTintColor / Enemy.explosionTint) - pale/soft early on, increasingly
        // saturated as the game goes on, with both bosses (7, 14) landing on a dramatic red/purple
        // that stands apart from the regular levels around them.
        private static readonly Color[] LevelTints =
        {
            new Color(0.75f, 0.95f, 1f),   // 1 pale cyan
            new Color(0.75f, 1f, 0.8f),    // 2 pale green
            new Color(1f, 0.95f, 0.7f),    // 3 pale yellow
            new Color(1f, 0.8f, 0.55f),    // 4 soft orange
            new Color(1f, 0.75f, 0.85f),   // 5 soft pink
            new Color(0.85f, 0.75f, 1f),   // 6 soft purple
            new Color(1f, 0.35f, 0.25f),   // 7 deep red (boss)
            new Color(0.5f, 0.75f, 1f),    // 8 blue
            new Color(0.4f, 1f, 0.85f),    // 9 teal
            new Color(1f, 0.85f, 0.3f),    // 10 gold
            new Color(1f, 0.4f, 0.85f),    // 11 magenta
            new Color(0.7f, 0.4f, 1f),     // 12 violet
            new Color(1f, 0.3f, 0.4f),     // 13 crimson
            new Color(0.65f, 0.25f, 1f),   // 14 intense purple (boss)
        };

        [MenuItem("Tools/Space Hawk/2. Build Level Database")]
        public static LevelDatabase Build()
        {
            System.IO.Directory.CreateDirectory(LevelsFolder);

            Def[] defs = new[]
            {
                new Def { number = 1, energyCost = 1, reward = 25, mapAnchor = new Vector2(0.073f, 0.693f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.9f, hp = 45, speed = 1.5f, fireInterval = 2.3f, bulletDamage = 6 },
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.8f, hp = 50, speed = 1.7f, fireInterval = 2.1f, bulletDamage = 6, delayBeforeWave = 2.5f },
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.7f, hp = 55, speed = 1.8f, fireInterval = 1.9f, bulletDamage = 7, delayBeforeWave = 2.5f },
                }},
                new Def { number = 2, energyCost = 1, reward = 32, mapAnchor = new Vector2(0.345f, 0.600f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 7, spawnInterval = 0.8f, hp = 50, speed = 1.6f, fireInterval = 2.1f, bulletDamage = 7 },
                    new EnemyWave { kind = EnemyKind.Light, count = 7, spawnInterval = 0.7f, hp = 55, speed = 1.8f, fireInterval = 1.9f, bulletDamage = 7, delayBeforeWave = 2.5f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 3, spawnInterval = 1.3f, hp = 90, speed = 1.2f, fireInterval = 1.7f, bulletDamage = 11, delayBeforeWave = 3f },
                }},
                new Def { number = 3, energyCost = 1, reward = 40, mapAnchor = new Vector2(0.165f, 0.387f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 8, spawnInterval = 0.7f, hp = 55, speed = 1.8f, fireInterval = 1.8f, bulletDamage = 8 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 4, spawnInterval = 1.3f, hp = 95, speed = 1.3f, fireInterval = 1.6f, bulletDamage = 12, delayBeforeWave = 2.5f },
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.6f, hp = 60, speed = 2.0f, fireInterval = 1.6f, bulletDamage = 8, delayBeforeWave = 2.5f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 3, spawnInterval = 1.2f, hp = 100, speed = 1.3f, fireInterval = 1.5f, bulletDamage = 13, delayBeforeWave = 2.5f },
                }},
                new Def { number = 4, energyCost = 2, reward = 48, mapAnchor = new Vector2(0.445f, 0.218f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 8, spawnInterval = 0.65f, hp = 60, speed = 2.0f, fireInterval = 1.7f, bulletDamage = 9 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 5, spawnInterval = 1.2f, hp = 105, speed = 1.4f, fireInterval = 1.5f, bulletDamage = 13, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 8, spawnInterval = 0.6f, hp = 65, speed = 2.1f, fireInterval = 1.5f, bulletDamage = 9, delayBeforeWave = 2.5f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 4, spawnInterval = 1.1f, hp = 110, speed = 1.4f, fireInterval = 1.4f, bulletDamage = 14, delayBeforeWave = 3f },
                }},
                new Def { number = 5, energyCost = 2, reward = 58, mapAnchor = new Vector2(0.595f, 0.791f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 9, spawnInterval = 0.6f, hp = 65, speed = 2.1f, fireInterval = 1.5f, bulletDamage = 9 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 5, spawnInterval = 1.1f, hp = 115, speed = 1.5f, fireInterval = 1.4f, bulletDamage = 14, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 9, spawnInterval = 0.55f, hp = 70, speed = 2.2f, fireInterval = 1.4f, bulletDamage = 10, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 6, spawnInterval = 1.1f, hp = 120, speed = 1.5f, fireInterval = 1.3f, bulletDamage = 15, delayBeforeWave = 3f },
                }},
                new Def { number = 6, energyCost = 2, reward = 70, mapAnchor = new Vector2(0.665f, 0.440f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 10, spawnInterval = 0.55f, hp = 70, speed = 2.2f, fireInterval = 1.4f, bulletDamage = 10 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 6, spawnInterval = 1.05f, hp = 125, speed = 1.6f, fireInterval = 1.3f, bulletDamage = 15, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 10, spawnInterval = 0.5f, hp = 75, speed = 2.3f, fireInterval = 1.3f, bulletDamage = 11, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 7, spawnInterval = 1.0f, hp = 130, speed = 1.6f, fireInterval = 1.2f, bulletDamage = 16, delayBeforeWave = 3f },
                }},
                new Def { number = 7, energyCost = 3, reward = 220, mapAnchor = new Vector2(0.855f, 0.187f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.7f, hp = 60, speed = 2.0f, fireInterval = 1.6f, bulletDamage = 9 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 5, spawnInterval = 1.1f, hp = 110, speed = 1.4f, fireInterval = 1.4f, bulletDamage = 13, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.6f, hp = 65, speed = 2.1f, fireInterval = 1.4f, bulletDamage = 10, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Boss, count = 1, spawnInterval = 0f, hp = 600, speed = 0.6f, fireInterval = 0.9f, bulletDamage = 22, delayBeforeWave = 4f },
                }},
                // Levels 8-14 are page 2 of the map - EVERY planet on a page gets exactly one
                // level (matching page 1's 7-planets-to-7-levels layout exactly), so these reuse
                // page 1's own 7 mapAnchor values slot-for-slot (level 8 = level 1's spot, level
                // 9 = level 2's spot, ... level 14 = level 7's spot) rather than inventing new
                // coordinates - positions already proven to have plenty of clearance from each
                // other on a page this size. Stats continue the curve from level 6's end state
                // (not level 7's own numbers, which were deliberately softened pre-boss - see
                // level 7 below), with the same gentle per-level deltas levels 1-6 used.
                new Def { number = 8, energyCost = 3, reward = 85, mapPage = 1, mapAnchor = new Vector2(0.073f, 0.693f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 10, spawnInterval = 0.5f, hp = 80, speed = 2.4f, fireInterval = 1.2f, bulletDamage = 11 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 7, spawnInterval = 1.0f, hp = 135, speed = 1.7f, fireInterval = 1.2f, bulletDamage = 16, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 10, spawnInterval = 0.45f, hp = 85, speed = 2.5f, fireInterval = 1.1f, bulletDamage = 12, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 8, spawnInterval = 0.95f, hp = 140, speed = 1.7f, fireInterval = 1.1f, bulletDamage = 17, delayBeforeWave = 3f },
                }},
                new Def { number = 9, energyCost = 3, reward = 98, mapPage = 1, mapAnchor = new Vector2(0.345f, 0.600f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 10, spawnInterval = 0.5f, hp = 90, speed = 2.6f, fireInterval = 1.1f, bulletDamage = 12 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 7, spawnInterval = 0.95f, hp = 145, speed = 1.8f, fireInterval = 1.1f, bulletDamage = 17, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 11, spawnInterval = 0.45f, hp = 95, speed = 2.6f, fireInterval = 1.0f, bulletDamage = 13, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 8, spawnInterval = 0.9f, hp = 150, speed = 1.8f, fireInterval = 1.0f, bulletDamage = 18, delayBeforeWave = 3f },
                }},
                new Def { number = 10, energyCost = 3, reward = 112, mapPage = 1, mapAnchor = new Vector2(0.165f, 0.387f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 11, spawnInterval = 0.45f, hp = 100, speed = 2.7f, fireInterval = 1.0f, bulletDamage = 13 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 8, spawnInterval = 0.9f, hp = 155, speed = 1.8f, fireInterval = 1.0f, bulletDamage = 18, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 11, spawnInterval = 0.4f, hp = 105, speed = 2.7f, fireInterval = 0.95f, bulletDamage = 14, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 9, spawnInterval = 0.85f, hp = 160, speed = 1.9f, fireInterval = 0.95f, bulletDamage = 19, delayBeforeWave = 3f },
                }},
                new Def { number = 11, energyCost = 4, reward = 128, mapPage = 1, mapAnchor = new Vector2(0.445f, 0.218f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 11, spawnInterval = 0.4f, hp = 110, speed = 2.8f, fireInterval = 0.9f, bulletDamage = 14 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 8, spawnInterval = 0.85f, hp = 165, speed = 1.9f, fireInterval = 0.9f, bulletDamage = 19, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 12, spawnInterval = 0.35f, hp = 115, speed = 2.8f, fireInterval = 0.85f, bulletDamage = 15, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 9, spawnInterval = 0.8f, hp = 170, speed = 1.9f, fireInterval = 0.85f, bulletDamage = 20, delayBeforeWave = 3f },
                }},
                new Def { number = 12, energyCost = 4, reward = 145, mapPage = 1, mapAnchor = new Vector2(0.595f, 0.791f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 12, spawnInterval = 0.35f, hp = 120, speed = 2.9f, fireInterval = 0.85f, bulletDamage = 15 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 9, spawnInterval = 0.8f, hp = 175, speed = 2.0f, fireInterval = 0.85f, bulletDamage = 20, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 12, spawnInterval = 0.3f, hp = 125, speed = 2.9f, fireInterval = 0.8f, bulletDamage = 16, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 10, spawnInterval = 0.75f, hp = 180, speed = 2.0f, fireInterval = 0.8f, bulletDamage = 21, delayBeforeWave = 3f },
                }},
                new Def { number = 13, energyCost = 4, reward = 165, mapPage = 1, mapAnchor = new Vector2(0.665f, 0.440f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 12, spawnInterval = 0.3f, hp = 130, speed = 3.0f, fireInterval = 0.8f, bulletDamage = 16 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 9, spawnInterval = 0.75f, hp = 185, speed = 2.0f, fireInterval = 0.8f, bulletDamage = 21, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 13, spawnInterval = 0.3f, hp = 135, speed = 3.0f, fireInterval = 0.75f, bulletDamage = 17, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 10, spawnInterval = 0.7f, hp = 190, speed = 2.1f, fireInterval = 0.75f, bulletDamage = 22, delayBeforeWave = 3f },
                }},
                // Second boss - tougher than level 7's across the board (1100 vs 600 HP, faster
                // fire, harder hits), with the same softened lead-in pacing level 7 used so the
                // climb doesn't also exhaust the player right before the fight.
                new Def { number = 14, energyCost = 5, reward = 500, mapPage = 1, mapAnchor = new Vector2(0.855f, 0.187f), waves = new[] {
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.6f, hp = 100, speed = 2.6f, fireInterval = 1.1f, bulletDamage = 14 },
                    new EnemyWave { kind = EnemyKind.Heavy, count = 5, spawnInterval = 1.0f, hp = 160, speed = 1.8f, fireInterval = 1.0f, bulletDamage = 19, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Light, count = 6, spawnInterval = 0.55f, hp = 105, speed = 2.7f, fireInterval = 1.0f, bulletDamage = 15, delayBeforeWave = 3f },
                    new EnemyWave { kind = EnemyKind.Boss, count = 1, spawnInterval = 0f, hp = 1100, speed = 0.7f, fireInterval = 0.7f, bulletDamage = 28, delayBeforeWave = 4f },
                }},
            };

            LevelData[] levels = new LevelData[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                Def d = defs[i];
                string id = $"level_{d.number:00}";
                string path = $"{LevelsFolder}/Level_{d.number:00}.asset";

                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelData>();
                    AssetDatabase.CreateAsset(level, path);
                }

                level.levelId = id;
                level.displayNumber = d.number;
                level.energyCost = d.energyCost;
                level.crystalReward = d.reward;
                level.waves = d.waves;
                level.mapPage = d.mapPage;
                level.mapAnchor = d.mapAnchor;
                level.backgroundSetIndex = (d.number - 1) % 4;
                level.levelTintColor = LevelTints[(d.number - 1) % LevelTints.Length];
                EditorUtility.SetDirty(level);

                levels[i] = level;
            }

            string dbPath = $"{DataFolder}/LevelDatabase.asset";
            LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            db.levels = levels;
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelContentBuilder] Built {levels.Length} levels.");
            return db;
        }
    }
}
