using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>"Forgot password?": two steps. (1) username + the e-mail saved on the
    /// account -> a 6-digit code is sent there. (2) the code + a new password -> the account gets that
    /// password and the player signs in as usual. All the checking happens on the server (see
    /// Assets/CloudCode/AccountRecovery.js); this panel only collects the input and shows the answer.</summary>
    public class ForgotPasswordPanel : MonoBehaviour
    {
        public TMP_Text descriptionLabel;
        public TMP_Text errorLabel;
        public TMP_Text statusLabel;
        public Button closeButton;

        [Header("Step 1 - ask for a code")]
        public GameObject step1Root;
        public TMP_InputField usernameInput;
        public TMP_InputField contactInput;

        [Header("Step 2 - the code and a new password")]
        public GameObject step2Root;
        public TMP_InputField codeInput;
        public TMP_InputField newPasswordInput;
        public TMP_InputField confirmPasswordInput;
        public Button resendButton;
        public TMP_Text resendLabel;

        [Header("Main button (SEND CODE / RESET PASSWORD)")]
        public Button actionButton;
        public TMP_Text actionLabel;

        private const float ResendSeconds = 60f;

        private bool _confirmStep;
        private bool _busy;
        private string _username;
        private string _contact;           // as the player typed it
        private float _resendAt;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            if (resendButton != null) resendButton.onClick.AddListener(Resend);
        }

        private void OnEnable()
        {
            string[] recent = AccountManager.GetRecentUsernames();
            if (usernameInput != null && string.IsNullOrEmpty(usernameInput.text) && recent.Length > 0) usernameInput.text = recent[0];
            ShowStep(false);
            Localization.LanguageChanged += RefreshTexts;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshTexts;
        }

        private void Update()
        {
            if (!_confirmStep) return;
            RefreshResendButton();
        }

        /// <summary>Goes straight to the second step for a contact (used when the code was already sent,
        /// and by the screenshot tests).</summary>
        public void OpenCodeStep(string contact)
        {
            _contact = contact;
            _username = string.IsNullOrEmpty(_username) ? (usernameInput != null ? usernameInput.text.Trim() : "") : _username;
            _resendAt = Time.unscaledTime + 42f;
            ShowStep(true);
        }

        private void ShowStep(bool confirm)
        {
            _confirmStep = confirm;
            if (step1Root != null) step1Root.SetActive(!confirm);
            if (step2Root != null) step2Root.SetActive(confirm);
            if (resendButton != null) resendButton.gameObject.SetActive(confirm);
            RefreshTexts();
            RefreshResendButton();
        }

        private void RefreshTexts()
        {
            if (descriptionLabel != null)
            {
                descriptionLabel.text = _confirmStep
                    ? Localization.Format("account.forgot_step2_desc_fmt", RecoveryContact.Mask(RecoveryContact.Normalize(_contact) ?? ""))
                    : Localization.Get("account.forgot_step1_desc");
            }
            if (actionLabel != null) actionLabel.text = Localization.Get(_confirmStep ? "account.forgot_reset" : "account.forgot_send");
            RefreshResendButton();
        }

        private void RefreshResendButton()
        {
            if (resendButton == null) return;
            float wait = _resendAt - Time.unscaledTime;
            bool ready = wait <= 0f && !_busy;
            resendButton.interactable = ready;
            if (resendLabel != null)
            {
                string text = Localization.Get("account.forgot_resend");
                resendLabel.text = wait > 0f ? $"{text} ({Mathf.CeilToInt(wait)})" : text;
            }
        }

        private void OnAction()
        {
            if (_busy) return;
            if (_confirmStep) ConfirmReset(); else RequestCode();
        }

        private async void RequestCode()
        {
            string username = usernameInput != null ? usernameInput.text.Trim() : "";
            string contact = contactInput != null ? contactInput.text.Trim() : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(contact))
            {
                ShowError(Localization.Get("account.fill_all_fields"));
                return;
            }
            if (!RecoveryContact.IsValid(contact))
            {
                ShowError(Localization.Get("account.error_invalid_contact"));
                return;
            }

            _username = username;
            _contact = contact;
            await SendCode();
        }

        private async void Resend()
        {
            if (_busy || string.IsNullOrEmpty(_username)) return;
            await SendCode();
        }

        private async System.Threading.Tasks.Task SendCode()
        {
            ShowError(null);
            SetBusy(true);
            RecoveryResult result = await AccountManager.RequestPasswordReset(_username, _contact);
            if (this == null) return;
            SetBusy(false);

            if (!result.ok)
            {
                if (result.error == "too_soon") _resendAt = Time.unscaledTime + Mathf.Max(1, result.retryAfter);
                ShowError(AccountManager.RecoveryErrorText(result));
                return;
            }

            _resendAt = Time.unscaledTime + ResendSeconds;
            ShowStep(true);
            ShowStatus(Localization.Get("account.forgot_sent"));
            if (codeInput != null) codeInput.Select();
        }

        private async void ConfirmReset()
        {
            string code = codeInput != null ? codeInput.text.Trim() : "";
            string next = newPasswordInput != null ? newPasswordInput.text : "";
            string confirm = confirmPasswordInput != null ? confirmPasswordInput.text : "";

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(next) || string.IsNullOrEmpty(confirm))
            {
                ShowError(Localization.Get("account.fill_all_fields"));
                return;
            }
            if (next != confirm)
            {
                ShowError(Localization.Get("account.passwords_dont_match"));
                return;
            }
            if (!AccountManager.IsPasswordValid(next))
            {
                ShowError(Localization.Get("account.error_weak_password"));
                return;
            }

            ShowError(null);
            SetBusy(true);
            RecoveryResult result = await AccountManager.ConfirmPasswordReset(_username, _contact, code, next);
            if (this == null) return;
            SetBusy(false);

            if (!result.ok)
            {
                ShowError(AccountManager.RecoveryErrorText(result));
                // A spent or expired code is no use any more: back to asking for a new one.
                if (result.error == "too_many_attempts" || result.error == "code_expired") ShowStep(false);
                return;
            }

            ToastUI.ShowToast(transform.parent != null ? transform.parent : transform, Localization.Get("account.forgot_done"));
            Close();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (actionButton != null) actionButton.interactable = !busy;
            RefreshResendButton();
        }

        private void ShowError(string message)
        {
            if (errorLabel != null)
            {
                errorLabel.text = message;
                errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
            if (!string.IsNullOrEmpty(message) && statusLabel != null) statusLabel.gameObject.SetActive(false);
        }

        private void ShowStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
                statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
            if (!string.IsNullOrEmpty(message) && errorLabel != null) errorLabel.gameObject.SetActive(false);
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
