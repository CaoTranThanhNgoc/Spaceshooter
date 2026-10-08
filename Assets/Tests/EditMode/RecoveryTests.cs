using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.Tests
{
    public class RecoveryContactTests
    {
        [TestCase("Pilot@Example.COM", "pilot@example.com")]
        [TestCase("  pilot@example.com ", "pilot@example.com")]
        [TestCase("me.name+game@gmail.com", "me.name+game@gmail.com")]
        public void ValidEmails_AreNormalized(string typed, string canonical)
        {
            Assert.AreEqual(canonical, RecoveryContact.Normalize(typed));
            Assert.IsTrue(RecoveryContact.IsValid(typed));
            Assert.AreEqual(canonical, RecoveryContact.Normalize(canonical), "Normalizing twice changes nothing.");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("pilot")]
        [TestCase("pilot@")]
        [TestCase("@example.com")]
        [TestCase("pilot@example")]
        [TestCase("two words@example.com")]
        [TestCase("12345")]
        [TestCase("0912 345 678")]      // phone numbers are not accepted
        [TestCase("+84912345678")]
        public void InvalidEmails_AreRefused(string typed)
        {
            Assert.IsNull(RecoveryContact.Normalize(typed));
            Assert.IsFalse(RecoveryContact.IsValid(typed));
        }

        [Test]
        public void MaskedEmails_DoNotGiveTheAddressAway()
        {
            Assert.AreEqual("pi***@example.com", RecoveryContact.Mask("pilot@example.com"));
            Assert.AreEqual("a***@example.com", RecoveryContact.Mask("a@example.com"));
            Assert.AreEqual("", RecoveryContact.Mask(""));
            Assert.AreEqual("", RecoveryContact.Mask("garbage"));
            Assert.AreEqual("", RecoveryContact.Mask("+84912345678"));
        }
    }

    /// <summary>Everything the game does around password recovery, against a fake server.</summary>
    public class RecoveryFlowTests
    {
        private class FakeBackend : IRecoveryBackend
        {
            public readonly List<string> Calls = new List<string>();
            public RecoveryResult Next = RecoveryResult.Ok;

            public Task<RecoveryResult> SendVerification(string contact) { Calls.Add("send " + contact); return Task.FromResult(Next); }
            public Task<RecoveryResult> VerifyContact(string contact, string code) { Calls.Add($"verify {contact} {code}"); return Task.FromResult(Next); }
            public Task<RecoveryResult> SetContact(string username, string contact) { Calls.Add($"set {username} {contact}"); return Task.FromResult(Next); }
            public Task<RecoveryResult> RequestReset(string username, string contact) { Calls.Add($"request {username} {contact}"); return Task.FromResult(Next); }
            public Task<RecoveryResult> ConfirmReset(string username, string contact, string code, string newPassword)
            {
                Calls.Add($"confirm {username} {contact} {code} {newPassword}");
                return Task.FromResult(Next);
            }
            public Task<RecoveryResult> DeleteAccountData() { Calls.Add("delete"); return Task.FromResult(Next); }
        }

        private FakeBackend _fake;
        private IRecoveryBackend _real;

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            _real = AccountManager.Recovery;
            _fake = new FakeBackend();
            AccountManager.Recovery = _fake;
        }

        [TearDown]
        public void TearDown()
        {
            AccountManager.Recovery = _real;
            AccountManager.ForgetUsername("flow_tester");
        }

        private static T Wait<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(2000), "the call did not finish");
            return task.Result;
        }

        [Test]
        public void RequestPasswordReset_SendsTheCanonicalContact()
        {
            RecoveryResult result = Wait(AccountManager.RequestPasswordReset("  flow_tester ", "Pilot@Example.com"));
            Assert.IsTrue(result.ok);
            Assert.AreEqual(new[] { "request flow_tester pilot@example.com" }, _fake.Calls.ToArray());
        }

        [Test]
        public void RequestPasswordReset_WithABadContactOrNoUsername_NeverReachesTheServer()
        {
            Assert.AreEqual("invalid_contact", Wait(AccountManager.RequestPasswordReset("flow_tester", "not a contact")).error);
            Assert.AreEqual("invalid_contact", Wait(AccountManager.RequestPasswordReset("", "a@b.co")).error);
            Assert.IsEmpty(_fake.Calls);
        }

        [Test]
        public void ConfirmPasswordReset_WeakPassword_NeverReachesTheServer()
        {
            RecoveryResult result = Wait(AccountManager.ConfirmPasswordReset("flow_tester", "a@b.co", "123456", "weakpass"));
            Assert.AreEqual("weak_password", result.error);
            Assert.IsEmpty(_fake.Calls);
        }

        [Test]
        public void ConfirmPasswordReset_Success_RemembersTheAccountAndAnnouncesIt()
        {
            string announced = null;
            void OnDone(string username) => announced = username;
            AccountManager.PasswordResetDone += OnDone;
            try
            {
                RecoveryResult result = Wait(AccountManager.ConfirmPasswordReset("flow_tester", "A@B.co", " 123456 ", "Brand-new1"));
                Assert.IsTrue(result.ok);
                Assert.AreEqual(new[] { "confirm flow_tester a@b.co 123456 Brand-new1" }, _fake.Calls.ToArray());
                Assert.AreEqual("flow_tester", announced);
                Assert.Contains("flow_tester", AccountManager.GetRecentUsernames());
            }
            finally
            {
                AccountManager.PasswordResetDone -= OnDone;
            }
        }

        [Test]
        public void ConfirmPasswordReset_ServerRefusal_AnnouncesNothing()
        {
            _fake.Next = RecoveryResult.Fail("invalid_code");
            bool announced = false;
            void OnDone(string _) => announced = true;
            AccountManager.PasswordResetDone += OnDone;
            try
            {
                Assert.AreEqual("invalid_code", Wait(AccountManager.ConfirmPasswordReset("flow_tester", "a@b.co", "000000", "Brand-new1")).error);
                Assert.IsFalse(announced);
            }
            finally
            {
                AccountManager.PasswordResetDone -= OnDone;
            }
        }

        [Test]
        public void SetRecoveryContact_NeedsAnAccount_AndStoresTheCanonicalForm()
        {
            Assert.IsFalse(Wait(AccountManager.SetRecoveryContact("a@b.co")).ok, "a guest has no account to attach it to");
            Assert.IsEmpty(_fake.Calls);

            SaveManager.SetAccountLinked(true, "flow_tester");
            int changed = 0;
            void OnChanged() => changed++;
            AccountManager.RecoveryContactChanged += OnChanged;
            try
            {
                Assert.AreEqual("invalid_contact", Wait(AccountManager.SetRecoveryContact("nope")).error);
                Assert.IsTrue(Wait(AccountManager.SetRecoveryContact("Pilot@Example.com")).ok);
                Assert.AreEqual("set flow_tester pilot@example.com", _fake.Calls[0]);
                Assert.AreEqual("pilot@example.com", SaveManager.GetRecoveryContact());
                Assert.IsTrue(AccountManager.HasRecoveryContact);
                Assert.AreEqual(1, changed);
            }
            finally
            {
                AccountManager.RecoveryContactChanged -= OnChanged;
            }
        }

        [Test]
        public void ARefusedContact_IsNotStoredLocally()
        {
            SaveManager.SetAccountLinked(true, "flow_tester");
            _fake.Next = RecoveryResult.Fail("not_verified");
            Assert.AreEqual("not_verified", Wait(AccountManager.SetRecoveryContact("a@b.co")).error);
            Assert.IsFalse(AccountManager.HasRecoveryContact);
            Assert.AreEqual("", SaveManager.GetRecoveryContact());
        }

        [Test]
        public void RequestContactVerification_SendsTheCanonicalContact()
        {
            Assert.IsTrue(Wait(AccountManager.RequestContactVerification(" Pilot@Example.com ")).ok);
            Assert.AreEqual(new[] { "send pilot@example.com" }, _fake.Calls.ToArray());
        }

        [Test]
        public void VerifyContact_SendsTheCodeTrimmed_AndABadContactNeverReachesTheServer()
        {
            Assert.IsTrue(Wait(AccountManager.VerifyContact("A@B.co", " 123456 ")).ok);
            Assert.AreEqual(new[] { "verify a@b.co 123456" }, _fake.Calls.ToArray());

            _fake.Calls.Clear();
            Assert.AreEqual("invalid_contact", Wait(AccountManager.VerifyContact("nope", "123456")).error);
            Assert.AreEqual("invalid_contact", Wait(AccountManager.RequestContactVerification("nope")).error);
            Assert.IsEmpty(_fake.Calls);
        }

        [Test]
        public void ATakenOverContact_IsReportedToThePlayer()
        {
            SaveManager.SetAccountLinked(true, "flow_tester");
            _fake.Next = new RecoveryResult { ok = true, transferred = true };
            RecoveryResult result = Wait(AccountManager.SetRecoveryContact("a@b.co"));
            Assert.IsTrue(result.ok);
            Assert.IsTrue(result.transferred, "The panel tells the player the contact moved from another account.");
            Assert.AreEqual("a@b.co", SaveManager.GetRecoveryContact());
        }

        [Test]
        public void AContactFromBeforeVerificationExisted_CountsAsNone()
        {
            SaveManager.SetAccountLinked(true, "flow_tester");
            SaveManager.Data.recoveryContact = "old@b.co";        // saved by an older version, never verified
            SaveManager.Data.recoveryContactVerified = false;
            Assert.IsFalse(AccountManager.HasRecoveryContact);

            SaveManager.SetRecoveryContact("old@b.co");           // verified and attached now
            Assert.IsTrue(AccountManager.HasRecoveryContact);
        }

        [Test]
        public void TheRecoveryContact_BelongsToTheAccount_NotTheGuestProfile()
        {
            SaveManager.Data.crystals = 55;
            SaveManager.StashGuestProfile();
            SaveManager.SetAccountLinked(true, "flow_tester");
            SaveManager.SetRecoveryContact("pilot@example.com");
            Assert.AreEqual("pilot@example.com", SaveManager.GetRecoveryContact());

            SaveManager.RestoreGuestProfile();
            Assert.AreEqual("", SaveManager.GetRecoveryContact(), "Logged out: the guest has no recovery contact.");
            Assert.AreEqual(55, SaveManager.GetCrystals());
        }

        [TestCase("invalid_contact")]
        [TestCase("not_verified")]
        [TestCase("rate_limited")]
        [TestCase("too_soon")]
        [TestCase("send_failed")]
        [TestCase("invalid_code")]
        [TestCase("code_expired")]
        [TestCase("too_many_attempts")]
        [TestCase("weak_password")]
        [TestCase("reset_failed")]
        [TestCase("purge_failed")]
        [TestCase("cleanup_failed")]
        [TestCase("server_not_configured")]
        [TestCase("server_unavailable")]
        [TestCase("something_new")]
        public void EveryServerAnswer_HasATextInBothLanguages(string code)
        {
            RecoveryResult result = RecoveryResult.Fail(code, 42);
            foreach (Language language in new[] { Language.English, Language.Vietnamese })
            {
                Localization.SetLanguage(language);
                string text = AccountManager.RecoveryErrorText(result);
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), code);
                Assert.IsFalse(text.StartsWith("account."), $"{code}: a missing translation shows its key ({text})");
            }
            Localization.SetLanguage(Language.English);
        }

        [Test]
        public void TheWaitTime_IsShownWhenAskingTooSoon()
        {
            Localization.SetLanguage(Language.English);
            StringAssert.Contains("42", AccountManager.RecoveryErrorText(RecoveryResult.Fail("too_soon", 42)));
        }
    }

    /// <summary>The set-up check explains what is missing instead of just failing.</summary>
    public class AccountServerCheckTests
    {
        private static string Describe(bool admin, bool pepper, bool email) =>
            AccountServerCheck.Describe(new AccountServerCheck.Report { ok = true, adminAuth = admin, adminAuthWorks = admin, adminAuthStatus = admin ? 200 : 0, pepper = pepper, email = email });

        [Test]
        public void EverythingSet_IsReady()
        {
            string text = Describe(true, true, true);
            StringAssert.Contains("Ready", text);
            StringAssert.DoesNotContain("MISSING", text);
        }

        [Test]
        public void EachMissingPiece_IsNamed()
        {
            StringAssert.Contains("UGS_ADMIN_AUTH:           MISSING", Describe(false, true, true));
            StringAssert.Contains("RECOVERY_PEPPER:          MISSING", Describe(true, false, true));
            string noChannel = Describe(true, true, false);
            StringAssert.Contains("Not ready", noChannel);
            StringAssert.Contains("MAIL_RELAY_URL", noChannel);
        }

        [Test]
        public void ThereIsNoMentionOfPhonesOrSms()
        {
            string text = Describe(true, true, true).ToLowerInvariant();
            StringAssert.DoesNotContain("sms", text);
            StringAssert.DoesNotContain("twilio", text);
            StringAssert.DoesNotContain("phone", text);
        }

        [Test]
        public void ARejectedAdminCredential_IsNotReady_AndSaysWhy()
        {
            string text = AccountServerCheck.Describe(new AccountServerCheck.Report
            { ok = true, adminAuth = true, adminAuthWorks = false, adminAuthStatus = 401, pepper = true, email = true });
            StringAssert.Contains("REJECTED", text);
            StringAssert.Contains("401", text);
            StringAssert.Contains("Not ready", text);
            StringAssert.DoesNotContain("Leaderboards Admin", text);

            string forbidden = AccountServerCheck.Describe(new AccountServerCheck.Report
            { ok = true, adminAuth = true, adminAuthWorks = false, adminAuthStatus = 403, pepper = true, email = true });
            StringAssert.Contains("Leaderboards Admin", forbidden);
        }

        [Test]
        public void AnEmptyAnswer_AsksForARedeploy()
        {
            StringAssert.Contains("redeploy", AccountServerCheck.Describe(null));
            StringAssert.Contains("redeploy", AccountServerCheck.Describe(new AccountServerCheck.Report { ok = false }));
        }
    }
}
