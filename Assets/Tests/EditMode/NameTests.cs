using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.Tests
{
    /// <summary>What a display name may look like and when two names are the same.</summary>
    public class PlayerNameRulesTests
    {
        [TestCase("Pilot_One", "Pilot_One")]
        [TestCase("  Hawk.99 ", "Hawk.99")]
        [TestCase("Ngọc", "Ngọc")]
        [TestCase("Đạt-Pro", "Đạt-Pro")]
        [TestCase("abc", "abc")]
        [TestCase("SixteenCharsName", "SixteenCharsName")]
        public void ValidNames_AreAccepted_AndTrimmed(string typed, string expected)
        {
            Assert.AreEqual(expected, PlayerNameRules.Clean(typed));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("ab")]
        [TestCase("SeventeenChars123")]
        [TestCase("two words")]
        [TestCase("no@symbols")]
        [TestCase("<b>bold</b>")]
        [TestCase("...")]
        [TestCase("a.-")]
        public void InvalidNames_AreRefused(string typed)
        {
            Assert.IsNull(PlayerNameRules.Clean(typed));
        }

        [Test]
        public void NamesThatDifferOnlyInCaseAccentsOrPunctuation_AreTheSameName()
        {
            Assert.AreEqual("ngoc", PlayerNameRules.Key("Ngọc"));
            Assert.AreEqual("ngoc", PlayerNameRules.Key("N.g-o_c"));
            Assert.AreEqual("datpro", PlayerNameRules.Key("Đạt Pro"), "d with a stroke is a d");
            Assert.IsTrue(PlayerNameRules.SameName("NGOC", "ngoc"));
            Assert.IsTrue(PlayerNameRules.SameName("Ngọc", "n.g.o.c"));
            Assert.IsFalse(PlayerNameRules.SameName("ngoc", "ngoc2"));
            Assert.IsFalse(PlayerNameRules.SameName("", ""));
        }

        [Test]
        public void TheGamesOwnNames_AreValidNames()
        {
            for (int i = 0; i < 50; i++)
            {
                string name = PlayerNameRules.RandomDefaultName();
                StringAssert.IsMatch("^Pilot[a-z0-9]{4}$", name);
                Assert.AreEqual(name, PlayerNameRules.Clean(name));
            }
        }

        [Test]
        public void TheWait_IsShownInTheLargestFittingUnits()
        {
            Localization.SetLanguage(Language.English);
            Assert.AreEqual("6 days 4 h", PlayerNameRules.FormatWait(6 * 86400 + 4 * 3600 + 59));
            Assert.AreEqual("5 h 12 min", PlayerNameRules.FormatWait(5 * 3600 + 12 * 60));
            Assert.AreEqual("9 min", PlayerNameRules.FormatWait(9 * 60 + 30));
            Assert.AreEqual("1 min", PlayerNameRules.FormatWait(5), "never shown as zero");
        }
    }

    /// <summary>The display-name bookkeeping of a profile, and who the player is on this device.</summary>
    public class NameStateTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            Localization.SetLanguage(Language.English);
        }

        [Test]
        public void EveryProfileHasAName_TheGameGivesOneUntilThePlayerChooses()
        {
            Assert.AreEqual("", SaveManager.GetPlayerName());
            string name = SaveManager.EnsureDefaultName();
            StringAssert.IsMatch("^Pilot[a-z0-9]{4}$", name);
            Assert.IsTrue(SaveManager.IsPlayerNameAuto());
            Assert.AreEqual(name, SaveManager.EnsureDefaultName(), "asking again changes nothing");
        }

        [Test]
        public void ANameChosenByHand_IsNotAutomatic_AndIsUnknownToTheServerUntilRegistered()
        {
            SaveManager.MarkNameRegistered("player-1", "Pilot7k2q", 0);
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-1"));

            SaveManager.SetPlayerName("Ngọc", false);
            Assert.IsFalse(SaveManager.IsPlayerNameAuto());
            Assert.IsFalse(SaveManager.IsNameRegisteredFor("player-1"), "a new name has to be claimed first");
        }

        [Test]
        public void TheWeeklyLock_CountsDownToZero()
        {
            Assert.AreEqual(0, SaveManager.GetNameChangeWaitSeconds());
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveManager.SetNameChangeUnlock(now + 3600);
            Assert.That(SaveManager.GetNameChangeWaitSeconds(), Is.InRange(3590, 3600));
            SaveManager.SetNameChangeUnlock(now - 5);
            Assert.AreEqual(0, SaveManager.GetNameChangeWaitSeconds());
        }

        [Test]
        public void CreatingAnAccount_MovesTheGuestProgressIn_AndTheGuestAfterLoggingOutStartsFresh()
        {
            SaveManager.Data.crystals = 777;
            SaveManager.Data.highestUnlockedLevelIndex = 3;
            SaveManager.Data.enemiesDestroyed = 120;
            SaveManager.Data.claimedAchievements.Add("kills_100");
            SaveManager.SetPlayerName("GuestPilot", false);

            SaveManager.SetAccountLinked(true, "tester_one");   // what Register does: nothing is stashed
            Assert.IsFalse(SaveManager.HasGuestStash);
            Assert.AreEqual(777, SaveManager.GetCrystals(), "the account has the guest progress");
            Assert.AreEqual(120, SaveManager.GetEnemiesDestroyed());
            Assert.AreEqual("GuestPilot", SaveManager.GetPlayerName(), "...and the name the guest had");

            SaveManager.RestoreGuestProfile();                  // logging out
            Assert.AreEqual(new SaveData().crystals, SaveManager.GetCrystals(), "the guest starts fresh");
            Assert.AreEqual(0, SaveManager.GetLevelsCleared());
            Assert.AreEqual(0, SaveManager.GetEnemiesDestroyed());
            Assert.IsFalse(SaveManager.IsAchievementClaimed("kills_100"), "no achievement of the account shows up in guest play");
            Assert.AreNotEqual("GuestPilot", SaveManager.GetPlayerName(), "the fresh guest has a name of its own");
            StringAssert.IsMatch("^Pilot[a-z0-9]{4}$", SaveManager.GetPlayerName());
            Assert.IsTrue(SaveManager.IsPlayerNameAuto());
        }

        [Test]
        public void SigningInToAnExistingAccount_PutsTheGuestProgressAside_AndLoggingOutBringsItBack()
        {
            SaveManager.Data.crystals = 300;
            SaveManager.SetPlayerName("GuestPilot", false);
            SaveManager.StashGuestProfile();
            SaveManager.SetAccountLinked(true, "tester_one");
            SaveManager.ApplyCloudData(UnityEngine.JsonUtility.ToJson(new SaveData { crystals = 9000, accountLinked = true }));
            Assert.AreEqual(9000, SaveManager.GetCrystals());

            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(300, SaveManager.GetCrystals());
            Assert.AreEqual("GuestPilot", SaveManager.GetPlayerName());
        }

        [Test]
        public void AccountThatHadNoCloudSave_TakesTheGuestProgress_WithNothingLeftBehind()
        {
            SaveManager.Data.crystals = 450;
            SaveManager.StashGuestProfile();
            SaveManager.SetAccountLinked(true, "tester_one");
            SaveManager.DiscardGuestStash();            // what SignIn does when the account has nothing saved yet
            Assert.IsFalse(SaveManager.HasGuestStash);
            Assert.AreEqual(450, SaveManager.GetCrystals());

            SaveManager.RestoreGuestProfile();
            Assert.AreEqual(new SaveData().crystals, SaveManager.GetCrystals(), "the guest does not get a second copy");
        }

        [Test]
        public void TheWhoAreYouGate_IsForABrandNewInstallOnly()
        {
            SaveManager.EnsureDefaultName();
            Assert.IsFalse(SaveManager.HasChosenIdentity(), "a brand-new install: the gate is shown");

            SaveManager.MarkIdentityChosen();           // Continue as Guest
            Assert.IsTrue(SaveManager.HasChosenIdentity());
        }

        [Test]
        public void TheGate_NeverComesBack_AfterLoggingOut()
        {
            SaveManager.EnsureDefaultName();
            SaveManager.SetAccountLinked(true, "tester_one");      // an account was created or signed in to
            SaveManager.RestoreGuestProfile();                     // logged out: a fresh guest with a game-picked name

            Assert.IsTrue(SaveManager.IsPlayerNameAuto());
            Assert.IsFalse(SaveManager.IsAccountLinked());
            Assert.IsTrue(SaveManager.HasChosenIdentity(), "no \"who are you\" screen again");
        }

        [Test]
        public void ANameChosenByHand_CountsAsAnAnsweredGate_ForPlayersFromBeforeTheGateWasRemembered()
        {
            SaveManager.SetPlayerName("OldTimer", false);
            Assert.IsTrue(SaveManager.HasChosenIdentity());
        }

        [Test]
        public void AnAccountLinkedBeforeTheGateWasRemembered_IsRememberedAtNextLaunch()
        {
            SaveManager.Data.accountLinked = true;                 // as loaded from an old save
            SaveManager.Data.accountUsername = "tester_one";
            Assert.IsTrue(SaveManager.HasChosenIdentity());

            SaveManager.RestoreGuestProfile();                     // logging out afterwards
            Assert.IsTrue(SaveManager.HasChosenIdentity());
        }
    }

    /// <summary>NameService against a fake server: the rules of "unique" and "once a week" live on the server (and are
    /// tested there); here the game's side - what it asks, what it keeps, what it tells the player.</summary>
    public class NameServiceTests
    {
        private class FakeNames : INameBackend
        {
            public readonly List<string> Calls = new List<string>();
            public Func<string, bool, NameResult> Answer = (name, auto) => new NameResult { ok = true, name = name, nextChangeAt = auto ? 0 : 1900000000 };

            public Task<NameResult> SetName(string name, bool auto)
            {
                Calls.Add((auto ? "auto " : "set ") + name);
                return Task.FromResult(Answer(name, auto));
            }

            public Task<NameResult> GetName() => Task.FromResult(new NameResult { ok = true });
        }

        private FakeNames _fake;
        private INameBackend _real;

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            Localization.SetLanguage(Language.English);
            _real = NameService.Backend;
            _fake = new FakeNames();
            NameService.Backend = _fake;
            LeaderboardManager.SimulateSignedInForTests("player-A");
        }

        [TearDown]
        public void TearDown()
        {
            NameService.Backend = _real;
            LeaderboardManager.SimulateOfflineForTests(false);
            LeaderboardManager.SimulateSignedInForTests(null);
        }

        private static T Wait<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(5000), "the task did not finish");
            return task.Result;
        }

        private static void Wait(Task task) => Assert.IsTrue(task.Wait(5000), "the task did not finish");

        // ------------------------------------------------------------------ the player changes the name

        [Test]
        public void ANameThatIsNotValid_IsRefusedWithoutAskingTheServer()
        {
            NameService.ChangeResult result = Wait(NameService.ChangeName("two words"));
            Assert.IsFalse(result.ok);
            Assert.AreEqual("invalid_name", result.code);
            Assert.IsEmpty(_fake.Calls);
            StringAssert.Contains("3-16", NameService.ErrorText(result));
        }

        [Test]
        public void ATakenName_IsRefused_AndTheOldNameStays()
        {
            SaveManager.SetPlayerName("OldName", false);
            _fake.Answer = (name, auto) => NameResult.Fail("name_taken");

            NameService.ChangeResult result = Wait(NameService.ChangeName("Ngọc"));
            Assert.IsFalse(result.ok);
            Assert.AreEqual("name_taken", result.code);
            Assert.AreEqual("OldName", SaveManager.GetPlayerName());
            StringAssert.Contains("already taken", NameService.ErrorText(result));
        }

        [Test]
        public void TooSoonAfterTheLastChange_IsRefused_WithWhenItWorksAgain()
        {
            SaveManager.SetPlayerName("OldName", false);
            _fake.Answer = (name, auto) => NameResult.Fail("name_cooldown", 3 * 86400 + 5 * 3600);

            NameService.ChangeResult result = Wait(NameService.ChangeName("NewName"));
            Assert.IsFalse(result.ok);
            Assert.AreEqual("name_cooldown", result.code);
            Assert.AreEqual("OldName", SaveManager.GetPlayerName());
            StringAssert.Contains("3 days 5 h", NameService.ErrorText(result));
            Assert.That(SaveManager.GetNameChangeWaitSeconds(), Is.InRange(3 * 86400 + 5 * 3600 - 10, 3 * 86400 + 5 * 3600));
        }

        [Test]
        public void AnAcceptedName_IsKept_AndRemembersWhenItMayChangeAgain()
        {
            long nextChange = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 7 * 86400;
            _fake.Answer = (name, auto) => new NameResult { ok = true, name = name, nextChangeAt = nextChange };

            NameService.ChangeResult result = Wait(NameService.ChangeName("  Ngọc "));
            Assert.IsTrue(result.ok);
            CollectionAssert.AreEqual(new[] { "set Ngọc" }, _fake.Calls, "trimmed, and not an automatic change");
            Assert.AreEqual("Ngọc", SaveManager.GetPlayerName());
            Assert.IsFalse(SaveManager.IsPlayerNameAuto());
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-A"));
            Assert.That(SaveManager.GetNameChangeWaitSeconds(), Is.InRange(7 * 86400 - 10, 7 * 86400));
        }

        [Test]
        public void TheNameAlreadyHeld_IsNotAChange()
        {
            SaveManager.SetPlayerName("SameName", false);
            NameService.ChangeResult result = Wait(NameService.ChangeName("SameName"));
            Assert.IsTrue(result.ok);
            Assert.IsEmpty(_fake.Calls);
        }

        [Test]
        public void WhenTheServerCannotBeReached_NothingChanges()
        {
            SaveManager.SetPlayerName("OldName", false);
            _fake.Answer = (name, auto) => NameResult.Fail("server_unavailable");

            NameService.ChangeResult result = Wait(NameService.ChangeName("NewName"));
            Assert.IsFalse(result.ok);
            Assert.AreEqual("OldName", SaveManager.GetPlayerName());
            StringAssert.Contains("isn't available", NameService.ErrorText(result));
        }

        [Test]
        public void WithoutAConnection_ThePlayerIsToldSo()
        {
            LeaderboardManager.SimulateSignedInForTests(null);
            LeaderboardManager.SimulateOfflineForTests(true);          // the services never become ready
            NameService.ChangeResult result = Wait(NameService.ChangeName("NewName"));
            Assert.IsFalse(result.ok);
            Assert.AreEqual("not_connected", result.code);
            Assert.IsEmpty(_fake.Calls);
        }

        // ------------------------------------------------------------------ the game registers names itself

        [Test]
        public void AGuestsFirstName_IsRegisteredOnce()
        {
            string name = SaveManager.EnsureDefaultName();
            Wait(NameService.EnsureRegistered());

            CollectionAssert.AreEqual(new[] { "auto " + name }, _fake.Calls);
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-A"));
            Assert.AreEqual(0, SaveManager.GetNameChangeWaitSeconds(), "an automatic name starts no weekly clock");

            Wait(NameService.EnsureRegistered());
            Assert.AreEqual(1, _fake.Calls.Count, "already registered: the server is not asked again");
        }

        [Test]
        public void AGamePickedName_ThatIsTaken_IsReplacedByAnother()
        {
            string first = SaveManager.EnsureDefaultName();
            int asked = 0;
            _fake.Answer = (name, auto) => ++asked <= 2 ? NameResult.Fail("name_taken") : new NameResult { ok = true, name = name };

            Wait(NameService.EnsureRegistered());
            Assert.AreEqual(3, _fake.Calls.Count);
            Assert.AreNotEqual(first, SaveManager.GetPlayerName());
            StringAssert.IsMatch("^Pilot[a-z0-9]{4}$", SaveManager.GetPlayerName());
            Assert.IsTrue(SaveManager.IsPlayerNameAuto());
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-A"));
        }

        [Test]
        public void ANameThePlayerChose_IsNeverReplaced_EvenWhenTaken()
        {
            SaveManager.SetPlayerName("Ngọc", false);
            _fake.Answer = (name, auto) => NameResult.Fail("name_taken");

            Wait(NameService.EnsureRegistered());
            Assert.AreEqual(1, _fake.Calls.Count);
            Assert.AreEqual("Ngọc", SaveManager.GetPlayerName());
            Assert.IsFalse(SaveManager.IsNameRegisteredFor("player-A"));
        }

        [Test]
        public void AProfileThatMovedToAnotherIdentity_RegistersItsNameAgain_WithoutTheOldWeeklyLock()
        {
            SaveManager.SetPlayerName("Hawkeye", false);
            SaveManager.MarkNameRegistered("player-OLD", "Hawkeye", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5 * 86400);

            Wait(NameService.EnsureRegistered());
            CollectionAssert.AreEqual(new[] { "auto Hawkeye" }, _fake.Calls);
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-A"));
            Assert.AreEqual(0, SaveManager.GetNameChangeWaitSeconds(), "the other identity's lock does not carry over");
        }

        // ------------------------------------------------------------------ a signed-in player is shown under the account name

        [Test]
        public void ASignedInPlayer_IsShownUnderTheAccountName_FromTheMomentTheAccountIsLinked()
        {
            string guestName = SaveManager.EnsureDefaultName();
            Assert.IsTrue(PlayerNameRules.IsGameDefault(guestName));

            SaveManager.SetAccountLinked(true, "thanhne");          // before any server call
            Assert.AreEqual("thanhne", SaveManager.GetPlayerName());
            Assert.IsTrue(SaveManager.IsPlayerNameAuto(), "it is still a name the game picked: the first change is free");
            Assert.AreEqual("thanhne", SaveManager.EnsureDefaultName(), "asking again changes nothing");
        }

        [Test]
        public void TheAccountName_IsRegisteredWithTheServer_AndNeverTheGamesGuestName()
        {
            SaveManager.EnsureDefaultName();
            SaveManager.SetAccountLinked(true, "pilot_hawk");

            Wait(NameService.EnsureRegistered());
            CollectionAssert.AreEqual(new[] { "auto pilot_hawk" }, _fake.Calls);
            Assert.AreEqual("pilot_hawk", SaveManager.GetPlayerName());
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-A"));
            Assert.AreEqual(0, SaveManager.GetNameChangeWaitSeconds(), "taking the account name starts no weekly clock");
        }

        [Test]
        public void TheAccountName_IsShownEvenWhenTheServerIsNotThere()
        {
            _fake.Answer = (name, auto) => NameResult.Fail("server_unavailable");
            SaveManager.EnsureDefaultName();
            SaveManager.SetAccountLinked(true, "pilot_hawk");

            Wait(NameService.EnsureRegistered());
            Assert.AreEqual("pilot_hawk", SaveManager.GetPlayerName(), "not waiting for a server that may not answer");
            Assert.IsFalse(SaveManager.IsNameRegisteredFor("player-A"), "registered later, when it can be");
        }

        [Test]
        public void AnAccountNameSomebodyElseHolds_BecomesTheAccountNameWithAShortTail_AndStaysThere()
        {
            _fake.Answer = (name, auto) => name == "pilot_hawk" ? NameResult.Fail("name_taken") : new NameResult { ok = true, name = name };
            SaveManager.EnsureDefaultName();
            SaveManager.SetAccountLinked(true, "pilot_hawk");

            Wait(NameService.EnsureRegistered());
            string name = SaveManager.GetPlayerName();
            StringAssert.IsMatch("^pilot_hawk_[a-z0-9]{4}$", name);
            Assert.IsTrue(SaveManager.IsNameRegisteredFor("player-A"));

            Wait(NameService.EnsureRegistered());
            Wait(NameService.EnsureRegistered());
            Assert.AreEqual(name, SaveManager.GetPlayerName(), "it does not hop to a new name every session");
            Assert.AreEqual(2, _fake.Calls.Count, "the account name, then the variant - nothing more");
        }

        [Test]
        public void ALongAccountName_IsCutToWhatANameMayBe()
        {
            SaveManager.SetAccountLinked(true, "a_username_longer_than_sixteen");
            Assert.AreEqual("a_username_longe", SaveManager.GetPlayerName());
            Assert.AreEqual(PlayerNameRules.MaxLength, SaveManager.GetPlayerName().Length);
        }

        [Test]
        public void ANameThePlayerChose_IsNeverReplacedByTheAccountName()
        {
            SaveManager.SetPlayerName("MyOwnName", false);
            SaveManager.SetAccountLinked(true, "pilot_hawk");
            Assert.AreEqual("MyOwnName", SaveManager.GetPlayerName());
        }

        [Test]
        public void AnOlderAccountStillShowingTheGamesGuestName_GetsItsAccountNameBack()
        {
            // Saved by a version that did not yet give accounts their own name.
            SaveManager.Data.accountLinked = true;
            SaveManager.Data.accountUsername = "thanhne";
            SaveManager.Data.playerName = "Pilotx3ur";
            SaveManager.Data.nameIsAuto = true;

            Assert.AreEqual("thanhne", SaveManager.EnsureDefaultName());
        }

        [Test]
        public void LoggingOut_GivesTheFreshGuestItsOwnName_NotTheAccountName()
        {
            SaveManager.SetAccountLinked(true, "thanhne");
            SaveManager.RestoreGuestProfile();
            Assert.IsTrue(PlayerNameRules.IsGameDefault(SaveManager.GetPlayerName()));
            Assert.AreNotEqual("thanhne", SaveManager.GetPlayerName());
        }

        [Test]
        public void AccountNames_AreClean_OrNotUsedAtAll()
        {
            Assert.AreEqual("thanhne", PlayerNameRules.AccountDefault("  thanhne "));
            Assert.AreEqual("a_username_longe", PlayerNameRules.AccountDefault("a_username_longer_than_sixteen"));
            Assert.IsNull(PlayerNameRules.AccountDefault(""));
            Assert.IsNull(PlayerNameRules.AccountDefault("ab"), "too short to be a name");
            StringAssert.IsMatch("^thanhne_[a-z0-9]{4}$", PlayerNameRules.VariantOf("thanhne"));
            Assert.LessOrEqual(PlayerNameRules.VariantOf("a_username_longe").Length, PlayerNameRules.MaxLength);
            Assert.IsTrue(PlayerNameRules.IsGameDefault("Pilot7k2q"));
            Assert.IsFalse(PlayerNameRules.IsGameDefault("Pilot"));
            Assert.IsFalse(PlayerNameRules.IsGameDefault("pilot7k2q"));
            Assert.IsFalse(PlayerNameRules.IsGameDefault("thanhne"));
        }
    }
}
