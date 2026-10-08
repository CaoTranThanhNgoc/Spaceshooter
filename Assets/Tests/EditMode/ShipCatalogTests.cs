using NUnit.Framework;
using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.Tests
{
    public class ShipCatalogTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
        }

        [Test]
        public void TheRoster_IsFifteenShips_ThreeFamiliesOfFiveTiers()
        {
            Assert.AreEqual(15, ShipCatalog.Count);
            Assert.AreEqual(ShipCatalog.Count, SaveManager.ShipCount);
            for (int i = 0; i < ShipCatalog.Count; i++)
            {
                ShipSpec s = ShipCatalog.Get(i);
                Assert.AreEqual(i, s.index);
                Assert.AreEqual(i, ShipCatalog.IndexOf(s.family, s.tier));
            }
        }

        [Test]
        public void OnlyTheStarter_IsFree()
        {
            Assert.AreEqual(0, ShipCatalog.Get(0).price);
            for (int i = 1; i < ShipCatalog.Count; i++)
                Assert.GreaterOrEqual(ShipCatalog.Get(i).price, 1000, $"Ship {i} is too cheap - buying should take real effort.");
        }

        [Test]
        public void EachTier_CostsMoreAndAsksMoreOfTheCampaign_ThanTheOneBefore()
        {
            for (int f = 0; f < ShipCatalog.FamilyCount; f++)
            {
                for (int t = 1; t < ShipCatalog.TiersPerFamily; t++)
                {
                    ShipSpec prev = ShipCatalog.Get(ShipCatalog.IndexOf(f, t - 1));
                    ShipSpec cur = ShipCatalog.Get(ShipCatalog.IndexOf(f, t));
                    Assert.Greater(cur.price, prev.price, $"Family {f} tier {t}: price must rise.");
                    Assert.Greater(cur.requiresClearedLevels, prev.requiresClearedLevels, $"Family {f} tier {t}: the campaign gate must rise.");
                }
            }
        }

        [Test]
        public void EveryShip_IsWorthSomethingOverTheStarter_AndTheFamiliesDiffer()
        {
            for (int i = 1; i < ShipCatalog.Count; i++)
            {
                ShipSpec s = ShipCatalog.Get(i);
                bool better = s.hp > 1f || s.dmg > 1f || s.rate > 1f || s.HasPerks;
                Assert.IsTrue(better, $"Ship {i} offers nothing over the starter.");
            }

            // The families have different jobs: tankiest, hardest-hitting, fastest-firing top tiers.
            ShipSpec hawk = ShipCatalog.Get(ShipCatalog.IndexOf(0, 4));
            ShipSpec viper = ShipCatalog.Get(ShipCatalog.IndexOf(1, 4));
            ShipSpec phantom = ShipCatalog.Get(ShipCatalog.IndexOf(2, 4));
            Assert.Greater(viper.hp, hawk.hp);
            Assert.Greater(viper.hp, phantom.hp);
            Assert.Greater(phantom.dmg, hawk.dmg);
            Assert.Greater(phantom.dmg, viper.dmg);
            Assert.Greater(hawk.rate, viper.rate);
            Assert.Greater(hawk.rate, phantom.rate);
        }

        [Test]
        public void ShipStats_MultiplyTheSharedUpgradeLevel()
        {
            int viper = ShipCatalog.IndexOf(1, 0);
            Assert.AreEqual(100, SaveManager.GetShipMaxHp(0), "The starter keeps the plain numbers.");
            Assert.AreEqual(120, SaveManager.GetShipMaxHp(viper));

            SaveManager.AddCrystals(10000);
            SaveManager.TryUpgradeShip();
            Assert.AreEqual(Mathf.RoundToInt(125 * 1.2f), SaveManager.GetShipMaxHp(viper));
        }

        [Test]
        public void BuyingAShip_NeedsThePreviousTier_TheCampaignAndTheCrystals()
        {
            int hawk2 = ShipCatalog.IndexOf(0, 1);
            int hawk3 = ShipCatalog.IndexOf(0, 2);
            ShipSpec spec2 = ShipCatalog.Get(hawk2);
            ShipSpec spec3 = ShipCatalog.Get(hawk3);

            // Rich but early in the campaign: refused.
            SaveManager.AddCrystals(100000);
            Assert.AreEqual(SaveManager.ShipRequirement.ClearedLevels, SaveManager.GetShipRequirement(hawk2, out int levels));
            Assert.AreEqual(spec2.requiresClearedLevels, levels);
            Assert.IsFalse(SaveManager.TryUnlockShip(hawk2));
            Assert.AreEqual(100100, SaveManager.GetCrystals(), "A refused purchase must not cost anything.");

            // Far enough in the campaign, but tier III before tier II: refused.
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            Assert.AreEqual(SaveManager.ShipRequirement.PreviousShip, SaveManager.GetShipRequirement(hawk3, out int previous));
            Assert.AreEqual(hawk2, previous);
            Assert.IsFalse(SaveManager.TryUnlockShip(hawk3));

            // In order: fine, and it costs exactly the price.
            int before = SaveManager.GetCrystals();
            Assert.IsTrue(SaveManager.TryUnlockShip(hawk2));
            Assert.AreEqual(before - spec2.price, SaveManager.GetCrystals());
            Assert.IsTrue(SaveManager.TryUnlockShip(hawk3));
            Assert.AreEqual(before - spec2.price - spec3.price, SaveManager.GetCrystals());

            // Short of Crystals: refused, nothing taken.
            SaveManager.ResetForTests();
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            SaveManager.Data.crystals = spec2.price - 1;
            Assert.IsFalse(SaveManager.TryUnlockShip(hawk2));
            Assert.AreEqual(spec2.price - 1, SaveManager.GetCrystals());
        }

        [Test]
        public void OnlyOwnedShips_CanBeWorn_AndWearingNeverCosts()
        {
            int viper = ShipCatalog.IndexOf(1, 0);
            SaveManager.SelectShip(viper);
            Assert.AreEqual(0, SaveManager.GetSelectedShip(), "A ship that is not owned cannot be worn.");

            SaveManager.AddCrystals(100000);
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            SaveManager.TryUnlockShip(viper);
            int crystals = SaveManager.GetCrystals();
            SaveManager.SelectShip(viper);
            SaveManager.SelectShip(0);
            SaveManager.SelectShip(viper);
            Assert.AreEqual(viper, SaveManager.GetSelectedShip());
            Assert.AreEqual(crystals, SaveManager.GetCrystals());
        }

        [Test]
        public void AnOldSave_KeepsTheShipsItHasFlown()
        {
            // Before the roster: hull 2 (Phantom) bought, upgrade level 3, wearing hull 2 - which then
            // showed Phantom's level-3 sprite. Hull 1 (Viper) was never bought.
            SaveData old = new SaveData
            {
                shipLevel = 3,
                selectedShip = 2,
                unlockedShips = new[] { true, false, true },
                ownedShips = null,
                shipRosterVersion = 0,
            };
            SaveManager.ApplyCloudData(JsonUtility.ToJson(old));

            for (int tier = 0; tier < 3; tier++)
            {
                Assert.IsTrue(SaveManager.IsShipUnlocked(ShipCatalog.IndexOf(0, tier)), $"Hawk tier {tier} should be kept.");
                Assert.IsTrue(SaveManager.IsShipUnlocked(ShipCatalog.IndexOf(2, tier)), $"Phantom tier {tier} should be kept.");
                Assert.IsFalse(SaveManager.IsShipUnlocked(ShipCatalog.IndexOf(1, tier)), "Viper was never bought.");
            }
            Assert.IsFalse(SaveManager.IsShipUnlocked(ShipCatalog.IndexOf(0, 3)));
            Assert.AreEqual(ShipCatalog.IndexOf(2, 2), SaveManager.GetSelectedShip(), "The look the player had stays on screen.");
            Assert.AreEqual(3, SaveManager.GetShipLevel());
        }
    }
}
