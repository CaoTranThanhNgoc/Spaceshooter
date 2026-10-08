using NUnit.Framework;
using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.Tests
{
    public class EndlessTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
        }

        [Test]
        public void EveryWave_IsWellFormed()
        {
            for (int wave = 1; wave <= 120; wave++)
            {
                EnemyWave[] groups = EndlessWaves.Build(wave);
                Assert.IsNotEmpty(groups, "Wave " + wave + " is empty.");
                foreach (EnemyWave g in groups)
                {
                    Assert.Greater(g.count, 0, "wave " + wave);
                    Assert.Greater(g.hp, 0, "wave " + wave);
                    Assert.Greater(g.speed, 0f, "wave " + wave);
                    Assert.Greater(g.spawnInterval, 0f, "wave " + wave);
                    Assert.Greater(g.fireInterval, 0.3f, "wave " + wave + ": enemies firing faster than 3 shots a second would be unfair");
                    Assert.Greater(g.bulletDamage, 0, "wave " + wave);
                }
            }
        }

        [Test]
        public void BossEveryTenthWave_AndOnlyThen()
        {
            for (int wave = 1; wave <= 60; wave++)
            {
                int bosses = 0;
                foreach (EnemyWave g in EndlessWaves.Build(wave)) if (g.kind == EnemyKind.Boss) bosses += g.count;
                Assert.AreEqual(wave % EndlessWaves.BossEvery == 0 ? 1 : 0, bosses, "wave " + wave);
                Assert.AreEqual(wave % EndlessWaves.BossEvery == 0, EndlessWaves.IsBossWave(wave));
            }
        }

        [Test]
        public void Difficulty_NeverEases_AsWavesClimb()
        {
            // Compare ordinary (non-boss) waves of the same shape: the first group is always Light.
            EnemyWave previous = EndlessWaves.Build(1)[0];
            for (int wave = 2; wave <= 60; wave++)
            {
                if (EndlessWaves.IsBossWave(wave)) continue;
                EnemyWave current = EndlessWaves.Build(wave)[0];
                Assert.GreaterOrEqual(current.hp, previous.hp, "hp eased at wave " + wave);
                Assert.GreaterOrEqual(current.speed, previous.speed - 0.0001f, "speed eased at wave " + wave);
                Assert.LessOrEqual(current.fireInterval, previous.fireInterval + 0.0001f, "fire rate eased at wave " + wave);
                Assert.GreaterOrEqual(current.bulletDamage, previous.bulletDamage, "bullet damage eased at wave " + wave);
                previous = current;
            }
        }

        [Test]
        public void LaterWavesBringMoreEnemies()
        {
            // Ordinary waves only - a boss wave deliberately swaps the crowd for one huge enemy.
            Assert.Greater(EndlessWaves.CountEnemies(14), EndlessWaves.CountEnemies(2));
            Assert.Greater(EndlessWaves.CountEnemies(28), EndlessWaves.CountEnemies(14));
        }

        [Test]
        public void HeavyEnemies_ArriveFromWaveThree()
        {
            bool HasHeavy(int wave)
            {
                foreach (EnemyWave g in EndlessWaves.Build(wave)) if (g.kind == EnemyKind.Heavy) return true;
                return false;
            }
            Assert.IsFalse(HasHeavy(1));
            Assert.IsFalse(HasHeavy(2));
            Assert.IsTrue(HasHeavy(3));
        }

        [Test]
        public void Bosses_GetTougherEachTime()
        {
            int Hp(int wave) { foreach (EnemyWave g in EndlessWaves.Build(wave)) if (g.kind == EnemyKind.Boss) return g.hp; return 0; }
            Assert.Greater(Hp(20), Hp(10));
            Assert.Greater(Hp(30), Hp(20));
        }

        [Test]
        public void Reward_PaysForSurvivedWaves_NotTheOneYouDiedIn()
        {
            Assert.AreEqual(0, EndlessWaves.RewardFor(1));
            Assert.Greater(EndlessWaves.RewardFor(2), 0);
            Assert.Greater(EndlessWaves.RewardFor(10), EndlessWaves.RewardFor(9));
            Assert.AreEqual(0, EndlessWaves.RewardFor(0));
        }

        [Test]
        public void BackgroundSet_StaysInRange_AndEventuallyChanges()
        {
            for (int wave = 1; wave <= 80; wave++)
            {
                int set = EndlessWaves.BackgroundSetFor(wave, 4);
                Assert.That(set, Is.InRange(0, 3));
            }
            Assert.AreNotEqual(EndlessWaves.BackgroundSetFor(1, 4), EndlessWaves.BackgroundSetFor(9, 4));
            Assert.AreEqual(0, EndlessWaves.BackgroundSetFor(5, 0));
        }

        [Test]
        public void Tint_IsAValidColour_ThatChangesPerWave()
        {
            Color a = EndlessWaves.TintFor(1);
            Color b = EndlessWaves.TintFor(2);
            Assert.AreNotEqual(a, b);
            Assert.AreEqual(1f, a.a);
        }

        [Test]
        public void EndlessLevel_IsOneSharedFlaggedInstance()
        {
            LevelData level = EndlessLevel.GetOrCreate();
            Assert.IsTrue(level.isEndless);
            Assert.AreEqual(EndlessWaves.EnergyCost, level.energyCost);
            Assert.AreEqual(0, level.GetRequiredKillCount());
            Assert.IsFalse(level.HasBossWave(), "Boss waves are generated on demand, not listed.");
            Assert.AreSame(level, EndlessLevel.GetOrCreate());
        }

        [Test]
        public void EndlessRun_RecordsBests_AndReportsNewWaveRecords()
        {
            Assert.IsTrue(SaveManager.RecordEndlessRun(5, 400), "First run is a record.");
            Assert.IsFalse(SaveManager.RecordEndlessRun(3, 900), "A lower wave is not a new wave record...");
            Assert.AreEqual(5, SaveManager.GetEndlessBestWave());
            Assert.AreEqual(900, SaveManager.GetEndlessBestScore(), "...but the score record still updates.");
            Assert.IsTrue(SaveManager.RecordEndlessRun(8, 100));
            Assert.AreEqual(8, SaveManager.GetEndlessBestWave());
            Assert.AreEqual(900, SaveManager.GetEndlessBestScore());
        }

        [Test]
        public void EndlessAchievement_TracksTheBestWave()
        {
            AchievementData data = ScriptableObject.CreateInstance<AchievementData>();
            try
            {
                data.kind = AchievementKind.EndlessWave;
                data.targetCount = 10;

                Assert.AreEqual(0, AchievementTracker.GetProgress(data));
                SaveManager.RecordEndlessRun(7, 0);
                Assert.AreEqual(7, AchievementTracker.GetProgress(data));

                (string title, string subtitle) = AchievementTracker.DescribeGoal(data);
                Assert.IsFalse(string.IsNullOrEmpty(title));
                StringAssert.Contains("10", subtitle);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
