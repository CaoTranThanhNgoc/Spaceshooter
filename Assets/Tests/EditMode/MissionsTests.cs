using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpaceHawk.Core;

namespace SpaceHawk.Tests
{
    public class MissionsTests
    {
        private DateTime _today;

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            _today = new DateTime(2026, 10, 5, 12, 0, 0);
            DailyMissions.Clock = () => _today;
        }

        [TearDown]
        public void TearDown()
        {
            DailyMissions.Clock = () => DateTime.Now;
        }

        private static int IndexOfKind(MissionKind kind)
        {
            for (int i = 0; i < DailyMissions.Count; i++)
                if (DailyMissions.Get(i).kind == kind) return i;
            return -1;
        }

        /// <summary>Moves the pretend date forward until today's missions include `kind`.</summary>
        private void AdvanceToADayWith(MissionKind kind)
        {
            for (int guard = 0; guard < 60 && IndexOfKind(kind) < 0; guard++) _today = _today.AddDays(1);
            Assert.GreaterOrEqual(IndexOfKind(kind), 0, "No day in two months offered " + kind);
        }

        [Test]
        public void ThreeDistinctMissionsEveryDay()
        {
            for (int day = 0; day < 40; day++)
            {
                HashSet<MissionKind> kinds = new HashSet<MissionKind>();
                for (int i = 0; i < DailyMissions.Count; i++)
                {
                    MissionInfo info = DailyMissions.Get(i);
                    Assert.IsTrue(kinds.Add(info.kind), "Duplicate mission kind on day " + day);
                    Assert.Greater(info.target, 0);
                    Assert.Greater(info.reward, 0);
                }
                _today = _today.AddDays(1);
            }
        }

        [Test]
        public void SameDayAlwaysGivesTheSameMissions()
        {
            MissionInfo first = DailyMissions.Get(0);
            SaveManager.ResetForTests();
            DailyMissions.Clock = () => _today;

            MissionInfo again = DailyMissions.Get(0);
            Assert.AreEqual(first.kind, again.kind);
            Assert.AreEqual(first.target, again.target);
            Assert.AreEqual(first.reward, again.reward);
        }

        [Test]
        public void DifferentDaysVaryTheMissions()
        {
            HashSet<string> seen = new HashSet<string>();
            for (int day = 0; day < 30; day++)
            {
                MissionInfo info = DailyMissions.Get(0);
                seen.Add(info.kind + ":" + info.target);
                _today = _today.AddDays(1);
            }
            Assert.Greater(seen.Count, 3, "The first mission slot should not be the same every day.");
        }

        [Test]
        public void NewDayWipesProgressAndClaims()
        {
            int index = 0;
            MissionInfo info = DailyMissions.Get(index);
            DailyMissions.Report(info.kind, info.target);
            Assert.IsTrue(DailyMissions.IsComplete(index));
            Assert.IsTrue(DailyMissions.TryClaim(index));

            _today = _today.AddDays(1);

            Assert.AreEqual(0, DailyMissions.GetProgress(0));
            Assert.IsFalse(DailyMissions.IsClaimed(0));
            Assert.IsFalse(DailyMissions.IsBonusClaimed);
        }

        [Test]
        public void Report_OnlyTouchesMatchingMissions_AndCapsAtTheTarget()
        {
            int index = 1;
            MissionInfo info = DailyMissions.Get(index);

            DailyMissions.Report(info.kind, info.target * 5);

            Assert.AreEqual(info.target, DailyMissions.GetProgress(index));
            for (int i = 0; i < DailyMissions.Count; i++)
                if (i != index) Assert.AreEqual(0, DailyMissions.GetProgress(i), "A different mission moved.");
        }

        [Test]
        public void Report_IgnoresZeroAndNegativeAmounts()
        {
            MissionInfo info = DailyMissions.Get(0);
            DailyMissions.Report(info.kind, 0);
            DailyMissions.Report(info.kind, -5);
            Assert.AreEqual(0, DailyMissions.GetProgress(0));
        }

        [Test]
        public void ReportMax_KeepsTheBestValueOnly()
        {
            AdvanceToADayWith(MissionKind.EndlessWave);
            int index = IndexOfKind(MissionKind.EndlessWave);
            int target = DailyMissions.Get(index).target;

            DailyMissions.ReportMax(MissionKind.EndlessWave, 3);
            DailyMissions.ReportMax(MissionKind.EndlessWave, 2);
            Assert.AreEqual(Math.Min(3, target), DailyMissions.GetProgress(index));

            DailyMissions.ReportMax(MissionKind.EndlessWave, target + 4);
            Assert.AreEqual(target, DailyMissions.GetProgress(index));
        }

        [Test]
        public void Claim_PaysOnce_AndOnlyWhenComplete()
        {
            MissionInfo info = DailyMissions.Get(0);
            int before = SaveManager.GetCrystals();

            Assert.IsFalse(DailyMissions.TryClaim(0), "Incomplete missions can't be claimed.");
            Assert.AreEqual(before, SaveManager.GetCrystals());

            DailyMissions.Report(info.kind, info.target);
            Assert.IsTrue(DailyMissions.TryClaim(0));
            Assert.AreEqual(before + info.reward, SaveManager.GetCrystals());

            Assert.IsFalse(DailyMissions.TryClaim(0), "Claiming twice must not pay twice.");
            Assert.AreEqual(before + info.reward, SaveManager.GetCrystals());
        }

        [Test]
        public void Bonus_NeedsAllThree_AndPaysOnce()
        {
            int before = SaveManager.GetCrystals();
            for (int i = 0; i < DailyMissions.Count - 1; i++)
            {
                MissionInfo info = DailyMissions.Get(i);
                DailyMissions.Report(info.kind, info.target);
            }
            Assert.IsFalse(DailyMissions.CanClaimBonus, "Two of three isn't enough.");
            Assert.IsFalse(DailyMissions.TryClaimBonus());

            MissionInfo last = DailyMissions.Get(DailyMissions.Count - 1);
            DailyMissions.Report(last.kind, last.target);
            Assert.AreEqual(DailyMissions.Count, DailyMissions.CompletedCount);

            Assert.IsTrue(DailyMissions.TryClaimBonus());
            Assert.AreEqual(before + DailyMissions.BonusReward, SaveManager.GetCrystals());
            Assert.IsFalse(DailyMissions.TryClaimBonus());
        }

        [Test]
        public void MissionCompleted_FiresExactlyOnce_WhenTheTargetIsReached()
        {
            MissionInfo info = DailyMissions.Get(0);
            List<int> completed = new List<int>();
            Action<int> handler = completed.Add;
            DailyMissions.MissionCompleted += handler;
            try
            {
                DailyMissions.Report(info.kind, info.target - 1);
                Assert.AreEqual(0, completed.Count);

                DailyMissions.Report(info.kind, 1);
                DailyMissions.Report(info.kind, 1);

                CollectionAssert.AreEqual(new[] { 0 }, completed);
            }
            finally
            {
                DailyMissions.MissionCompleted -= handler;
            }
        }

        [Test]
        public void EveryKill_CountsTowardDestroyEnemiesMissions()
        {
            AdvanceToADayWith(MissionKind.DestroyEnemies);
            int index = IndexOfKind(MissionKind.DestroyEnemies);

            for (int i = 0; i < 5; i++) SaveManager.AddEnemyKill();

            Assert.AreEqual(Math.Min(5, DailyMissions.Get(index).target), DailyMissions.GetProgress(index));
        }

        [Test]
        public void Descriptions_MentionTheTarget_InBothLanguages()
        {
            Language original = Localization.Current;
            try
            {
                foreach (Language language in new[] { Language.English, Language.Vietnamese })
                {
                    Localization.SetLanguage(language);
                    for (int day = 0; day < 10; day++)
                    {
                        for (int i = 0; i < DailyMissions.Count; i++)
                        {
                            MissionInfo info = DailyMissions.Get(i);
                            Assert.IsFalse(string.IsNullOrEmpty(DailyMissions.GetTitle(info)));
                            StringAssert.Contains(info.target.ToString(), DailyMissions.Describe(info));
                            StringAssert.DoesNotContain("missions.kind", DailyMissions.Describe(info), "A localization key is missing.");
                        }
                        _today = _today.AddDays(1);
                    }
                }
            }
            finally
            {
                Localization.SetLanguage(original);
            }
        }

        [Test]
        public void TimeUntilReset_IsUnderADay_AndCountsToMidnight()
        {
            _today = new DateTime(2026, 10, 5, 23, 0, 0);
            Assert.AreEqual(TimeSpan.FromHours(1), DailyMissions.TimeUntilReset);

            _today = new DateTime(2026, 10, 5, 0, 0, 0);
            Assert.AreEqual(TimeSpan.FromHours(24), DailyMissions.TimeUntilReset);
        }
    }
}
