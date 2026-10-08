using UnityEngine;
using SpaceHawk.Core;

namespace SpaceHawk.Data
{
    /// <summary>One cleared level's score, split into the parts the Victory screen lists.</summary>
    public struct LevelScoreBreakdown
    {
        public int killPoints;
        public int healthBonus;
        public int speedBonus;
        public float difficulty;
        public int total;
    }

    /// <summary>The Leaderboard formula. The rating is the SUM OF YOUR BEST SCORE ON EACH LEVEL plus
    /// your best Endless run - so replaying an easy level to grind kills no longer climbs the board,
    /// only playing a level better than before does.
    ///
    /// A level's score for one clear:
    ///   (kill points + health bonus + speed bonus) x difficulty
    ///
    ///   kill points   what every enemy was worth: Light 10, Heavy 25, Boss 250. Missing enemies
    ///                 (letting them fly past) simply forfeits their points.
    ///   health bonus  up to +60% of kill points for taking little damage: 0.6 x (1 - damage/maxHP)^2.
    ///                 Squared, so a near-flawless run is worth much more than a merely healthy one;
    ///                 dying and reviving counts as maximum damage (bonus 0).
    ///   speed bonus   up to +40% of kill points for clearing the level near its "par" time (the
    ///                 enemies' own spawn schedule plus a little kill time): full at or under par,
    ///                 fading to nothing at twice par.
    ///   difficulty    x(1 + 0.10 per level above the first): level 14 counts 2.3x level 1.
    ///
    /// An Endless run scores kill points + 100 for each wave reached beyond the first.</summary>
    public static class SkillScore
    {
        public const int PointsLight = 10;
        public const int PointsHeavy = 25;
        public const int PointsBoss = 250;

        public const float HealthBonusMax = 0.6f;
        public const float SpeedBonusMax = 0.4f;
        public const float DifficultyPerLevel = 0.10f;
        public const int EndlessWaveBonus = 100;

        // Par time = the wave schedule + this much time to actually shoot the last enemies down,
        // plus boss hit points at an assumed damage rate (a mid-upgrade ship).
        private const float ParOverheadSeconds = 8f;
        private const float AssumedDamagePerSecond = 70f;

        public static int EnemyPoints(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Heavy: return PointsHeavy;
                case EnemyKind.Boss: return PointsBoss;
                default: return PointsLight;
            }
        }

        public static float DifficultyMultiplier(int levelNumber)
        {
            return 1f + DifficultyPerLevel * Mathf.Max(0, levelNumber - 1);
        }

        /// <summary>Kill points for destroying every single enemy the level spawns.</summary>
        public static int MaxKillPoints(LevelData level)
        {
            int total = 0;
            if (level.waves == null) return total;
            foreach (EnemyWave wave in level.waves) total += wave.count * EnemyPoints(wave.kind);
            return total;
        }

        public static float ParSeconds(LevelData level)
        {
            float seconds = ParOverheadSeconds;
            if (level.waves == null) return seconds;

            foreach (EnemyWave wave in level.waves)
            {
                seconds += wave.delayBeforeWave + wave.count * wave.spawnInterval;
                if (wave.kind == EnemyKind.Boss) seconds += wave.hp * wave.count / AssumedDamagePerSecond;
            }
            return seconds;
        }

        /// <summary>1 for a clean run, 0 once a whole health bar's worth of damage was taken.</summary>
        public static float HealthFactor(float damageTaken, int maxHp)
        {
            if (maxHp <= 0) return 0f;
            float kept = 1f - Mathf.Clamp01(damageTaken / maxHp);
            return kept * kept;
        }

        /// <summary>1 at or under par, falling linearly to 0 at twice par.</summary>
        public static float PaceFactor(float seconds, float parSeconds)
        {
            if (parSeconds <= 0f) return 0f;
            return Mathf.Clamp01((2f * parSeconds - seconds) / parSeconds);
        }

        public static LevelScoreBreakdown ForLevel(LevelData level, int killPoints, float damageTaken, int maxHp, float seconds)
        {
            LevelScoreBreakdown result = new LevelScoreBreakdown
            {
                killPoints = killPoints,
                healthBonus = Mathf.RoundToInt(killPoints * HealthBonusMax * HealthFactor(damageTaken, maxHp)),
                speedBonus = Mathf.RoundToInt(killPoints * SpeedBonusMax * PaceFactor(seconds, ParSeconds(level))),
                difficulty = DifficultyMultiplier(level.displayNumber),
            };
            result.total = Mathf.RoundToInt((result.killPoints + result.healthBonus + result.speedBonus) * result.difficulty);
            return result;
        }

        public static int ForEndless(int killPoints, int wave)
        {
            return killPoints + EndlessWaveBonus * Mathf.Max(0, wave - 1);
        }

        /// <summary>What a level cleared BEFORE this scoring existed is credited as: a full clear
        /// at the difficulty multiplier, plus a modest bonus per star earned (stars were the only
        /// skill record kept back then). Deliberately a little below a genuinely good run, so
        /// replaying always has something to gain.</summary>
        public static int EstimateLegacy(LevelData level, int stars)
        {
            float starBonus = 1f + 0.3f * Mathf.Max(0, stars - 1);
            return Mathf.RoundToInt(MaxKillPoints(level) * starBonus * DifficultyMultiplier(level.displayNumber));
        }

        /// <summary>Once, for players who already had progress: gives every cleared level a starting
        /// best score from its stars, so their rating doesn't restart from zero. Never overwrites a
        /// real score.</summary>
        public static void MigrateLegacyScores(LevelDatabase database)
        {
            if (database == null || SaveManager.Data.skillScoresMigrated) return;

            for (int i = 0; i < database.Count; i++)
            {
                LevelData level = database.GetByIndex(i);
                if (ReferenceEquals(level, null)) continue;

                int stars = SaveManager.GetStars(level.levelId);
                if (stars > 0 && SaveManager.GetLevelBestScore(level.levelId) <= 0)
                    SaveManager.SetLevelBestScore(level.levelId, EstimateLegacy(level, stars));
            }

            SaveManager.Data.skillScoresMigrated = true;
            SaveManager.Save();
        }
    }
}
