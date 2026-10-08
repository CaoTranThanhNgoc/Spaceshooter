using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.Gameplay;
using SpaceHawk.UI;

namespace SpaceHawk.Tests.PlayMode
{
    /// <summary>Runs the real prefabs and scenes (not mocks): the Missions panel, the energy
    /// dialog, revive, and a whole Endless run in the actual Gameplay scene.</summary>
    public class FeaturePlayModeTests
    {
        private static readonly DateTime FixedDay = new DateTime(2026, 10, 5, 12, 0, 0);

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            DailyMissions.Clock = () => FixedDay;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            DailyMissions.Clock = () => DateTime.Now;
            Time.timeScale = 1f;
        }

        private static Transform NewOverlayRoot()
        {
            GameObject canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            return canvasGo.transform;
        }

        // ---------------------------------------------------------------- energy dialog

        [UnityTest]
        public IEnumerator EnergyDialog_CrystalRefill_FillsEnergy_OrExplainsWhenPoor()
        {
            SaveManager.TrySpendEnergy(SaveManager.MaxEnergy);
            SaveManager.TrySpendCrystals(SaveManager.GetCrystals());
            Transform root = NewOverlayRoot();

            EnergyRefillDialog.Show(root);
            EnergyRefillDialog dialog = root.GetComponentInChildren<EnergyRefillDialog>();
            Assert.IsNotNull(dialog, "EnergyRefillDialog prefab did not instantiate.");
            yield return null;

            dialog.refillButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, SaveManager.GetEnergy(), "Too poor - nothing may be refilled.");
            Assert.IsNotNull(root.GetComponentInChildren<EnergyRefillDialog>(), "The dialog should stay open.");

            SaveManager.AddCrystals(SaveManager.EnergyRefillCrystalCost);
            dialog.refillButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(SaveManager.MaxEnergy, SaveManager.GetEnergy());
            Assert.IsNull(root.GetComponentInChildren<EnergyRefillDialog>(), "The dialog should close once refilled.");
            UnityEngine.Object.Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- missions panel

        private static MissionsPanel OpenMissions(Transform root)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/MissionsPanel");
            Assert.IsNotNull(prefab, "MissionsPanel prefab missing.");
            return UnityEngine.Object.Instantiate(prefab, root).GetComponent<MissionsPanel>();
        }

        [UnityTest]
        public IEnumerator MissionsPanel_ShowsTodaysMissions_WithTheirRewards()
        {
            Transform root = NewOverlayRoot();
            MissionsPanel panel = OpenMissions(root);
            yield return null;

            Assert.AreEqual(DailyMissions.Count, panel.missionRows.Length);
            for (int i = 0; i < DailyMissions.Count; i++)
            {
                MissionInfo info = DailyMissions.Get(i);
                Assert.AreEqual(DailyMissions.GetTitle(info), panel.missionRows[i].titleLabel.text);
                Assert.AreEqual(DailyMissions.Describe(info), panel.missionRows[i].subtitleLabel.text);
                Assert.AreEqual(info.reward.ToString(), panel.missionRows[i].rewardLabel.text);
                Assert.AreEqual($"0 / {info.target}", panel.missionRows[i].progressLabel.text);
                Assert.IsFalse(panel.missionRows[i].statusButton.interactable, "Nothing is claimable before progress is made.");
            }
            Assert.AreEqual(DailyMissions.BonusReward.ToString(), panel.bonusRow.rewardLabel.text);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator MissionsPanel_ClaimFlow_PaysEachMission_ThenTheBonus()
        {
            Transform root = NewOverlayRoot();
            MissionsPanel panel = OpenMissions(root);
            yield return null;

            int expected = SaveManager.GetCrystals();
            for (int i = 0; i < DailyMissions.Count; i++)
            {
                MissionInfo info = DailyMissions.Get(i);
                DailyMissions.Report(info.kind, info.target);
                yield return null;

                Assert.IsTrue(panel.missionRows[i].statusButton.interactable, "A finished mission must become claimable.");
                panel.missionRows[i].statusButton.onClick.Invoke();
                expected += info.reward;
                Assert.AreEqual(expected, SaveManager.GetCrystals(), "Mission " + i + " paid the wrong amount.");
                Assert.IsFalse(panel.missionRows[i].statusButton.interactable, "A claimed mission must not be claimable again.");
            }

            Assert.IsTrue(panel.bonusRow.statusButton.interactable, "All three claimed - the bonus is ready.");
            panel.bonusRow.statusButton.onClick.Invoke();
            expected += DailyMissions.BonusReward;
            Assert.AreEqual(expected, SaveManager.GetCrystals());
            Assert.IsFalse(panel.bonusRow.statusButton.interactable);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator MissionsPanel_DailyLoginRow_ClaimsTheStreakReward()
        {
            Transform root = NewOverlayRoot();
            MissionsPanel panel = OpenMissions(root);
            yield return null;

            Assert.IsTrue(panel.dailyRow.statusButton.interactable, "Day 1 should be ready to collect.");
            int before = SaveManager.GetCrystals();

            panel.dailyRow.statusButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(before + SaveManager.GetDailyRewardAmount(1), SaveManager.GetCrystals());
            Assert.IsFalse(panel.dailyRow.statusButton.interactable, "Only one login reward per day.");
            Assert.AreEqual($"1 / {SaveManager.DailyRewardDays}", panel.dailyRow.progressLabel.text);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator MissionsPanel_UpdatesLiveWhenProgressHappensBehindIt()
        {
            Transform root = NewOverlayRoot();
            MissionsPanel panel = OpenMissions(root);
            yield return null;

            MissionInfo info = DailyMissions.Get(0);
            DailyMissions.Report(info.kind, 1);
            yield return null;

            Assert.AreEqual($"1 / {info.target}", panel.missionRows[0].progressLabel.text);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- real gameplay scene

        private static IEnumerator LoadLevel(LevelData level)
        {
            GameManager.StartLevel(level);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == GameManager.GameplaySceneName);
            yield return null;
            yield return null;
        }

        private static void SetCurrentWave(EnemySpawner spawner, int wave)
        {
            PropertyInfo property = typeof(EnemySpawner).GetProperty("CurrentWave", BindingFlags.Public | BindingFlags.Instance);
            property.GetSetMethod(true).Invoke(spawner, new object[] { wave });
        }

        [UnityTest]
        public IEnumerator Gameplay_Defeat_OffersRevive_AndTheShipComesBackToLife()
        {
            yield return LoadLevel(Resources.Load<LevelDatabase>("Data/LevelDatabase").GetByIndex(0));

            PlayerShip player = UnityEngine.Object.FindAnyObjectByType<PlayerShip>();
            Health health = player.GetComponent<Health>();
            health.TakeDamage(99999);
            yield return null;

            GameOverPanel panel = UnityEngine.Object.FindAnyObjectByType<GameOverPanel>();
            Assert.IsNotNull(panel, "Dying did not show the Game Over panel.");
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(panel.reviveRow.activeSelf, "A first defeat by destruction must offer a revive.");

            SaveManager.AddCrystals(SaveManager.ReviveCrystalCost);
            int crystalsBefore = SaveManager.GetCrystals();
            panel.reviveButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(health.IsDead);
            Assert.Greater(health.CurrentHp, 0);
            Assert.IsTrue(health.IsInvincible, "A revived ship needs a moment of protection.");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(crystalsBefore - SaveManager.ReviveCrystalCost, SaveManager.GetCrystals());
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<GameOverPanel>(), "The Game Over panel should be gone after reviving.");

            // The one revive per attempt is spent: dying again is final.
            health.SetInvincible(false);
            health.TakeDamage(99999);
            yield return null;
            GameOverPanel second = UnityEngine.Object.FindAnyObjectByType<GameOverPanel>();
            Assert.IsNotNull(second);
            Assert.IsFalse(second.reviveRow.activeSelf, "Only one revive is allowed per attempt.");
        }

        [UnityTest]
        public IEnumerator Gameplay_Revive_RefusedWhenTooPoor_AndNothingIsCharged()
        {
            yield return LoadLevel(Resources.Load<LevelDatabase>("Data/LevelDatabase").GetByIndex(0));

            Health health = UnityEngine.Object.FindAnyObjectByType<PlayerShip>().GetComponent<Health>();
            health.TakeDamage(99999);
            yield return null;
            GameOverPanel panel = UnityEngine.Object.FindAnyObjectByType<GameOverPanel>();

            SaveManager.TrySpendCrystals(SaveManager.GetCrystals());
            panel.reviveButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(health.IsDead, "Revive without the Crystals must not work.");
            Assert.AreEqual(0, SaveManager.GetCrystals());
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<GameOverPanel>());
        }

        [UnityTest]
        public IEnumerator Gameplay_PausesItselfWhenTheAppGoesToTheBackground()
        {
            yield return LoadLevel(Resources.Load<LevelDatabase>("Data/LevelDatabase").GetByIndex(0));
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<PauseMenu>());

            GameplayController controller = UnityEngine.Object.FindAnyObjectByType<GameplayController>();
            controller.SendMessage("OnApplicationPause", true);
            yield return null;

            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<PauseMenu>(), "Backgrounding the app must open the pause menu.");
            Assert.AreEqual(0f, Time.timeScale);

            controller.SendMessage("OnApplicationPause", true);
            yield return null;
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PauseMenu>().Length, "Repeated pauses must not stack menus.");
        }

        [UnityTest]
        public IEnumerator Gameplay_KillingEnemies_ProgressesTheDailyMission()
        {
            // Find a day whose missions include "destroy enemies" so the kill path is observable.
            DateTime day = FixedDay;
            for (int guard = 0; guard < 60; guard++)
            {
                DateTime captured = day;
                DailyMissions.Clock = () => captured;
                bool found = false;
                for (int i = 0; i < DailyMissions.Count; i++) found |= DailyMissions.Get(i).kind == MissionKind.DestroyEnemies;
                if (found) break;
                day = day.AddDays(1);
            }

            yield return LoadLevel(Resources.Load<LevelDatabase>("Data/LevelDatabase").GetByIndex(0));
            // Enemy waves only begin once the doors finish opening.
            float until = Time.realtimeSinceStartup + 10f;
            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            while (Time.realtimeSinceStartup < until && spawner.AliveCount == 0) yield return null;
            Assert.Greater(spawner.AliveCount, 0, "No enemy ever spawned.");

            int index = 0;
            for (int i = 0; i < DailyMissions.Count; i++) if (DailyMissions.Get(i).kind == MissionKind.DestroyEnemies) index = i;

            Enemy.Detonate(99999);
            yield return null;

            Assert.Greater(DailyMissions.GetProgress(index), 0, "Killing enemies did not advance the mission.");
        }

        [UnityTest]
        public IEnumerator Gameplay_ClearingALevel_ScoresIt_AndShowsHowItWasScored()
        {
            LevelData level = Resources.Load<LevelDatabase>("Data/LevelDatabase").GetByIndex(0);
            yield return LoadLevel(level);

            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            HUDController hud = UnityEngine.Object.FindAnyObjectByType<HUDController>();
            UnityEngine.Object.FindAnyObjectByType<PlayerShip>().GetComponent<Health>().SetInvincible(true);

            // Shoot everything down as it appears until the level ends in Victory.
            float until = Time.realtimeSinceStartup + 120f;
            VictoryPanel victory = null;
            bool hudScoreMoved = false;
            while (Time.realtimeSinceStartup < until && victory == null)
            {
                Enemy.Detonate(99999);
                if (spawner.TotalPoints > 0 && hud.scoreLabel.text.EndsWith(spawner.TotalPoints.ToString())) hudScoreMoved = true;
                // Realtime: Victory freezes Time.timeScale, which would stall a scaled wait forever.
                yield return new WaitForSecondsRealtime(0.25f);
                victory = UnityEngine.Object.FindAnyObjectByType<VictoryPanel>();
            }
            Assert.IsNotNull(victory, "The level never ended in Victory.");
            Assert.IsTrue(hudScoreMoved, "The HUD score never showed the points earned so far.");

            int stored = SaveManager.GetLevelBestScore(level.levelId);
            int killPoints = SkillScore.MaxKillPoints(level);
            Assert.Greater(stored, 0, "The clear was not scored.");
            Assert.GreaterOrEqual(stored, Mathf.RoundToInt(killPoints * 0.8f), "Nearly every enemy was destroyed - kill points alone should show.");
            Assert.LessOrEqual(stored, killPoints * 2, "Bonuses can at most double the kill points on level 1.");
            Assert.AreEqual(stored, SaveManager.GetSkillRating());
            Assert.AreEqual(stored, SpaceHawk.Online.LeaderboardManager.GetLocalScore(), "The Leaderboard must be fed the skill rating.");

            // The Victory screen explains the number: four breakdown rows, then the total.
            Assert.AreEqual(4, victory.breakdownValues.text.Split((char)10).Length, "Kills, health, speed, difficulty.");
            Assert.AreEqual(4, victory.breakdownLabels.text.Split((char)10).Length);
            Assert.AreEqual(stored.ToString(), victory.totalValue.text);
            Assert.AreEqual(level.displayNumber.ToString(), victory.medalNumberLabel.text);
            StringAssert.Contains(stored.ToString(), victory.ratingLabel.text, "The new skill rating is shown.");
            StringAssert.Contains(Localization.Get("endless.new_best"), victory.newBestLabel.text, "A first clear is a new best.");
        }

        // ---------------------------------------------------------------- endless

        [UnityTest]
        public IEnumerator Endless_SpawnsWaves_AndTheHudShowsTheWave()
        {
            yield return LoadLevel(EndlessLevel.GetOrCreate());

            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            float until = Time.realtimeSinceStartup + 14f;
            while (Time.realtimeSinceStartup < until && spawner.AliveCount < 3) yield return null;

            Assert.AreEqual(1, spawner.CurrentWave, "Endless should start at wave 1.");
            Assert.GreaterOrEqual(spawner.AliveCount, 3, "Wave 1 never put enemies on screen.");

            HUDController hud = UnityEngine.Object.FindAnyObjectByType<HUDController>();
            StringAssert.Contains("1", hud.levelLabel.text);
            StringAssert.DoesNotContain("LEVEL", hud.levelLabel.text.ToUpperInvariant(), "Endless is not a numbered level.");
        }

        [UnityTest]
        public IEnumerator Endless_Defeat_RecordsTheRun_PaysCrystals_AndNeverPaysTwiceAfterARevive()
        {
            yield return LoadLevel(EndlessLevel.GetOrCreate());

            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            PlayerShip player = UnityEngine.Object.FindAnyObjectByType<PlayerShip>();
            Health health = player.GetComponent<Health>();

            SetCurrentWave(spawner, 6);
            int before = SaveManager.GetCrystals();
            health.TakeDamage(99999);
            yield return null;

            GameOverPanel panel = UnityEngine.Object.FindAnyObjectByType<GameOverPanel>();
            Assert.IsNotNull(panel);
            Assert.AreEqual(6, SaveManager.GetEndlessBestWave());
            Assert.AreEqual(before + EndlessWaves.RewardFor(6), SaveManager.GetCrystals());
            StringAssert.Contains("6", panel.reasonLabel.text);
            StringAssert.Contains(Localization.Get("endless.new_best"), panel.reasonLabel.text);
            Assert.IsTrue(panel.reviveRow.activeSelf);

            // Revive, push on to wave 8 and die again: only the difference is paid.
            SaveManager.AddCrystals(SaveManager.ReviveCrystalCost);
            int afterFirst = SaveManager.GetCrystals();
            panel.reviveButton.onClick.Invoke();
            yield return null;
            SetCurrentWave(spawner, 8);
            health.SetInvincible(false);
            health.TakeDamage(99999);
            yield return null;

            Assert.AreEqual(8, SaveManager.GetEndlessBestWave());
            Assert.AreEqual(afterFirst - SaveManager.ReviveCrystalCost + (EndlessWaves.RewardFor(8) - EndlessWaves.RewardFor(6)),
                SaveManager.GetCrystals(), "The second death must only pay for the extra waves.");
        }

        [UnityTest]
        public IEnumerator Endless_TheWaveMissionAdvances_AsWavesStart()
        {
            DateTime day = FixedDay;
            for (int guard = 0; guard < 60; guard++)
            {
                DateTime captured = day;
                DailyMissions.Clock = () => captured;
                bool found = false;
                for (int i = 0; i < DailyMissions.Count; i++) found |= DailyMissions.Get(i).kind == MissionKind.EndlessWave;
                if (found) break;
                day = day.AddDays(1);
            }

            yield return LoadLevel(EndlessLevel.GetOrCreate());
            float until = Time.realtimeSinceStartup + 14f;
            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            while (Time.realtimeSinceStartup < until && spawner.CurrentWave < 1) yield return null;

            int index = 0;
            for (int i = 0; i < DailyMissions.Count; i++) if (DailyMissions.Get(i).kind == MissionKind.EndlessWave) index = i;
            Assert.AreEqual(1, DailyMissions.GetProgress(index), "Reaching wave 1 should read as progress 1.");
        }

        private static InventoryPanel OpenInventory()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/InventoryPanel");
            Assert.IsNotNull(prefab);
            return UnityEngine.Object.Instantiate(prefab, NewOverlayRoot()).GetComponent<InventoryPanel>();
        }

        [UnityTest]
        public IEnumerator Inventory_BoughtShips_CanBeWornAgainForFree()
        {
            SaveManager.Data.crystals = 5000;
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            InventoryPanel panel = OpenInventory();
            yield return null;

            int hawk2 = ShipCatalog.IndexOf(0, 1);

            // Looking at a ship that is not owned costs nothing and changes nothing.
            panel.cardButtons[1].onClick.Invoke();
            Assert.AreEqual(hawk2, panel.PreviewIndex);
            Assert.AreEqual(5000, SaveManager.GetCrystals());
            Assert.AreEqual(0, SaveManager.GetSelectedShip());
            Assert.IsFalse(SaveManager.IsShipUnlocked(hawk2));

            // Buying it equips it, for exactly its price.
            panel.hullActionButton.onClick.Invoke();
            Assert.IsTrue(SaveManager.IsShipUnlocked(hawk2));
            Assert.AreEqual(hawk2, SaveManager.GetSelectedShip());
            int afterBuying = SaveManager.GetCrystals();
            Assert.AreEqual(5000 - ShipCatalog.Get(hawk2).price, afterBuying);

            // Back to the starter and to the bought ship again: free both times.
            panel.cardButtons[0].onClick.Invoke();
            Assert.IsTrue(panel.hullActionButton.interactable, "An owned ship that isn't worn must offer EQUIP.");
            panel.hullActionButton.onClick.Invoke();
            Assert.AreEqual(0, SaveManager.GetSelectedShip());

            panel.cardButtons[1].onClick.Invoke();
            Assert.IsTrue(panel.hullActionButton.interactable);
            panel.hullActionButton.onClick.Invoke();
            Assert.AreEqual(hawk2, SaveManager.GetSelectedShip());
            Assert.AreEqual(afterBuying, SaveManager.GetCrystals(), "Wearing a ship that was already bought must never cost Crystals.");

            Assert.IsFalse(panel.hullActionButton.interactable, "The worn ship has nothing left to do.");
            Assert.IsTrue(SaveManager.IsShipUnlocked(hawk2), "A bought ship stays owned.");
        }

        [UnityTest]
        public IEnumerator Inventory_FamilyTabs_ShowTheOtherShips()
        {
            InventoryPanel panel = OpenInventory();
            yield return null;
            Assert.AreEqual(0, ShipCatalog.FamilyOf(panel.PreviewIndex));

            panel.familyTabs[2].onClick.Invoke();
            Assert.AreEqual(ShipCatalog.IndexOf(2, 0), panel.PreviewIndex);
            panel.cardButtons[3].onClick.Invoke();
            Assert.AreEqual(ShipCatalog.IndexOf(2, 3), panel.PreviewIndex, "Cards must address the open family.");
        }

        [UnityTest]
        public IEnumerator Inventory_ShipThatIsNotAvailableYet_CannotBeBought()
        {
            SaveManager.Data.crystals = 100000;   // plenty, but the campaign is untouched
            InventoryPanel panel = OpenInventory();
            yield return null;

            int phantom1 = ShipCatalog.IndexOf(2, 0);
            panel.PreviewShip(phantom1);
            Assert.IsFalse(panel.hullActionButton.interactable, "A ship gated by the campaign must show a disabled button.");
            panel.hullActionButton.onClick.Invoke();
            Assert.IsFalse(SaveManager.IsShipUnlocked(phantom1));
            Assert.AreEqual(100000, SaveManager.GetCrystals());

            // A later tier of a family needs the earlier one first, whatever the campaign says.
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            panel.PreviewShip(ShipCatalog.IndexOf(2, 2));
            Assert.IsFalse(panel.hullActionButton.interactable);
        }

        [UnityTest]
        public IEnumerator Inventory_ShipWithoutEnoughCrystals_NeedsMore()
        {
            SaveManager.Data.crystals = 10;
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            InventoryPanel panel = OpenInventory();
            yield return null;

            int viper1 = ShipCatalog.IndexOf(1, 0);
            panel.PreviewShip(viper1);
            panel.hullActionButton.onClick.Invoke();
            Assert.IsFalse(SaveManager.IsShipUnlocked(viper1));
            Assert.AreEqual(0, SaveManager.GetSelectedShip());
            Assert.AreEqual(10, SaveManager.GetCrystals());
        }

        [UnityTest]
        public IEnumerator Ship_TheWornShip_DrivesTheSpawnedPlayer()
        {
            SaveManager.Data.crystals = 100000;
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            int viper = ShipCatalog.IndexOf(1, 0);
            Assert.IsTrue(SaveManager.TryUnlockShip(viper));
            SaveManager.SelectShip(viper);
            SaveManager.TryUpgradeShip();

            yield return LoadLevel(LevelDatabase_First());
            PlayerShip ship = UnityEngine.Object.FindAnyObjectByType<PlayerShip>();
            Assert.AreEqual(viper, ship.Spec.index);
            Assert.AreSame(ship.hulls[1].levelSprites[0], ship.bodySprite.sprite, "The spawned player must look like the worn ship.");
            Health health = ship.GetComponent<Health>();
            Assert.AreEqual(SaveManager.GetShipMaxHp(), health.maxHp, "Max HP = upgrade level x the ship's own multiplier.");
            Assert.AreEqual(SaveManager.GetShipDamage(), ship.bulletDamage);
            Assert.AreEqual(0.9f, health.DamageReductionMultiplier, 0.001f, "Viper's armour plating must be active.");
        }

        [UnityTest]
        public IEnumerator Ship_TwinCannons_FireTwoBullets()
        {
            // Hawk III has twin cannons, Phantom I piercing shots.
            SaveManager.Data.crystals = 100000;
            SaveManager.Data.highestUnlockedLevelIndex = 13;
            int hawk3 = ShipCatalog.IndexOf(0, 2);
            for (int t = 1; t <= 2; t++) Assert.IsTrue(SaveManager.TryUnlockShip(ShipCatalog.IndexOf(0, t)));
            SaveManager.SelectShip(hawk3);

            yield return LoadLevel(LevelDatabase_First());
            PlayerShip ship = UnityEngine.Object.FindAnyObjectByType<PlayerShip>();
            Assert.AreEqual(hawk3, ship.Spec.index);
            Assert.AreEqual(2, ship.Spec.volley);

            foreach (Bullet old in UnityEngine.Object.FindObjectsByType<Bullet>()) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            // The ship shoots by itself; the first volley after the intro puts two player bullets up together.
            float until = Time.realtimeSinceStartup + 6f;
            int playerBullets = 0;
            while (Time.realtimeSinceStartup < until && playerBullets < 2)
            {
                playerBullets = 0;
                foreach (Bullet b in UnityEngine.Object.FindObjectsByType<Bullet>())
                    if (b.owner == BulletOwner.Player) playerBullets++;
                yield return null;
            }
            Assert.GreaterOrEqual(playerBullets, 2, "A twin-cannon ship must put two bullets up per shot.");
        }

        private static LevelData LevelDatabase_First()
        {
            LevelDatabase db = Resources.Load<LevelDatabase>("Data/LevelDatabase");
            return db.GetByIndex(0);
        }

        private class FakeRecovery : SpaceHawk.Online.IRecoveryBackend
        {
            public readonly System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();
            public SpaceHawk.Online.RecoveryResult Next = SpaceHawk.Online.RecoveryResult.Ok;
            public SpaceHawk.Online.RecoveryResult? NextVerify;
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> SendVerification(string c) { Calls.Add($"send {c}"); return System.Threading.Tasks.Task.FromResult(Next); }
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> VerifyContact(string c, string code) { Calls.Add($"verify {c} {code}"); return System.Threading.Tasks.Task.FromResult(NextVerify ?? Next); }
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> SetContact(string u, string c) { Calls.Add("set"); return System.Threading.Tasks.Task.FromResult(Next); }
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> RequestReset(string u, string c) { Calls.Add($"request {u} {c}"); return System.Threading.Tasks.Task.FromResult(Next); }
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> ConfirmReset(string u, string c, string code, string pw) { Calls.Add($"confirm {u} {c} {code} {pw}"); return System.Threading.Tasks.Task.FromResult(Next); }
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> DeleteAccountData() { Calls.Add("delete"); return System.Threading.Tasks.Task.FromResult(Next); }
        }

        private static ForgotPasswordPanel OpenForgotPassword()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/ForgotPasswordPanel");
            Assert.IsNotNull(prefab, "ForgotPasswordPanel prefab missing");
            return UnityEngine.Object.Instantiate(prefab, NewOverlayRoot()).GetComponent<ForgotPasswordPanel>();
        }

        [UnityTest]
        public IEnumerator ForgotPassword_AskForACode_ThenSetANewPassword()
        {
            FakeRecovery fake = new FakeRecovery();
            SpaceHawk.Online.IRecoveryBackend real = SpaceHawk.Online.AccountManager.Recovery;
            SpaceHawk.Online.AccountManager.Recovery = fake;
            try
            {
                ForgotPasswordPanel panel = OpenForgotPassword();
                yield return null;
                Assert.IsTrue(panel.step1Root.activeSelf);
                Assert.IsFalse(panel.step2Root.activeSelf);

                // Nothing typed / a contact that is not an e-mail address: refused here, server never asked.
                panel.usernameInput.text = "";
                panel.contactInput.text = "";
                panel.actionButton.onClick.Invoke();
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf);
                panel.usernameInput.text = "pilot_hawk";
                panel.contactInput.text = "not a contact";
                panel.actionButton.onClick.Invoke();
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf);
                Assert.AreEqual(0, fake.Calls.Count);

                // A real contact: the code is requested and the second step opens.
                panel.contactInput.text = "Pilot@Example.com";
                panel.actionButton.onClick.Invoke();
                yield return null;
                Assert.AreEqual(new[] { "request pilot_hawk pilot@example.com" }, fake.Calls.ToArray());
                Assert.IsFalse(panel.step1Root.activeSelf);
                Assert.IsTrue(panel.step2Root.activeSelf);
                Assert.IsFalse(panel.resendButton.interactable, "A new code can only be asked for after a wait.");

                // Step 2: a mismatching or weak password never reaches the server.
                panel.codeInput.text = "123456";
                panel.newPasswordInput.text = "Brand-new1!";
                panel.confirmPasswordInput.text = "Different1!";
                panel.actionButton.onClick.Invoke();
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf);
                panel.newPasswordInput.text = "weakpass";
                panel.confirmPasswordInput.text = "weakpass";
                panel.actionButton.onClick.Invoke();
                Assert.AreEqual(1, fake.Calls.Count);

                // A wrong code: the server says so, the panel stays on step 2 with the message.
                panel.newPasswordInput.text = "Brand-new1!";
                panel.confirmPasswordInput.text = "Brand-new1!";
                fake.Next = SpaceHawk.Online.RecoveryResult.Fail("invalid_code");
                panel.actionButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(panel.step2Root.activeSelf);
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf);

                // The right code: the password is reset and the panel closes.
                fake.Next = SpaceHawk.Online.RecoveryResult.Ok;
                panel.actionButton.onClick.Invoke();
                yield return null;
                yield return null;
                Assert.AreEqual("confirm pilot_hawk pilot@example.com 123456 Brand-new1!", fake.Calls[fake.Calls.Count - 1]);
                Assert.IsTrue(panel == null, "The panel closes once the password is reset.");
                SpaceHawk.Online.AccountManager.ForgetUsername("pilot_hawk");
            }
            finally
            {
                SpaceHawk.Online.AccountManager.Recovery = real;
            }
        }

        [UnityTest]
        public IEnumerator ForgotPassword_ASpentCode_SendsThePlayerBackToAskForANewOne()
        {
            FakeRecovery fake = new FakeRecovery();
            SpaceHawk.Online.IRecoveryBackend real = SpaceHawk.Online.AccountManager.Recovery;
            SpaceHawk.Online.AccountManager.Recovery = fake;
            try
            {
                ForgotPasswordPanel panel = OpenForgotPassword();
                yield return null;
                panel.usernameInput.text = "pilot_hawk";
                panel.contactInput.text = "pilot@example.com";
                panel.actionButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(panel.step2Root.activeSelf);

                panel.codeInput.text = "000000";
                panel.newPasswordInput.text = "Brand-new1!";
                panel.confirmPasswordInput.text = "Brand-new1!";
                fake.Next = SpaceHawk.Online.RecoveryResult.Fail("too_many_attempts");
                panel.actionButton.onClick.Invoke();
                yield return null;

                Assert.IsTrue(panel.step1Root.activeSelf, "After too many wrong tries a new code has to be asked for.");
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf);
                UnityEngine.Object.Destroy(panel.gameObject);
            }
            finally
            {
                SpaceHawk.Online.AccountManager.Recovery = real;
            }
        }

        [UnityTest]
        public IEnumerator VerifyContact_TheCodeProvesTheContact_AndOnlyThenTheCallbackRuns()
        {
            FakeRecovery fake = new FakeRecovery();
            SpaceHawk.Online.IRecoveryBackend real = SpaceHawk.Online.AccountManager.Recovery;
            SpaceHawk.Online.AccountManager.Recovery = fake;
            try
            {
                int verified = 0;
                VerifyContactPanel panel = VerifyContactPanel.Show(NewOverlayRoot(), "Pilot@Example.com", () => verified++);
                yield return null;
                Assert.AreEqual(new[] { "send pilot@example.com" }, fake.Calls.ToArray(), "Opening the panel sends the code.");
                Assert.IsTrue(panel.statusLabel.gameObject.activeSelf);
                StringAssert.Contains("pi***@example.com", panel.descriptionLabel.text);
                Assert.IsFalse(panel.resendButton.interactable, "A new code can only be asked for after a wait.");

                // Nothing typed: refused here. A wrong code: the server says so, the panel stays, nothing is verified.
                panel.verifyButton.onClick.Invoke();
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf);
                Assert.AreEqual(1, fake.Calls.Count);
                panel.codeInput.text = "000000";
                fake.NextVerify = SpaceHawk.Online.RecoveryResult.Fail("invalid_code");
                panel.verifyButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(panel != null && panel.errorLabel.gameObject.activeSelf);
                Assert.AreEqual(0, verified);

                // The right code: the panel closes and only now the callback runs.
                fake.NextVerify = SpaceHawk.Online.RecoveryResult.Ok;
                panel.codeInput.text = " 123456 ";
                panel.verifyButton.onClick.Invoke();
                yield return null;
                yield return null;
                Assert.AreEqual("verify pilot@example.com 123456", fake.Calls[fake.Calls.Count - 1]);
                Assert.AreEqual(1, verified);
                Assert.IsTrue(panel == null, "The panel closes once the contact is verified.");
            }
            finally
            {
                SpaceHawk.Online.AccountManager.Recovery = real;
            }
        }

        [UnityTest]
        public IEnumerator VerifyContact_ASpentCode_CanBeReplacedAtOnce()
        {
            FakeRecovery fake = new FakeRecovery();
            SpaceHawk.Online.IRecoveryBackend real = SpaceHawk.Online.AccountManager.Recovery;
            SpaceHawk.Online.AccountManager.Recovery = fake;
            try
            {
                VerifyContactPanel panel = VerifyContactPanel.Show(NewOverlayRoot(), "pilot@example.com", null);
                yield return null;
                Assert.IsFalse(panel.resendButton.interactable);

                panel.codeInput.text = "000000";
                fake.NextVerify = SpaceHawk.Online.RecoveryResult.Fail("too_many_attempts");
                panel.verifyButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(panel.resendButton.interactable, "After too many wrong tries a new code can be asked for right away.");
                UnityEngine.Object.Destroy(panel.gameObject);
            }
            finally
            {
                SpaceHawk.Online.AccountManager.Recovery = real;
            }
        }

        [UnityTest]
        public IEnumerator Register_AsksToVerifyTheContact_BeforeAnythingIsCreated()
        {
            FakeRecovery fake = new FakeRecovery();
            SpaceHawk.Online.IRecoveryBackend real = SpaceHawk.Online.AccountManager.Recovery;
            SpaceHawk.Online.AccountManager.Recovery = fake;
            try
            {
                Transform root = NewOverlayRoot();
                GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/RegisterPanel");
                RegisterPanel panel = UnityEngine.Object.Instantiate(prefab, root).GetComponent<RegisterPanel>();
                yield return null;

                panel.usernameInput.text = "pilot_hawk";
                panel.passwordInput.text = "Brand-new1!";
                panel.confirmPasswordInput.text = "Brand-new1!";
                panel.contactInput.text = "not a contact";
                panel.confirmButton.onClick.Invoke();
                Assert.IsTrue(panel.errorLabel.gameObject.activeSelf, "A bad contact is refused on the spot.");
                Assert.AreEqual(0, fake.Calls.Count);
                Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<VerifyContactPanel>());

                panel.contactInput.text = "Pilot@Example.com";
                panel.confirmButton.onClick.Invoke();
                yield return null;
                VerifyContactPanel verify = UnityEngine.Object.FindAnyObjectByType<VerifyContactPanel>();
                Assert.IsNotNull(verify, "A valid contact opens the verification step.");
                Assert.AreEqual(new[] { "send pilot@example.com" }, fake.Calls.ToArray());

                // Backing out of the verification verifies nothing and creates nothing.
                verify.closeButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(panel.confirmButton.interactable);
                Assert.AreEqual(1, fake.Calls.Count);
                UnityEngine.Object.Destroy(panel.gameObject);
            }
            finally
            {
                SpaceHawk.Online.AccountManager.Recovery = real;
            }
        }

        // ------------------------------------------------------------------ notices that always fit

        private static string LongestText(string prefix, Language language)
        {
            string longest = "";
            foreach (string key in Localization.Keys)
            {
                if (!key.StartsWith(prefix)) continue;
                Language previous = Localization.Current;
                Localization.SetLanguage(language);
                string text = Localization.Get(key);
                Localization.SetLanguage(previous);
                if (text.Length > longest.Length) longest = text;
            }
            return longest;
        }

        [UnityTest]
        public IEnumerator Toast_FrameGrowsWithTheText_AndNothingSpillsOut()
        {
            Transform root = NewOverlayRoot();
            ((RectTransform)root).sizeDelta = new Vector2(1920, 1080);
            string longest = LongestText("account.forgot", Language.Vietnamese) + " " + LongestText("account.error", Language.Vietnamese);

            ToastUI.ShowToast(root, "OK");
            ToastUI.ShowToast(root, longest);
            yield return null;

            ToastUI[] toasts = root.GetComponentsInChildren<ToastUI>();
            Assert.AreEqual(2, toasts.Length);
            ToastUI small = System.Array.Find(toasts, t => t.Message == "OK");
            ToastUI big = System.Array.Find(toasts, t => t.Message == longest);
            RectTransform smallRect = (RectTransform)small.transform, bigRect = (RectTransform)big.transform;

            Assert.Greater(bigRect.rect.height, smallRect.rect.height, "A long message gets a taller frame.");
            Assert.LessOrEqual(bigRect.rect.width, 760f + 0.01f);
            foreach (ToastUI toast in toasts)
            {
                toast.label.ForceMeshUpdate();
                RectTransform frame = (RectTransform)toast.transform;
                Assert.LessOrEqual(toast.label.textBounds.size.y, frame.rect.height - 2f, $"'{toast.Message}' does not fit its frame.");
                Assert.LessOrEqual(toast.label.textBounds.size.x, frame.rect.width, $"'{toast.Message}' is wider than its frame.");
            }
        }

        [UnityTest]
        public IEnumerator Toasts_AtTheSameTime_StackInsteadOfOverlapping_AndRepeatsAreMerged()
        {
            Transform root = NewOverlayRoot();
            ((RectTransform)root).sizeDelta = new Vector2(1920, 1080);

            ToastUI.ShowToast(root, "Saving your progress...");
            ToastUI.ShowToast(root, "Logged out - back to your guest progress.");
            ToastUI.ShowToast(root, "Account created!");
            ToastUI.ShowToast(root, "Account created!");   // the same notice again
            yield return null;

            ToastUI[] toasts = root.GetComponentsInChildren<ToastUI>();
            Assert.AreEqual(3, toasts.Length, "The repeated notice is not shown twice.");

            for (int i = 0; i < toasts.Length; i++)
            {
                for (int j = i + 1; j < toasts.Length; j++)
                {
                    Rect a = RectIn(root, (RectTransform)toasts[i].transform);
                    Rect b = RectIn(root, (RectTransform)toasts[j].transform);
                    Assert.IsFalse(a.Overlaps(b), $"'{toasts[i].Message}' overlaps '{toasts[j].Message}'.");
                }
            }
        }

        private static Rect RectIn(Transform root, RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 min = root.InverseTransformPoint(corners[0]);
            Vector3 max = root.InverseTransformPoint(corners[2]);
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        // Every explanation / error / status text of the account screens, in the longest wording of each
        // language, must fit its own box without being cut off.
        [UnityTest]
        public IEnumerator AccountScreens_MessagesFitTheirBoxes_InBothLanguages()
        {
            string[] screens = { "LoginPanel", "RegisterPanel", "ChangePasswordPanel", "ForgotPasswordPanel", "RecoveryContactPanel", "VerifyContactPanel", "PlayerProfilePanel", "ConfirmDialog" };
            string[] messageNames = { "ErrorLabel", "StatusLabel" };
            System.Collections.Generic.List<string> problems = new System.Collections.Generic.List<string>();

            foreach (Language language in new[] { Language.English, Language.Vietnamese })
            {
                Localization.SetLanguage(language);
                string longestError = LongestText("account.error", language);
                string longestStatus = LongestText("account.verify_sent", language) + " " + Localization.Get("account.forgot_sent");

                foreach (string screen in screens)
                {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + screen);
                    GameObject instance = UnityEngine.Object.Instantiate(prefab, NewOverlayRoot());
                    yield return null;

                    foreach (TMPro.TMP_Text label in instance.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    {
                        bool isMessage = System.Array.IndexOf(messageNames, label.name) >= 0;
                        bool isNote = label.name == "Description" || label.name.EndsWith("Hint") || label.name == "Message" || label.name == "AccountStatus";
                        if (!isMessage && !isNote) continue;

                        string original = label.text;
                        if (label.name == "ErrorLabel") label.text = longestError;
                        else if (label.name == "StatusLabel") label.text = longestStatus;
                        else if (label.name == "Message") label.text = Localization.Get("account.delete_confirm_desc");
                        else if (label.name == "AccountStatus") label.text = Localization.Format("account.status_linked_fmt", "twenty_char_username");
                        label.gameObject.SetActive(true);
                        label.ForceMeshUpdate();
                        if (label.isTextTruncated || label.textBounds.size.y > label.rectTransform.rect.height + 1f)
                            problems.Add($"{language} {screen}/{label.name}: text does not fit ('{label.text}')");
                        label.text = original;
                    }
                    UnityEngine.Object.Destroy(instance);
                    yield return null;
                }
            }
            Localization.SetLanguage(Language.English);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [UnityTest]
        public IEnumerator BeamImpactFx_ThrowsSparks_ThenCleansItselfUp()
        {
            BeamImpactFX fx = BeamImpactFX.Create();
            for (int i = 0; i < 20; i++) fx.Splash(Vector2.zero, Vector2.down, 0.016f);
            fx.Pulse(Vector2.zero);
            Assert.Greater(fx.transform.childCount, 10, "A strike should throw a visible spray of particles.");
            Assert.LessOrEqual(fx.transform.childCount, 200, "The pool must stay bounded.");

            fx.Retire();
            fx.Splash(Vector2.zero, Vector2.down, 0.016f);
            float until = Time.realtimeSinceStartup + 3f;
            while (fx != null && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(fx == null, "Once the beam is gone and the last spark has faded, the effect object must remove itself.");
        }
    }
}
