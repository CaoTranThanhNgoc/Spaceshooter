using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceHawk.Data
{
    /// <summary>The wave recipe for Endless mode: wave N is built on demand from the wave number
    /// alone, so the fight never runs out and every number simply keeps climbing - more enemies,
    /// more hit points, faster, firing more often, harder-hitting bullets - with a boss every
    /// BossEvery waves. Wave 1 is about as hard as level 3 of the campaign.</summary>
    public static class EndlessWaves
    {
        public const int BossEvery = 10;
        public const int EnergyCost = 2;
        private const int CrystalsPerWave = 5;

        public static bool IsBossWave(int wave) => wave > 0 && wave % BossEvery == 0;

        /// <summary>Crystals paid when a run ends having reached `wave` (the wave you died in
        /// doesn't count - only the ones you survived through to it).</summary>
        public static int RewardFor(int wave) => Mathf.Max(0, wave - 1) * CrystalsPerWave;

        /// <summary>Each wave tints its enemies' explosions differently, cycling through the hue wheel.</summary>
        public static Color TintFor(int wave) => Color.HSVToRGB((wave * 0.137f) % 1f, 0.5f, 1f);

        /// <summary>The scrolling background changes every few waves.</summary>
        public static int BackgroundSetFor(int wave, int setCount)
        {
            if (setCount <= 0) return 0;
            return Mathf.Max(0, (wave - 1) / 4) % setCount;
        }

        public static EnemyWave[] Build(int wave)
        {
            wave = Mathf.Max(1, wave);
            int t = wave - 1;

            float lightSpeed = Mathf.Min(1.8f + t * 0.035f, 3.4f);
            float lightFire = Mathf.Max(0.85f, 1.8f - t * 0.025f);
            int lightHp = 55 + 6 * t;
            int lightDamage = Mathf.Min(8 + t / 3, 32);
            float lightInterval = Mathf.Max(0.35f, 0.8f - t * 0.015f);

            List<EnemyWave> waves = new List<EnemyWave>();

            if (IsBossWave(wave))
            {
                int tier = wave / BossEvery;
                waves.Add(new EnemyWave
                {
                    kind = EnemyKind.Light, count = Mathf.Min(6 + tier * 2, 14), spawnInterval = 0.55f,
                    hp = lightHp, speed = lightSpeed, fireInterval = lightFire, bulletDamage = lightDamage,
                });
                waves.Add(new EnemyWave
                {
                    kind = EnemyKind.Boss, count = 1, spawnInterval = 1f,
                    hp = 900 + 220 * (tier - 1), speed = 0.7f,
                    fireInterval = Mathf.Max(0.55f, 0.9f - 0.04f * (tier - 1)),
                    bulletDamage = Mathf.Min(22 + 3 * (tier - 1), 45), delayBeforeWave = 1.5f,
                });
                return waves.ToArray();
            }

            waves.Add(new EnemyWave
            {
                kind = EnemyKind.Light, count = Mathf.Min(6 + wave / 2, 16), spawnInterval = lightInterval,
                hp = lightHp, speed = lightSpeed, fireInterval = lightFire, bulletDamage = lightDamage,
            });

            if (wave >= 3)
            {
                waves.Add(new EnemyWave
                {
                    kind = EnemyKind.Heavy, count = Mathf.Min(1 + wave / 4, 8),
                    spawnInterval = Mathf.Max(0.8f, 1.3f - t * 0.01f),
                    hp = 95 + 10 * t, speed = Mathf.Min(1.3f + t * 0.025f, 2.4f),
                    fireInterval = Mathf.Max(0.8f, 1.6f - t * 0.025f),
                    bulletDamage = Mathf.Min(lightDamage + 4, 40), delayBeforeWave = 1.5f,
                });
            }

            // From wave 6, every other wave ends with a second fast rush of light enemies.
            if (wave >= 6 && wave % 2 == 0)
            {
                waves.Add(new EnemyWave
                {
                    kind = EnemyKind.Light, count = Mathf.Min(4 + wave / 3, 12), spawnInterval = Mathf.Max(0.3f, lightInterval - 0.15f),
                    hp = lightHp, speed = Mathf.Min(lightSpeed + 0.4f, 3.8f), fireInterval = lightFire, bulletDamage = lightDamage,
                    delayBeforeWave = 2f,
                });
            }

            return waves.ToArray();
        }

        /// <summary>Total enemies a wave will spawn - used by tests and by the HUD-less balance checks.</summary>
        public static int CountEnemies(int wave)
        {
            int total = 0;
            foreach (EnemyWave group in Build(wave)) total += group.count;
            return total;
        }
    }

    /// <summary>Creates (once) the runtime LevelData that stands for an Endless run. It is not an
    /// asset: nothing about it needs to be saved, and it is flagged so the spawner generates the
    /// waves instead of reading them.</summary>
    public static class EndlessLevel
    {
        public const string LevelId = "endless";
        private static LevelData _instance;

        public static LevelData GetOrCreate()
        {
            if (_instance != null) return _instance;

            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            level.hideFlags = HideFlags.HideAndDontSave;
            level.levelId = LevelId;
            level.displayNumber = 0;
            level.energyCost = EndlessWaves.EnergyCost;
            level.crystalReward = 0;
            level.requiredKillRatio = 0f;
            level.waves = Array.Empty<EnemyWave>();
            level.isEndless = true;
            _instance = level;
            return level;
        }
    }
}
