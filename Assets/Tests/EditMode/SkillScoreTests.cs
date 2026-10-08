using NUnit.Framework;
using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.Gameplay;

namespace SpaceHawk.Tests
{
    public class SkillScoreTests
    {
        private LevelDatabase _db;

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            _db = Resources.Load<LevelDatabase>("Data/LevelDatabase");
            Assert.IsNotNull(_db, "LevelDatabase missing - run Tools/Space Hawk/0. Build Everything first.");
        }

        private LevelData Level(int number)
        {
            LevelData level = _db.GetByIndex(number - 1);
            Assert.IsFalse(ReferenceEquals(level, null));
            return level;
        }

        // ---------------------------------------------------------------- the pieces

        [Test]
        public void EnemyPoints_HarderEnemiesAreWorthMore()
        {
            Assert.Less(SkillScore.EnemyPoints(EnemyKind.Light), SkillScore.EnemyPoints(EnemyKind.Heavy));
            Assert.Less(SkillScore.EnemyPoints(EnemyKind.Heavy), SkillScore.EnemyPoints(EnemyKind.Boss));
        }

        [Test]
        public void Difficulty_RisesByTenPercentPerLevel()
        {
            Assert.AreEqual(1.0f, SkillScore.DifficultyMultiplier(1), 0.0001f);
            Assert.AreEqual(1.1f, SkillScore.DifficultyMultiplier(2), 0.0001f);
            Assert.AreEqual(2.3f, SkillScore.DifficultyMultiplier(14), 0.0001f);
            Assert.AreEqual(1.0f, SkillScore.DifficultyMultiplier(0), 0.0001f, "Nothing below the base multiplier.");
        }

        [Test]
        public void HealthFactor_IsSquared_AndZeroOnceAWholeBarIsLost()
        {
            Assert.AreEqual(1f, SkillScore.HealthFactor(0, 100), 0.0001f);
            Assert.AreEqual(0.25f, SkillScore.HealthFactor(50, 100), 0.0001f);
            Assert.AreEqual(0f, SkillScore.HealthFactor(100, 100), 0.0001f);
            Assert.AreEqual(0f, SkillScore.HealthFactor(900, 100), 0.0001f, "Extra damage past a full bar can't go negative.");
            Assert.AreEqual(0f, SkillScore.HealthFactor(0, 0), 0.0001f, "No max HP means no bonus, not a divide by zero.");
        }

        [Test]
        public void PaceFactor_FullAtPar_FadingToNothingAtDoublePar()
        {
            Assert.AreEqual(1f, SkillScore.PaceFactor(10, 30), 0.0001f, "Faster than par is capped at full marks.");
            Assert.AreEqual(1f, SkillScore.PaceFactor(30, 30), 0.0001f);
            Assert.AreEqual(0.5f, SkillScore.PaceFactor(45, 30), 0.0001f);
            Assert.AreEqual(0f, SkillScore.PaceFactor(60, 30), 0.0001f);
            Assert.AreEqual(0f, SkillScore.PaceFactor(600, 30), 0.0001f);
        }

        // ---------------------------------------------------------------- a level's score

        [Test]
        public void ACleanFastClear_EarnsTheMaximumBonuses()
        {
            LevelData level = Level(1);
            int kill = SkillScore.MaxKillPoints(level);

            LevelScoreBreakdown best = SkillScore.ForLevel(level, kill, 0f, 100, SkillScore.ParSeconds(level) * 0.5f);

            Assert.AreEqual(kill, best.killPoints);
            Assert.AreEqual(Mathf.RoundToInt(kill * SkillScore.HealthBonusMax), best.healthBonus);
            Assert.AreEqual(Mathf.RoundToInt(kill * SkillScore.SpeedBonusMax), best.speedBonus);
            Assert.AreEqual(1f, best.difficulty, 0.0001f);
            Assert.AreEqual(best.killPoints + best.healthBonus + best.speedBonus, best.total, "Level 1 has no difficulty multiplier.");
        }

        [Test]
        public void TheMaximumPossibleScore_IsTwiceTheKillPoints_TimesDifficulty()
        {
            for (int n = 1; n <= _db.Count; n++)
            {
                LevelData level = Level(n);
                int kill = SkillScore.MaxKillPoints(level);
                LevelScoreBreakdown best = SkillScore.ForLevel(level, kill, 0f, 100, 0f);
                Assert.LessOrEqual(best.total, Mathf.CeilToInt(kill * 2f * SkillScore.DifficultyMultiplier(level.displayNumber)) + 1, "level " + n);
            }
        }

        [Test]
        public void TakingDamage_NeverRaisesTheScore()
        {
            LevelData level = Level(3);
            int kill = SkillScore.MaxKillPoints(level);
            float par = SkillScore.ParSeconds(level);
            int previous = int.MaxValue;
            for (int damage = 0; damage <= 120; damage += 10)
            {
                int total = SkillScore.ForLevel(level, kill, damage, 100, par).total;
                Assert.LessOrEqual(total, previous, "damage " + damage);
                previous = total;
            }
        }

        [Test]
        public void TakingLonger_NeverRaisesTheScore()
        {
            LevelData level = Level(3);
            int kill = SkillScore.MaxKillPoints(level);
            float par = SkillScore.ParSeconds(level);
            int previous = int.MaxValue;
            for (float t = 0; t <= par * 3f; t += par / 8f)
            {
                int total = SkillScore.ForLevel(level, kill, 0f, 100, t).total;
                Assert.LessOrEqual(total, previous, "time " + t);
                previous = total;
            }
        }

        [Test]
        public void MissingEnemies_CostsTheirPoints()
        {
            LevelData level = Level(2);
            int all = SkillScore.MaxKillPoints(level);
            float par = SkillScore.ParSeconds(level);

            int full = SkillScore.ForLevel(level, all, 0f, 100, par).total;
            int most = SkillScore.ForLevel(level, Mathf.RoundToInt(all * 0.7f), 0f, 100, par).total;

            Assert.Less(most, full);
        }

        [Test]
        public void DyingAndRevivingForfeitsTheHealthBonus()
        {
            LevelData level = Level(1);
            int kill = SkillScore.MaxKillPoints(level);
            // A death means the whole bar was lost at least once.
            LevelScoreBreakdown revived = SkillScore.ForLevel(level, kill, 100f, 100, 0f);
            Assert.AreEqual(0, revived.healthBonus);
        }

        [Test]
        public void TheSamePerformance_ScoresMoreOnAHarderLevel()
        {
            LevelData easy = Level(1);
            LevelData hard = Level(10);
            // 100 kill points, clean, at par: only the difficulty multiplier differs.
            int onEasy = SkillScore.ForLevel(easy, 100, 0f, 100, SkillScore.ParSeconds(easy)).total;
            int onHard = SkillScore.ForLevel(hard, 100, 0f, 100, SkillScore.ParSeconds(hard)).total;
            Assert.Greater(onHard, onEasy);
        }

        [Test]
        public void EveryLevel_HasSensibleScoringInputs()
        {
            for (int n = 1; n <= _db.Count; n++)
            {
                LevelData level = Level(n);
                Assert.Greater(SkillScore.MaxKillPoints(level), 0, "level " + n);
                Assert.Greater(SkillScore.ParSeconds(level), 10f, "level " + n + " par time");
                Assert.Less(SkillScore.ParSeconds(level), 240f, "level " + n + " par time");
            }
        }

        [Test]
        public void BossLevels_CountTheBossAtItsFullValue()
        {
            int boss = 0;
            for (int n = 1; n <= _db.Count; n++)
            {
                LevelData level = Level(n);
                if (!level.HasBossWave()) continue;
                boss++;
                Assert.GreaterOrEqual(SkillScore.MaxKillPoints(level), SkillScore.PointsBoss, "level " + n);
            }
            Assert.GreaterOrEqual(boss, 2, "The campaign has two boss levels.");
        }

        // ---------------------------------------------------------------- endless

        [Test]
        public void EndlessScore_RewardsWavesAndKills()
        {
            Assert.AreEqual(500, SkillScore.ForEndless(500, 1));
            Assert.AreEqual(500 + 100 * 9, SkillScore.ForEndless(500, 10));
            Assert.Greater(SkillScore.ForEndless(500, 11), SkillScore.ForEndless(500, 10));
            Assert.AreEqual(0, SkillScore.ForEndless(0, 0));
        }

        // ---------------------------------------------------------------- the rating

        [Test]
        public void Rating_IsTheSumOfEveryLevelsBest_PlusTheBestEndlessRun()
        {
            SaveManager.SetLevelBestScore("level_01", 300);
            SaveManager.SetLevelBestScore("level_02", 500);
            SaveManager.RecordEndlessRun(8, 1200);

            Assert.AreEqual(300 + 500 + 1200, SaveManager.GetSkillRating());
        }

        [Test]
        public void ReplayingALevel_OnlyHelpsWhenItBeatsTheBest()
        {
            Assert.IsTrue(SaveManager.SetLevelBestScore("level_01", 300), "A first clear is a record.");
            Assert.IsFalse(SaveManager.SetLevelBestScore("level_01", 250), "A worse run changes nothing.");
            Assert.IsFalse(SaveManager.SetLevelBestScore("level_01", 300), "Neither does an equal one.");
            Assert.AreEqual(300, SaveManager.GetSkillRating());

            Assert.IsTrue(SaveManager.SetLevelBestScore("level_01", 410));
            Assert.AreEqual(410, SaveManager.GetSkillRating(), "The best REPLACES the old one, it is not added to it.");
        }

        [Test]
        public void GrindingAnEasyLevel_CannotClimbTheBoard()
        {
            long before = 0;
            for (int run = 0; run < 100; run++)
            {
                SaveManager.SetLevelBestScore("level_01", 340);
                if (run == 0) before = SaveManager.GetSkillRating();
            }
            Assert.AreEqual(before, SaveManager.GetSkillRating(), "100 identical clears must be worth exactly one.");
        }

        [Test]
        public void ZeroScores_AreNotRecorded()
        {
            Assert.IsFalse(SaveManager.SetLevelBestScore("level_01", 0));
            Assert.AreEqual(0, SaveManager.GetSkillRating());
        }

        [Test]
        public void LeaderboardNumber_IsTheSkillRating()
        {
            SaveManager.SetLevelBestScore("level_03", 777);
            Assert.AreEqual(777, SpaceHawk.Online.LeaderboardManager.GetLocalScore());
        }

        // ---------------------------------------------------------------- migration

        [Test]
        public void Migration_CreditsClearedLevels_FromTheirStars_Once()
        {
            LevelData one = Level(1);
            LevelData two = Level(2);
            SaveManager.SetStars(one.levelId, 3);
            SaveManager.SetStars(two.levelId, 1);

            SkillScore.MigrateLegacyScores(_db);

            Assert.AreEqual(SkillScore.EstimateLegacy(one, 3), SaveManager.GetLevelBestScore(one.levelId));
            Assert.AreEqual(SkillScore.EstimateLegacy(two, 1), SaveManager.GetLevelBestScore(two.levelId));
            Assert.AreEqual(0, SaveManager.GetLevelBestScore(Level(3).levelId), "A level never cleared gets nothing.");
            Assert.IsTrue(SaveManager.Data.skillScoresMigrated);

            long rating = SaveManager.GetSkillRating();
            SkillScore.MigrateLegacyScores(_db);
            Assert.AreEqual(rating, SaveManager.GetSkillRating(), "Running it again must not change anything.");
        }

        [Test]
        public void Migration_NeverOverwritesARealScore()
        {
            LevelData one = Level(1);
            SaveManager.SetStars(one.levelId, 3);
            SaveManager.SetLevelBestScore(one.levelId, 99999);

            SkillScore.MigrateLegacyScores(_db);

            Assert.AreEqual(99999, SaveManager.GetLevelBestScore(one.levelId));
        }

        [Test]
        public void Migration_GivesMoreStarsMoreCredit_ButLessThanAGreatRun()
        {
            LevelData level = Level(4);
            int oneStar = SkillScore.EstimateLegacy(level, 1);
            int threeStars = SkillScore.EstimateLegacy(level, 3);
            int greatRun = SkillScore.ForLevel(level, SkillScore.MaxKillPoints(level), 0f, 100, SkillScore.ParSeconds(level)).total;

            Assert.Greater(threeStars, oneStar);
            Assert.Less(threeStars, greatRun, "Replaying a legacy level must always have something to gain.");
        }

        // ---------------------------------------------------------------- damage tracking

        [Test]
        public void Health_CountsDamageActuallyLost_NotOverkillOrBlockedHits()
        {
            GameObject go = new GameObject("TestHealth");
            try
            {
                Health health = go.AddComponent<Health>();
                health.SetMax(100);

                health.TakeDamage(30);
                Assert.AreEqual(30, health.TotalDamageTaken);

                health.SetInvincible(true);
                health.TakeDamage(50);
                Assert.AreEqual(30, health.TotalDamageTaken, "A blocked hit costs nothing.");
                health.SetInvincible(false);

                health.Heal(20);
                Assert.AreEqual(30, health.TotalDamageTaken, "Healing doesn't undo damage already taken.");

                health.TakeDamage(9999);
                Assert.AreEqual(30 + 90, health.TotalDamageTaken, "Overkill only counts the HP that was left.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Health_DamageReduction_IsAppliedBeforeCounting()
        {
            GameObject go = new GameObject("TestHealth");
            try
            {
                Health health = go.AddComponent<Health>();
                health.SetMax(100);
                health.SetDamageReduction(0.5f);

                health.TakeDamage(40);

                Assert.AreEqual(20, health.TotalDamageTaken);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
