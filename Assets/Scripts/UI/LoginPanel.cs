using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Switches to a different, already-registered identity (e.g. restoring an account
    /// on a new device) - opened from PlayerProfilePanel's "SIGN IN" button.
    ///
    /// For a player who has forgotten something: the usernames last used on this device are
    /// offered as tap-to-fill chips (and the newest is pre-filled), a SHOW button reveals the
    /// password being typed, and "Forgot password?" starts a reset by a code sent to the e-mail
    /// saved on the account.</summary>
    public class LoginPanel : MonoBehaviour
    {
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        public TMP_Text errorLabel;
        public Button confirmButton;
        public Button closeButton;

        [Header("Forgotten username / password")]
        public GameObject recentRow;
        public Button[] recentButtons;
        public TMP_Text[] recentLabels;
        public Button showPasswordButton;
        public TMP_Text showPasswordLabel;
        public Button forgotPasswordButton;

        private bool _showPassword;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Submit);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (showPasswordButton != null) showPasswordButton.onClick.AddListener(ToggleShowPassword);
            if (forgotPasswordButton != null) forgotPasswordButton.onClick.AddListener(ShowForgotPassword);

            if (recentButtons != null)
            {
                for (int i = 0; i < recentButtons.Length; i++)
                {
                    int index = i;
                    if (recentButtons[i] != null) recentButtons[i].onClick.AddListener(() => FillRecent(index));
                }
            }
        }

        private void OnEnable()
        {
            SetError(null);
            RefreshRecent();
            RefreshShowPasswordLabel();
            Localization.LanguageChanged += RefreshShowPasswordLabel;
            AccountManager.PasswordResetDone += OnPasswordResetDone;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshShowPasswordLabel;
            AccountManager.PasswordResetDone -= OnPasswordResetDone;
        }

        // The password was just reset from "Forgot password?": ready to sign in with the new one.
        private void OnPasswordResetDone(string username)
        {
            if (this == null) return;
            if (usernameInput != null) usernameInput.text = username;
            if (passwordInput != null)
            {
                passwordInput.text = "";
                passwordInput.Select();
            }
            RefreshRecent();
            SetError(null);
        }

        private void RefreshRecent()
        {
            string[] names = AccountManager.GetRecentUsernames();
            if (recentRow != null) recentRow.SetActive(names.Length > 0);

            if (recentButtons != null)
            {
                for (int i = 0; i < recentButtons.Length; i++)
                {
                    if (recentButtons[i] == null) continue;
                    bool has = i < names.Length;
                    recentButtons[i].gameObject.SetActive(has);
                    if (has && recentLabels != null && i < recentLabels.Length && recentLabels[i] != null) recentLabels[i].text = names[i];
                }
            }

            // The most likely answer to "which account was it?" is the last one used here.
            if (usernameInput != null && string.IsNullOrEmpty(usernameInput.text) && names.Length > 0)
                usernameInput.text = names[0];
        }

        private void FillRecent(int index)
        {
            string[] names = AccountManager.GetRecentUsernames();
            if (index < 0 || index >= names.Length) return;
            if (usernameInput != null) usernameInput.text = names[index];
            if (passwordInput != null) passwordInput.Select();
            SetError(null);
        }

        private void ToggleShowPassword()
        {
            _showPassword = !_showPassword;
            if (passwordInput != null)
            {
                passwordInput.contentType = _showPassword ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                passwordInput.ForceLabelUpdate();
            }
            RefreshShowPasswordLabel();
        }

        private void RefreshShowPasswordLabel()
        {
            if (showPasswordLabel != null)
                showPasswordLabel.text = Localization.Get(_showPassword ? "account.hide_password" : "account.show_password");
        }

        /// <summary>Opens the recovery flow on top of this screen (it returns here when it is done).</summary>
        private void ShowForgotPassword()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/ForgotPasswordPanel");
            if (prefab != null) Instantiate(prefab, transform.parent != null ? transform.parent : transform);
        }

        private async void Submit()
        {
            string username = usernameInput != null ? usernameInput.text.Trim() : "";
            string password = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetError(Localization.Get("account.fill_all_fields"));
                return;
            }

            SetError(null);
            if (confirmButton != null) confirmButton.interactable = false;
            (bool success, string error) = await AccountManager.SignIn(username, password);
            if (this == null) return;
            if (confirmButton != null) confirmButton.interactable = true;

            if (success)
            {
                // Toast is parented to transform.parent (survives this panel closing), not
                // transform (this panel) - it used to be destroyed along with this panel the
                // instant Close() ran, cutting the success message off before anyone could read it.
                ToastUI.ShowToast(transform.parent != null ? transform.parent : transform, Localization.Get("account.signin_success"));
                if (usernameInput != null) usernameInput.interactable = false;
                if (passwordInput != null) passwordInput.interactable = false;
                await Task.Delay(2000);
                if (this == null) return; // closed some other way while waiting
                Close();
            }
            else
            {
                SetError(error);
            }
        }

        private void SetError(string message)
        {
            if (errorLabel == null) return;
            errorLabel.text = message;
            errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
