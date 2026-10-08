using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Proves that an e-mail address belongs to the player: a 6-digit code is sent to it
    /// and has to be typed back. Opened on top of whatever is attaching the contact (the Create Account
    /// screen, or the Recovery Contact screen); it calls `onVerified` once the server confirmed the code
    /// and then closes. Closing it any other way verifies nothing.</summary>
    public class VerifyContactPanel : MonoBehaviour
    {
        public TMP_Text descriptionLabel;
        public TMP_InputField codeInput;
        public TMP_Text errorLabel;
        public TMP_Text statusLabel;
        public Button verifyButton;
        public Button resendButton;
        public TMP_Text resendLabel;
        public Button closeButton;

        private const float ResendSeconds = 60f;

        private string _contact;           // canonical form
        private Action _onVerified;
        private float _resendAt;
        private bool _busy;

        /// <summary>Opens the panel above `parent`'s other screens and sends the first code.</summary>
        public static VerifyContactPanel Show(Transform parent, string contact, Action onVerified)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/VerifyContactPanel");
            if (prefab == null)
            {
                Debug.LogWarning("[VerifyContactPanel] Missing prefab - the contact was not verified.");
                return null;
            }
            VerifyContactPanel panel = Instantiate(prefab, parent).GetComponent<VerifyContactPanel>();
            panel.Begin(contact, onVerified);
            return panel;
        }

        private void Awake()
        {
            if (verifyButton != null) verifyButton.onClick.AddListener(Verify);
            if (resendButton != null) resendButton.onClick.AddListener(Resend);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            Localization.LanguageChanged += RefreshTexts;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshTexts;
        }

        /// <summary>Starts verifying `contact` (any typed form) and sends the code.</summary>
        public void Begin(string contact, Action onVerified)
        {
            _contact = RecoveryContact.Normalize(contact) ?? "";
            _onVerified = onVerified;
            RefreshTexts();
            _ = SendCode();
        }

        private void Update()
        {
            RefreshResendButton();
        }

        private string Masked => RecoveryContact.Mask(_contact);

        private void RefreshTexts()
        {
            if (descriptionLabel != null) descriptionLabel.text = Localization.Format("account.verify_desc_fmt", Masked);
            RefreshResendButton();
        }

        private void RefreshResendButton()
        {
            if (resendButton == null) return;
            float wait = _resendAt - Time.unscaledTime;
            resendButton.interactable = wait <= 0f && !_busy;
            if (resendLabel != null)
            {
                string text = Localization.Get("account.forgot_resend");
                resendLabel.text = wait > 0f ? $"{text} ({Mathf.CeilToInt(wait)})" : text;
            }
        }

        private async void Resend()
        {
            if (_busy) return;
            await SendCode();
        }

        private async Task SendCode()
        {
            ShowError(null);
            SetBusy(true);
            RecoveryResult result = await AccountManager.RequestContactVerification(_contact);
            if (this == null) return;
            SetBusy(false);

            if (!result.ok)
            {
                // "too soon" means a code was sent a moment ago: it is still good, so keep waiting for it.
                if (result.error == "too_soon") _resendAt = Time.unscaledTime + Mathf.Max(1, result.retryAfter);
                ShowError(AccountManager.RecoveryErrorText(result));
                return;
            }

            _resendAt = Time.unscaledTime + ResendSeconds;
            ShowStatus(Localization.Format("account.verify_sent_fmt", Masked));
            if (codeInput != null) codeInput.Select();
        }

        private async void Verify()
        {
            if (_busy) return;
            string code = codeInput != null ? codeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                ShowError(Localization.Get("account.fill_all_fields"));
                return;
            }

            ShowError(null);
            SetBusy(true);
            RecoveryResult result = await AccountManager.VerifyContact(_contact, code);
            if (this == null) return;
            SetBusy(false);

            if (!result.ok)
            {
                ShowError(AccountManager.RecoveryErrorText(result));
                // A spent or expired code is no use any more: a new one can be asked for right away.
                if (result.error == "too_many_attempts" || result.error == "code_expired") _resendAt = 0f;
                return;
            }

            Action done = _onVerified;
            Close();
            done?.Invoke();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (verifyButton != null) verifyButton.interactable = !busy;
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
