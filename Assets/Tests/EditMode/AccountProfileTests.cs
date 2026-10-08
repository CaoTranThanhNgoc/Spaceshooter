using NUnit.Framework;
using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.Tests
{
    /// <summary>Guest and account progress are separate profiles: signing in puts the guest progress
    /// aside, logging out brings it back untouched, and the account's progress never leaks into
    /// guest play. (The network side - Authentication / Cloud Save - is not exercised here; these
    /// drive the same SaveManager calls AccountManager makes.)</summary>
    public class AccountProfileTests
    {
        private static readonly string[] TestNames = { "tester_one", "tester_two", "tester_three", "tester_four" };

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string name in TestNames) AccountManager.ForgetUsername(name);
        }

        /// <summary>What a cloud snapshot of an account looks like.</summary>
        private static string AccountSnapshot(int crystals, int levelsCleared)
        {
            SaveData data = new SaveData { crystals = crystals, highestUnlockedLevelIndex = levelsCleared, accountLinked = true, accountUsername = "tester_one" };
            data.levelBestScores.Add(new LevelScoreEntry { levelId = "level_01", score = 9999 });
            return JsonUtility.ToJson(data);
        }

        private static void SignInTo(string username, string snapshot)
        {
            SaveManager.StashGuestProfile();
            SaveManager.SetAccountLinked(true, username);
            SaveManager.ApplyCloudData(snapshot);
        }

        [Test]
        public void LoggingOut_BringsTheGuestProgressBack()
        {
            SaveManager.Data.crystals = 777;
            SaveManager.Data.highestUnlockedLevelIndex = 2;
            SaveManager.Data.playerName = "GuestPilot";

            SignInTo("tester_one", AccountSnapshot(9000, 9));
            Assert.AreEqual(9000, SaveManager.GetCrystals(), "Signed in: the account's progress is on screen.");
            Assert.AreEqual(9, SaveManager.GetLevelsCleared());
            Assert.Greater(SaveManager.GetSkillRating(), 0);
            Assert.IsTrue(SaveManager.HasGuestStash);

            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(777, SaveManager.GetCrystals(), "Logged out: the guest progress is back...");
            Assert.AreEqual(2, SaveManager.GetLevelsCleared());
            Assert.AreEqual("GuestPilot", SaveManager.GetPlayerName());
            Assert.AreEqual(0, SaveManager.GetSkillRating(), "...and the account's scores are gone from guest play.");
            Assert.IsFalse(SaveManager.IsAccountLinked());
            Assert.AreEqual("", SaveManager.GetAccountUsername());
            Assert.IsFalse(SaveManager.HasGuestStash, "The stash is used up.");
        }

        [Test]
        public void LoggingOut_WithNoGuestProgress_GivesAFreshGuest()
        {
            // An account on a device that never had guest progress (or a save from before the split).
            SaveManager.SetAccountLinked(true, "tester_one");
            SaveManager.Data.crystals = 9000;
            SaveManager.Data.highestUnlockedLevelIndex = 9;

            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(new SaveData().crystals, SaveManager.GetCrystals());
            Assert.AreEqual(0, SaveManager.GetLevelsCleared());
            Assert.IsFalse(SaveManager.IsAccountLinked());
        }

        [Test]
        public void EachSignIn_PutsTheGuestProgressAsideAgain()
        {
            SaveManager.Data.crystals = 300;
            SignInTo("tester_one", AccountSnapshot(5000, 6));
            SaveManager.RestoreGuestProfile();

            SaveManager.Data.crystals += 500; // played on as a guest
            SignInTo("tester_two", AccountSnapshot(1200, 3));
            Assert.AreEqual(1200, SaveManager.GetCrystals());
            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(800, SaveManager.GetCrystals(), "The guest progress made in between is what comes back.");
        }

        [Test]
        public void TheGuestProfile_IsNotOverwritten_ByStashingWhileSignedIn()
        {
            SaveManager.Data.crystals = 400;
            SignInTo("tester_one", AccountSnapshot(9000, 9));

            // A second stash while the account is on screen would store the ACCOUNT as the guest.
            SaveManager.StashGuestProfile();
            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(400, SaveManager.GetCrystals());
        }

        [Test]
        public void VolumeAndLanguage_StayWithTheDevice_AcrossProfileSwaps()
        {
            SaveManager.Data.language = 1;
            SaveManager.Data.masterVolume = 0.3f;
            SaveManager.Data.sfxEnabled = false;
            SaveManager.Data.fullscreen = false;

            // The snapshot carries other settings (made on another device) - they must not win.
            SaveData cloud = new SaveData { language = 0, masterVolume = 1f, sfxEnabled = true, fullscreen = true, accountLinked = true };
            SignInTo("tester_one", JsonUtility.ToJson(cloud));
            Assert.AreEqual(1, SaveManager.Data.language);
            Assert.AreEqual(0.3f, SaveManager.Data.masterVolume, 0.0001f);
            Assert.IsFalse(SaveManager.Data.sfxEnabled);
            Assert.IsFalse(SaveManager.Data.fullscreen);

            SaveManager.Data.language = 0; // changed while signed in
            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(0, SaveManager.Data.language, "Settings follow the device, whichever profile is loaded.");
        }

        [Test]
        public void SwappingProfiles_RaisesTheEventsOpenScreensListenTo()
        {
            int profileChanged = 0, crystalsChanged = 0;
            void OnProfile() => profileChanged++;
            void OnCrystals(int _) => crystalsChanged++;
            SaveManager.ProfileChanged += OnProfile;
            SaveManager.CrystalsChanged += OnCrystals;
            try
            {
                SignInTo("tester_one", AccountSnapshot(9000, 9));
                SaveManager.RestoreGuestProfile();
                Assert.AreEqual(2, profileChanged, "Once for the sign-in snapshot, once for the log out.");
                Assert.AreEqual(2, crystalsChanged);
            }
            finally
            {
                SaveManager.ProfileChanged -= OnProfile;
                SaveManager.CrystalsChanged -= OnCrystals;
            }
        }

        [Test]
        public void RecentUsernames_AreRememberedNewestFirst_WithoutDuplicates_AndCapped()
        {
            AccountManager.RememberUsername("tester_one");
            AccountManager.RememberUsername("tester_two");
            AccountManager.RememberUsername("tester_three");
            AccountManager.RememberUsername("TESTER_ONE");   // same account again: moves to the front

            string[] recent = AccountManager.GetRecentUsernames();
            Assert.AreEqual("TESTER_ONE", recent[0]);
            Assert.AreEqual(3, recent.Length);

            AccountManager.RememberUsername("tester_four");
            recent = AccountManager.GetRecentUsernames();
            Assert.AreEqual(3, recent.Length, "Only the last three are kept.");
            Assert.AreEqual("tester_four", recent[0]);

            AccountManager.ForgetUsername("tester_four");
            Assert.AreNotEqual("tester_four", AccountManager.GetRecentUsernames().Length > 0 ? AccountManager.GetRecentUsernames()[0] : "");
        }

        [Test]
        public void InputRules_MatchTheService()
        {
            Assert.IsTrue(AccountManager.IsUsernameValid("good_name-1.x"));
            Assert.IsFalse(AccountManager.IsUsernameValid("ab"));
            Assert.IsFalse(AccountManager.IsUsernameValid("has space"));
            Assert.IsFalse(AccountManager.IsUsernameValid(new string('a', 21)));

            Assert.IsTrue(AccountManager.IsPasswordValid("Abcdefg1!"));
            Assert.IsFalse(AccountManager.IsPasswordValid("Abcdefg1"), "needs a symbol");
            Assert.IsFalse(AccountManager.IsPasswordValid("abcdefg1!"), "needs an upper-case letter");
            Assert.IsFalse(AccountManager.IsPasswordValid("ABCDEFG1!"), "needs a lower-case letter");
            Assert.IsFalse(AccountManager.IsPasswordValid("Abcdefgh!"), "needs a digit");
            Assert.IsFalse(AccountManager.IsPasswordValid("Ab1!"), "too short");
            Assert.IsFalse(AccountManager.IsPasswordValid("Abcdefg1!" + new string('x', 25)), "too long");
        }
    }
}
