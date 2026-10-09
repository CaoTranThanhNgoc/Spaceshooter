using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.Online;
using SpaceHawk.UI;

namespace SpaceHawk.Tests.PlayMode
{
    /// <summary>The display-name flow of the real Profile screen, the first-run gate on the real menu, and the screens
    /// that must follow the profile when another account (or the guest) takes over.</summary>
    public class NameAndProfilePlayModeTests
    {
        private class FakeNames : INameBackend
        {
            public Func<string, bool, NameResult> Answer = (name, auto) => new NameResult { ok = true, name = name };
            public Task<NameResult> SetName(string name, bool auto) => Task.FromResult(Answer(name, auto));
            public Task<NameResult> GetName() => Task.FromResult(new NameResult { ok = true });
        }

        private INameBackend _realBackend;
        private FakeNames _names;

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            Localization.SetLanguage(Language.English);
            Time.timeScale = 1f;
            _realBackend = NameService.Backend;
            _names = new FakeNames();
            NameService.Backend = _names;
            LeaderboardManager.SimulateSignedInForTests("player-test");
        }

        [TearDown]
        public void TearDown()
        {
            NameService.Backend = _realBackend;
            LeaderboardManager.SimulateOfflineForTests(false);
            LeaderboardManager.SimulateSignedInForTests(null);
            Localization.SetLanguage(Language.English);
        }

        private static Transform NewOverlayRoot()
        {
            GameObject canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            return canvasGo.transform;
        }

        private static T Open<T>(string prefabName, Transform root) where T : Component
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + prefabName);
            Assert.IsNotNull(prefab, prefabName + " prefab missing.");
            return UnityEngine.Object.Instantiate(prefab, root).GetComponent<T>();
        }

        private static bool HasToast(Transform root, string message)
        {
            foreach (ToastUI toast in root.GetComponentsInChildren<ToastUI>())
                if (toast.Message == message) return true;
            return false;
        }

        // ---------------------------------------------------------------- the profile screen

        [UnityTest]
        public IEnumerator Profile_ShowsTheNameTheGameGave_FromTheVeryFirstVisit()
        {
            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;

            StringAssert.IsMatch("^Pilot[a-z0-9]{4}$", panel.nameInput.text, "a guest already has a name, shown as the Leaderboard shows it");
            Assert.AreEqual(SaveManager.GetPlayerName(), panel.nameInput.text);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_ATakenName_IsRefused_WithANotice_AndTheOldNameStays()
        {
            SaveManager.SetPlayerName("OldName", false);
            _names.Answer = (name, auto) => NameResult.Fail("name_taken");
            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;

            panel.nameInput.text = "Taken_One";
            panel.confirmButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(HasToast(root, Localization.Get("profile.name_taken")), "the player is told the name is taken");
            Assert.IsNotNull(panel, "the panel stays open to try another name");
            Assert.IsTrue(panel.confirmButton.interactable);
            Assert.AreEqual("OldName", SaveManager.GetPlayerName());
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_ANameThatIsNotValid_IsRefusedWithoutAskingTheServer()
        {
            int asked = 0;
            _names.Answer = (name, auto) => { asked++; return new NameResult { ok = true, name = name }; };
            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;

            panel.nameInput.text = "two words";
            panel.confirmButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, asked);
            Assert.IsTrue(HasToast(root, Localization.Get("profile.name_invalid")));
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_TooSoonAfterTheLastChange_SaysWhenItWorksAgain_AndKeepsSayingIt()
        {
            SaveManager.SetPlayerName("OldName", false);
            _names.Answer = (name, auto) => NameResult.Fail("name_cooldown", 2 * 86400 + 3 * 3600);
            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;
            Assert.AreEqual(Localization.Get("leaderboard.choose_name_desc"), panel.nameHintLabel.text, "unlocked: the line says what the name is for");

            panel.nameInput.text = "NewName";
            panel.confirmButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(HasToast(root, Localization.Format("profile.name_cooldown_fmt", "2 days 3 h")));
            StringAssert.Contains("2 days 3 h", panel.nameHintLabel.text, "the line above the field now counts down to the next change");
            Assert.IsFalse(panel.nameInput.interactable, "the field is switched off until the wait is over");
            Assert.IsFalse(panel.confirmButton.interactable, "...and so is the button");
            Assert.AreEqual("OldName", SaveManager.GetPlayerName());

            // Opening the screen later still shows the lock.
            PlayerProfilePanel again = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;
            StringAssert.Contains("2 days", again.nameHintLabel.text);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_ASignedInPlayer_IsShownUnderTheAccountName()
        {
            SaveManager.EnsureDefaultName();
            SaveManager.SetAccountLinked(true, "thanhne");
            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;

            Assert.AreEqual("thanhne", panel.nameInput.text, "the account name, not a name the game made up");
            Assert.AreEqual(SaveManager.GetPlayerName(), panel.nameInput.text);
            Assert.IsTrue(panel.nameInput.interactable, "a name nobody changed yet can be changed");
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_WhileTheWeeklyLockIsOn_NothingCanBeChanged_UntilItEnds()
        {
            int asked = 0;
            _names.Answer = (name, auto) => { asked++; return new NameResult { ok = true, name = name }; };
            SaveManager.EnsureDefaultName();
            SaveManager.SetNameChangeUnlock(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2);

            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;
            Assert.IsFalse(panel.nameInput.interactable);
            Assert.IsFalse(panel.confirmButton.interactable);
            StringAssert.Contains(Localization.Format("profile.name_locked_fmt", "1 min"), panel.nameHintLabel.text);

            panel.nameInput.text = "SomethingNew";
            panel.confirmButton.onClick.Invoke();     // even if it were pressed anyway
            yield return null;
            Assert.AreEqual(0, asked, "a locked name never reaches the server");

            yield return new WaitForSecondsRealtime(3.6f);   // the wait ends; the panel notices by itself
            Assert.IsTrue(panel.nameInput.interactable, "the field is back on");
            Assert.IsTrue(panel.confirmButton.interactable, "...and the button");
            Assert.AreEqual(Localization.Get("leaderboard.choose_name_desc"), panel.nameHintLabel.text);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_AnAcceptedName_IsSaved_TheScreenClosesAndASavedNoticeStays()
        {
            Transform root = NewOverlayRoot();
            PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
            yield return null;

            panel.nameInput.text = "Ngọc";
            panel.confirmButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.AreEqual("Ngọc", SaveManager.GetPlayerName());
            Assert.IsFalse(SaveManager.IsPlayerNameAuto());
            Assert.IsTrue(panel == null, "the screen closes after a saved name");
            Assert.IsTrue(HasToast(root, Localization.Get("profile.name_saved")));
            UnityEngine.Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator Profile_TheLockLine_FitsItsBox_InBothLanguages()
        {
            long longestWait = 6 * 86400 + 23 * 3600 + 59 * 60;
            SaveManager.EnsureDefaultName();    // a profile always has a name before it can have a lock on it
            SaveManager.SetNameChangeUnlock(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + longestWait);
            foreach (Language language in new[] { Language.English, Language.Vietnamese })
            {
                Localization.SetLanguage(language);
                Transform root = NewOverlayRoot();
                PlayerProfilePanel panel = Open<PlayerProfilePanel>("PlayerProfilePanel", root);
                yield return null;

                TMPro.TMP_Text hint = panel.nameHintLabel;
                StringAssert.Contains(PlayerNameRules.FormatWait(longestWait), hint.text);
                hint.ForceMeshUpdate();
                Assert.IsFalse(hint.isTextTruncated, language + ": the lock line is cut off");
                Assert.LessOrEqual(hint.textBounds.size.y, hint.rectTransform.rect.height + 1f, language + ": the lock line does not fit its box");
                UnityEngine.Object.Destroy(root.gameObject);
                yield return null;
            }
        }

        // ---------------------------------------------------------------- the screens follow the profile

        [UnityTest]
        public IEnumerator Achievements_FollowTheProfile_WhenTheGuestTakesOverFromAnAccount()
        {
            SaveManager.SetAccountLinked(true, "tester_one");
            SaveManager.Data.enemiesDestroyed = 5000;
            SaveManager.Data.highestUnlockedLevelIndex = 9;
            SaveManager.Data.totalCrystalsEarned = 4000;
            SaveManager.Data.endlessBestWave = 25;
            SaveManager.Data.shipLevel = 4;

            Transform root = NewOverlayRoot();
            AchievementsPanel panel = Open<AchievementsPanel>("AchievementsPanel", root);
            yield return null;
            string[] before = ProgressTexts(panel);
            AssertRowsMatchTheProfile(panel);
            Assert.IsTrue(Array.Exists(before, t => !t.StartsWith("0 /")), "the account has progress on show");

            SaveManager.RestoreGuestProfile();     // logged out: a fresh guest
            yield return null;

            string[] after = ProgressTexts(panel);
            AssertRowsMatchTheProfile(panel);
            for (int i = 0; i < Mathf.Min(after.Length, panel.database.achievements.Length); i++)
            {
                // Everything but the ship level (a fresh ship is level 1) starts from zero.
                if (panel.database.achievements[i].kind == AchievementKind.ShipLevel) continue;
                StringAssert.StartsWith("0 /", after[i], "no progress of the account may show in guest play (row " + i + ")");
            }
            CollectionAssert.AreNotEqual(before, after);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        private static string[] ProgressTexts(AchievementsPanel panel)
        {
            string[] texts = new string[panel.rows.Length];
            for (int i = 0; i < texts.Length; i++) texts[i] = panel.rows[i].progressLabel.text;
            return texts;
        }

        private static void AssertRowsMatchTheProfile(AchievementsPanel panel)
        {
            int count = Mathf.Min(panel.rows.Length, panel.database.achievements.Length);
            for (int i = 0; i < count; i++)
            {
                AchievementData data = panel.database.achievements[i];
                string expected = $"{Mathf.Min(AchievementTracker.GetProgress(data), data.targetCount)} / {data.targetCount}";
                Assert.AreEqual(expected, panel.rows[i].progressLabel.text, "achievement row " + i);
            }
        }

        [UnityTest]
        public IEnumerator Inventory_FollowsTheProfile_TheShipOfTheAccountIsNotWornByTheGuest()
        {
            SaveManager.SetAccountLinked(true, "tester_one");
            SaveManager.Data.ownedShips[3] = true;
            SaveManager.Data.selectedShip = 3;

            Transform root = NewOverlayRoot();
            InventoryPanel panel = Open<InventoryPanel>("InventoryPanel", root);
            yield return null;
            Assert.AreEqual(3, panel.PreviewIndex, "the account wears ship 3");

            SaveManager.RestoreGuestProfile();
            yield return null;

            Assert.AreEqual(SaveManager.GetSelectedShip(), panel.PreviewIndex);
            Assert.AreEqual(0, panel.PreviewIndex, "the fresh guest wears the starter ship");
            UnityEngine.Object.Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- the first-run gate on the real menu

        private static IEnumerator LoadMenu()
        {
            SceneManager.LoadScene(GameManager.MainMenuSceneName);
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Menu_ABrandNewInstall_AsksWhoYouAre()
        {
            yield return LoadMenu();
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<AuthGatePanel>(), "a brand-new install sees the gate");
        }

        [UnityTest]
        public IEnumerator Menu_AfterLoggingOut_TheGateIsNotShownAgain()
        {
            SaveManager.MarkIdentityChosen();
            SaveManager.SetAccountLinked(true, "tester_one");
            SaveManager.RestoreGuestProfile();      // logged out: a fresh guest with a name the game picked
            Assert.IsTrue(SaveManager.IsPlayerNameAuto());

            yield return LoadMenu();    // e.g. back from a lost Endless run
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<AuthGatePanel>(), "no \"who are you\" screen after logging out");
        }

        [UnityTest]
        public IEnumerator Menu_AGuestWhoChoseToContinue_IsNotAskedAgain()
        {
            yield return LoadMenu();
            AuthGatePanel gate = UnityEngine.Object.FindAnyObjectByType<AuthGatePanel>();
            Assert.IsNotNull(gate);
            gate.guestButton.onClick.Invoke();
            yield return null;
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<AuthGatePanel>(), "the gate closes - and no naming screen is forced on a guest");
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<PlayerProfilePanel>());

            yield return LoadMenu();
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<AuthGatePanel>());
        }
    }
}
