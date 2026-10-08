using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Standalone overlay opened from the player icon button on Level Select - both the
    /// first-ever naming prompt (opened by AuthGatePanel/MainMenuUI when no name is chosen yet)
    /// and an always-available "my profile" screen afterward: display name, lifetime stats
    /// (score/crystals/energy), and account management.
    ///
    /// Guest players see CREATE ACCOUNT / SIGN IN; players with a linked account see LOG OUT /
    /// DELETE ACCOUNT instead - see AccountManager for what each of those actually does.</summary>
    public class PlayerProfilePanel : MonoBehaviour
    {
        public TMP_InputField nameInput;
        public Button confirmButton;
        public Button closeButton;

        public TMP_Text scoreLabel;
        public TMP_Text crystalLabel;
        public TMP_Text energyLabel;

        public TMP_Text accountStatusLabel;
        public TMP_Text accountHintLabel;
        public Button registerButton;
        public Button signInButton;
        public Button signOutButton;
        public Button deleteAccountButton;
        public Button changePasswordButton;

        [Header("Recovery contact (the e-mail for a forgotten password)")]
        public TMP_Text recoveryLabel;
        public Button recoveryButton;
        public TMP_Text recoveryButtonLabel;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (registerButton != null) registerButton.onClick.AddListener(OpenRegister);
            if (signInButton != null) signInButton.onClick.AddListener(OpenSignIn);
            if (signOutButton != null) signOutButton.onClick.AddListener(DoSignOut);
            if (deleteAccountButton != null) deleteAccountButton.onClick.AddListener(ConfirmDeleteAccount);
            if (changePasswordButton != null) changePasswordButton.onClick.AddListener(OpenChangePassword);
            if (recoveryButton != null) recoveryButton.onClick.AddListener(OpenRecoveryContact);
        }

        private void OnEnable()
        {
            // Account button visibility is refreshed FIRST and on its own - if anything below
            // throws, the register/sign-in vs sign-out/delete buttons must still end up correct
            // rather than falling back to the prefab's build-time default for both pairs at once.
            RefreshAccountStatus();
            RefreshStats();
            RefreshNameField();
            Localization.LanguageChanged += OnLanguageChanged;
            AccountManager.AccountLinked += OnAccountLinked;
            SaveManager.ProfileChanged += OnProfileChanged;
            AccountManager.RecoveryContactChanged += RefreshAccountStatus;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= OnLanguageChanged;
            AccountManager.AccountLinked -= OnAccountLinked;
            SaveManager.ProfileChanged -= OnProfileChanged;
            AccountManager.RecoveryContactChanged -= RefreshAccountStatus;
        }

        // The progress on screen was swapped (account loaded from the cloud, or back to guest) - the
        // stats shown here belong to the other profile now.
        private void OnProfileChanged()
        {
            if (this == null) return;
            RefreshStats();
            RefreshNameField();
        }

        // AccountStatus/AccountHint/ScoreLabel mix in runtime data (username, current stats) that
        // no static LocalizedText component can reproduce, so this panel re-applies them itself
        // whenever the language changes while it's open, instead of relying on per-label locKeys.
        private void OnLanguageChanged()
        {
            RefreshAccountStatus();
            RefreshStats();
        }

        // Fires when Register/SignIn succeeds from a LoginPanel/RegisterPanel opened ON TOP of
        // this already-open panel (e.g. tapping "SIGN IN" from here). Without this, the account
        // section kept showing "Playing as Guest" until the panel was closed and reopened, even
        // though the player was now actually signed in.
        private void OnAccountLinked()
        {
            if (this == null) return;
            RefreshAccountStatus();
            RefreshNameField();
        }

        private void RefreshStats()
        {
            if (scoreLabel != null) scoreLabel.text = Localization.Format("profile.score_fmt", LeaderboardManager.GetLocalScore());
            if (crystalLabel != null) crystalLabel.text = SaveManager.GetCrystals().ToString();
            if (energyLabel != null) energyLabel.text = $"{SaveManager.GetEnergy()}/{SaveManager.MaxEnergy}";
        }

        private void RefreshNameField()
        {
            string current = SaveManager.GetPlayerName();
            if (nameInput != null) nameInput.text = string.IsNullOrEmpty(current) ? LeaderboardManager.GenerateRandomName() : current;
        }

        private async void Confirm()
        {
            string name = nameInput != null ? nameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name)) name = LeaderboardManager.GenerateRandomName();
            if (name.Length > 16) name = name.Substring(0, 16);

            if (confirmButton != null) confirmButton.interactable = false;
            await LeaderboardManager.SetPlayerName(name);
            if (this == null) return; // panel closed while the network call was in flight

            Close();
        }

        private void RefreshAccountStatus()
        {
            bool linked = AccountManager.IsLinked;
            if (accountStatusLabel != null)
            {
                accountStatusLabel.text = linked
                    ? Localization.Format("account.status_linked_fmt", AccountManager.CurrentUsername)
                    : Localization.Get("account.status_guest");
            }
            if (accountHintLabel != null)
            {
                // Signed in, the recovery row (below) takes this line's place.
                accountHintLabel.gameObject.SetActive(!linked);
                accountHintLabel.text = Localization.Get("profile.account_hint_guest");
            }

            if (registerButton != null) registerButton.gameObject.SetActive(!linked);
            if (signInButton != null) signInButton.gameObject.SetActive(!linked);
            if (signOutButton != null) signOutButton.gameObject.SetActive(linked);
            if (deleteAccountButton != null) deleteAccountButton.gameObject.SetActive(linked);
            if (changePasswordButton != null) changePasswordButton.gameObject.SetActive(linked);

            bool hasContact = AccountManager.HasRecoveryContact;
            if (recoveryLabel != null)
            {
                recoveryLabel.gameObject.SetActive(linked);
                recoveryLabel.text = hasContact
                    ? Localization.Format("profile.recovery_fmt", RecoveryContact.Mask(SaveManager.GetRecoveryContact()))
                    : Localization.Get("profile.recovery_none");
                recoveryLabel.color = hasContact ? RecoveryOkColor : RecoveryMissingColor;
            }
            if (recoveryButton != null)
            {
                recoveryButton.gameObject.SetActive(linked);
                if (recoveryButtonLabel != null) recoveryButtonLabel.text = Localization.Get(hasContact ? "account.contact_change" : "account.contact_add");
            }
        }

        private static readonly Color RecoveryOkColor = new Color(0.63f, 0.93f, 0.93f, 1f);
        private static readonly Color RecoveryMissingColor = new Color(1f, 0.72f, 0.4f, 1f);

        private void OpenRecoveryContact()
        {
            OpenOverlay("RecoveryContactPanel");
        }

        private void OpenChangePassword()
        {
            OpenOverlay("ChangePasswordPanel");
        }

        private void OpenRegister()
        {
            OpenOverlay("RegisterPanel");
        }

        private void OpenSignIn()
        {
            OpenOverlay("LoginPanel");
        }

        private void OpenOverlay(string resourceName)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + resourceName);
            if (prefab == null) return;
            Instantiate(prefab, transform.parent != null ? transform.parent : transform);
        }

        private async void DoSignOut()
        {
            // Saves the account's progress online first, so this takes a moment.
            if (signOutButton != null) signOutButton.interactable = false;
            ToastUI.ShowToast(transform, Localization.Get("account.signing_out"));
            await AccountManager.SignOut();
            if (this == null) return;

            if (signOutButton != null) signOutButton.interactable = true;
            ToastUI.ShowToast(transform, Localization.Get("account.signout_success"));
            RefreshAccountStatus();
            RefreshStats();
            RefreshNameField();
        }

        private void ConfirmDeleteAccount()
        {
            ConfirmDialog.Show(transform.parent != null ? transform.parent : transform,
                Localization.Get("account.delete_confirm_title"),
                Localization.Get("account.delete_confirm_desc"),
                DeleteAccountConfirmed);
        }

        private async void DeleteAccountConfirmed()
        {
            (bool ok, string error) = await AccountManager.DeleteAccount();
            if (this == null) return;

            ToastUI.ShowToast(transform, ok ? Localization.Get("account.delete_success") : (error ?? Localization.Get("account.delete_failed")));
            if (ok)
            {
                RefreshAccountStatus();
                RefreshStats();
                RefreshNameField();
            }
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
