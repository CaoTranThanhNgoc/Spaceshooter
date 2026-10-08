using NUnit.Framework;
using SpaceHawk.Core;

namespace SpaceHawk.Tests
{
    public class SaveManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
        }

        [Test]
        public void AddCrystals_IncreasesBalance()
        {
            int before = SaveManager.GetCrystals();
            SaveManager.AddCrystals(50);
            Assert.AreEqual(before + 50, SaveManager.GetCrystals());
        }

        [Test]
        public void TrySpendCrystals_FailsWhenInsufficient()
        {
            bool result = SaveManager.TrySpendCrystals(SaveManager.GetCrystals() + 1000);
            Assert.IsFalse(result);
        }

        [Test]
        public void TrySpendCrystals_SucceedsWhenAffordable()
        {
            SaveManager.AddCrystals(100);
            int before = SaveManager.GetCrystals();
            bool result = SaveManager.TrySpendCrystals(30);
            Assert.IsTrue(result);
            Assert.AreEqual(before - 30, SaveManager.GetCrystals());
        }

        [Test]
        public void TrySpendEnergy_DeductsWhenAvailable()
        {
            int before = SaveManager.GetEnergy();
            bool result = SaveManager.TrySpendEnergy(1);
            Assert.IsTrue(result);
            Assert.AreEqual(before - 1, SaveManager.GetEnergy());
        }

        [Test]
        public void TrySpendEnergy_FailsWhenNotEnough()
        {
            bool result = SaveManager.TrySpendEnergy(SaveManager.MaxEnergy + 1);
            Assert.IsFalse(result);
        }

        [Test]
        public void Stars_OnlyIncreaseNeverDecrease()
        {
            SaveManager.SetStars("level_01", 2);
            SaveManager.SetStars("level_01", 1);
            Assert.AreEqual(2, SaveManager.GetStars("level_01"));
        }

        [Test]
        public void UnlockLevel_OnlyMovesForward()
        {
            SaveManager.UnlockLevel(3);
            SaveManager.UnlockLevel(1);
            Assert.IsTrue(SaveManager.IsLevelUnlocked(3));
            Assert.IsFalse(SaveManager.IsLevelUnlocked(4));
        }
    }
}
