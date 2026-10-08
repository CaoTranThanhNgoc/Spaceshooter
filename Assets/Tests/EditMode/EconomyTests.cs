using System;
using NUnit.Framework;
using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Gameplay;

namespace SpaceHawk.Tests
{
    /// <summary>Daily login streak, energy refills and the revive mechanic's Health support.</summary>
    public class EconomyTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
        }

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ---------------------------------------------------------------- daily streak

        [Test]
        public void DailyReward_FirstClaimIsDayOne()
        {
            Assert.AreEqual(1, SaveManager.GetNextDailyRewardDay());
            Assert.AreEqual(SaveManager.GetDailyRewardAmount(1), SaveManager.TryClaimDailyReward());
            Assert.IsFalse(SaveManager.CanClaimDailyReward(), "A second claim the same day must be refused.");
            Assert.AreEqual(0, SaveManager.TryClaimDailyReward());
        }

        [Test]
        public void DailyReward_NextDayEscalates()
        {
            SaveManager.TryClaimDailyReward();
            SaveManager.Data.lastDailyRewardUnixSeconds = Now - SaveManager.DailyRewardIntervalSeconds - 60;

            Assert.AreEqual(2, SaveManager.GetNextDailyRewardDay());
            Assert.AreEqual(SaveManager.GetDailyRewardAmount(2), SaveManager.TryClaimDailyReward());
            Assert.Greater(SaveManager.GetDailyRewardAmount(2), SaveManager.GetDailyRewardAmount(1));
        }

        [Test]
        public void DailyReward_MissingDaysResetsStreak()
        {
            SaveManager.Data.dailyStreak = 4;
            SaveManager.Data.lastDailyRewardUnixSeconds = Now - 3 * SaveManager.DailyRewardIntervalSeconds;

            Assert.AreEqual(1, SaveManager.GetNextDailyRewardDay());
        }

        [Test]
        public void DailyReward_AfterLastDayWrapsToDayOne()
        {
            SaveManager.Data.dailyStreak = SaveManager.DailyRewardDays;
            SaveManager.Data.lastDailyRewardUnixSeconds = Now - SaveManager.DailyRewardIntervalSeconds - 60;

            Assert.AreEqual(1, SaveManager.GetNextDailyRewardDay());
        }

        [Test]
        public void DailyReward_AmountsNeverDecreaseAcrossTheWeek()
        {
            for (int day = 2; day <= SaveManager.DailyRewardDays; day++)
                Assert.GreaterOrEqual(SaveManager.GetDailyRewardAmount(day), SaveManager.GetDailyRewardAmount(day - 1));
        }

        // ---------------------------------------------------------------- energy

        [Test]
        public void AddEnergy_NeverExceedsMax()
        {
            SaveManager.AddEnergy(SaveManager.MaxEnergy * 3);
            Assert.AreEqual(SaveManager.MaxEnergy, SaveManager.GetEnergy());
        }

        [Test]
        public void AddEnergy_AddsOnTopOfCurrent()
        {
            SaveManager.TrySpendEnergy(6);
            int before = SaveManager.GetEnergy();
            SaveManager.AddEnergy(3);
            Assert.AreEqual(before + 3, SaveManager.GetEnergy());
        }

        [Test]
        public void RefillWithCrystals_FillsEnergyAndChargesTheCost()
        {
            SaveManager.TrySpendEnergy(SaveManager.MaxEnergy);
            SaveManager.AddCrystals(SaveManager.EnergyRefillCrystalCost);
            int crystalsBefore = SaveManager.GetCrystals();

            Assert.IsTrue(SaveManager.TryRefillEnergyWithCrystals());
            Assert.AreEqual(SaveManager.MaxEnergy, SaveManager.GetEnergy());
            Assert.AreEqual(crystalsBefore - SaveManager.EnergyRefillCrystalCost, SaveManager.GetCrystals());
        }

        [Test]
        public void RefillWithCrystals_RefusedWhenEnergyAlreadyFull_AndNothingCharged()
        {
            int crystalsBefore = SaveManager.GetCrystals();
            Assert.IsFalse(SaveManager.TryRefillEnergyWithCrystals());
            Assert.AreEqual(crystalsBefore, SaveManager.GetCrystals());
        }

        [Test]
        public void RefillWithCrystals_RefusedWhenTooPoor()
        {
            SaveManager.TrySpendEnergy(SaveManager.MaxEnergy);
            SaveManager.TrySpendCrystals(SaveManager.GetCrystals());

            Assert.IsFalse(SaveManager.TryRefillEnergyWithCrystals());
            Assert.AreEqual(0, SaveManager.GetEnergy());
        }

        // ---------------------------------------------------------------- revive

        [Test]
        public void Health_Revive_BringsBackAPartOfMaxHp()
        {
            GameObject go = new GameObject("TestHealth");
            try
            {
                Health health = go.AddComponent<Health>();
                health.SetMax(100);
                health.TakeDamage(500);
                Assert.IsTrue(health.IsDead);

                health.Revive(0.6f);

                Assert.IsFalse(health.IsDead);
                Assert.AreEqual(60, health.CurrentHp);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Health_Revive_DoesNothingWhileAlive()
        {
            GameObject go = new GameObject("TestHealth");
            try
            {
                Health health = go.AddComponent<Health>();
                health.SetMax(100);
                health.TakeDamage(30);

                health.Revive(1f);

                Assert.AreEqual(70, health.CurrentHp, "Reviving a living ship must not heal it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Health_Revive_DiesAgainNormally()
        {
            GameObject go = new GameObject("TestHealth");
            try
            {
                Health health = go.AddComponent<Health>();
                int deaths = 0;
                health.Died += () => deaths++;
                health.SetMax(100);

                health.TakeDamage(500);
                health.Revive(0.5f);
                health.TakeDamage(500);

                Assert.AreEqual(2, deaths);
                Assert.IsTrue(health.IsDead);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
