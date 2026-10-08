using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Attaches username/password to the player's current (anonymous) identity - opened
    /// from PlayerProfilePanel's "CREATE ACCOUNT" button.</summary>
    public class RegisterPanel : MonoBehaviour
    {
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        public TMP_InputField confirmPasswordInput;
        [Tooltip("E-mail address kept so a forgotten password can be reset.")]
        public TMP_InputField contactInput;
        public TMP_Text errorLabel;
        public Button confirmButton;
        public Button closeButton;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Submit);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            SetError(null);
        }

        // A verified contact stays good on the server for a while, so a retry after a taken username does
        // not make the player type another code.
        private const float VerifiedValidSeconds = 14f * 60f;
        private string _verifiedContact;
        private float _verifiedAt;

        private void Submit()
        {
            string username = usernameInput != null ? usernameInput.text.Trim() : "";
            string password = passwordInput != null ? passwordInput.text : "";
            string confirm = confirmPasswordInput != null ? confirmPasswordInput.text : "";
            string contact = contactInput != null ? contactInput.text.Trim() : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirm) || string.IsNullOrEmpty(contact))
            {
                SetError(Localization.Get("account.fill_all_fields"));
                return;
            }
            if (!AccountManager.IsUsernameValid(username))
            {
                SetError(Localization.Get("account.error_invalid_username"));
                return;
            }
            if (password != confirm)
            {
                SetError(Localization.Get("account.passwords_dont_match"));
                return;
            }
            if (!AccountManager.IsPasswordValid(password))
            {
                SetError(Localization.Get("account.error_weak_password"));
                return;
            }

            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null)
            {
                SetError(Localization.Get("account.error_invalid_contact"));
                return;
            }

            SetError(null);
            bool verified = canonical == _verifiedContact && Time.unscaledTime - _verifiedAt < VerifiedValidSeconds;
            if (verified)
            {
                _ = CreateAccount(username, password, canonical);
                return;
            }

            // First prove the contact is the player's: a code goes to it and has to be typed back.
            VerifyContactPanel.Show(transform.parent != null ? transform.parent : transform, canonical, () =>
            {
                if (this == null) return;
                _verifiedContact = canonical;
                _verifiedAt = Time.unscaledTime;
                _ = CreateAccount(username, password, canonical);
            });
        }

        private async Task CreateAccount(string username, string password, string contact)
        {
            if (confirmButton != null) confirmButton.interactable = false;
            AccountManager.RegisterResult result = await AccountManager.Register(username, password, contact);
            if (this == null) return;
            if (confirmButton != null) confirmButton.interactable = true;

            if (!result.success)
            {
                SetError(result.error);
                return;
            }

            // Toasts are parented to transform.parent (they survive this panel closing), not to
            // transform (this panel) - they used to be destroyed along with it the instant Close()
            // ran, cutting the message off before anyone could read it.
            Transform toastParent = transform.parent != null ? transform.parent : transform;
            ToastUI.ShowToast(toastParent, Localization.Get(result.contactSaved ? "account.register_success" : "account.register_success_nocontact"));
            if (result.contactMoved) ToastUI.ShowToast(toastParent, Localization.Get("account.contact_moved"));
            if (usernameInput != null) usernameInput.interactable = false;
            if (passwordInput != null) passwordInput.interactable = false;
            if (confirmPasswordInput != null) confirmPasswordInput.interactable = false;
            if (contactInput != null) contactInput.interactable = false;
            await Task.Delay(2000);
            if (this == null) return; // closed some other way while waiting
            Close();
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
