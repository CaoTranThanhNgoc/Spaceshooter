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
        [Tooltip("The line above the name field: what the name is for, or - while it cannot be changed yet - when it can.")]
        public TMP_Text nameHintLabel;
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

        private bool _saving;
        private bool _nameLocked;
        private float _lockTimer;
        private TMP_Text _confirmLabel;
        private Color _confirmLabelColor = Color.white;
        private Color _nameTextColor = Color.white;
        private static readonly Color SwitchedOffTint = new Color(1f, 1f, 1f, 0.55f);
        // The switched-off button is grey (the pack's Disable_Btn): its caption has to be dark to stay readable on it.
        private static readonly Color SwitchedOffCaption = new Color(0.27f, 0.32f, 0.36f, 1f);

        private void Awake()
        {
            if (confirmButton != null)
            {
                _confirmLabel = confirmButton.GetComponentInChildren<TMP_Text>();
                if (_confirmLabel != null) _confirmLabelColor = _confirmLabel.color;
            }
            if (nameInput != null && nameInput.textComponent != null) _nameTextColor = nameInput.textComponent.color;

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
            RefreshNameHint();
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

        // The name shown is the one the Leaderboard shows: every profile has one (the account name for a signed-in
        // player, the game's "PilotXXXX" for a guest) until the player picks another.
        private void RefreshNameField()
        {
            if (nameInput != null) nameInput.text = LeaderboardManager.GetDisplayName();
            RefreshNameLock();
        }

        // The name can be changed once a week. Until then the field and the button are switched off and the line above
        // them counts down; the moment the wait is over they come back on by themselves.
        private void RefreshNameLock()
        {
            long wait = SaveManager.GetNameChangeWaitSeconds();
            _nameLocked = wait > 0;
            bool canEdit = !_nameLocked && !_saving;
            if (nameInput != null)
            {
                nameInput.interactable = canEdit;
                if (nameInput.textComponent != null) nameInput.textComponent.color = canEdit ? _nameTextColor : _nameTextColor * SwitchedOffTint;
            }
            if (confirmButton != null) confirmButton.interactable = canEdit;
            if (_confirmLabel != null) _confirmLabel.color = canEdit ? _confirmLabelColor : SwitchedOffCaption;
            RefreshNameHint();
        }

        // While the weekly lock is on, the line above the name says when it ends; otherwise it says what the name is for.
        private void RefreshNameHint()
        {
            if (nameHintLabel == null) return;
            long wait = SaveManager.GetNameChangeWaitSeconds();
            nameHintLabel.text = wait > 0
                ? Localization.Format("profile.name_locked_fmt", PlayerNameRules.FormatWait(wait))
                : Localization.Get("leaderboard.choose_name_desc");
        }

        private void Update()
        {
            if (!_nameLocked) return;
            _lockTimer += Time.unscaledDeltaTime;
            if (_lockTimer < 1f) return;
            _lockTimer = 0f;
            RefreshNameLock();   // keeps the countdown current and switches the field back on when the wait is over
        }

        private async void Confirm()
        {
            if (_nameLocked || _saving) return;
            string typed = nameInput != null ? nameInput.text : "";

            _saving = true;
            RefreshNameLock();
            NameService.ChangeResult result = await NameService.ChangeName(typed);
            if (this == null) return; // panel closed while the network call was in flight
            _saving = false;
            RefreshNameLock();

            // The notice goes on the overlay root, so it survives this panel closing.
            Transform noticeRoot = transform.parent != null ? transform.parent : transform;
            if (result.ok)
            {
                ToastUI.ShowToast(noticeRoot, Localization.Get("profile.name_saved"));
                Close();
                return;
            }

            ToastUI.ShowToast(noticeRoot, NameService.ErrorText(result));
            RefreshNameLock();
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
