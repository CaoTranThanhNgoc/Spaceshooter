using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Adds or changes the e-mail kept on the signed-in account so a forgotten
    /// password can be reset - opened from PlayerProfilePanel. A code is sent to the new contact first
    /// (VerifyContactPanel); only a contact the player proved to own is saved.</summary>
    public class RecoveryContactPanel : MonoBehaviour
    {
        public TMP_Text currentLabel;
        public TMP_InputField contactInput;
        public TMP_Text errorLabel;
        public Button saveButton;
        public Button closeButton;

        private void Awake()
        {
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            SetError(null);
            Refresh();
            Localization.LanguageChanged += Refresh;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (currentLabel == null) return;
            string current = SaveManager.GetRecoveryContact();
            bool has = SaveManager.HasVerifiedRecoveryContact();
            currentLabel.gameObject.SetActive(has);
            if (has) currentLabel.text = Localization.Format("account.contact_current_fmt", RecoveryContact.Mask(current));
        }

        private void Save()
        {
            string contact = contactInput != null ? contactInput.text.Trim() : "";
            if (string.IsNullOrEmpty(contact))
            {
                SetError(Localization.Get("account.fill_all_fields"));
                return;
            }
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null)
            {
                SetError(Localization.Get("account.error_invalid_contact"));
                return;
            }

            SetError(null);
            // The contact is only attached once the player proved it is theirs with the code sent to it.
            VerifyContactPanel.Show(transform.parent != null ? transform.parent : transform, canonical, () =>
            {
                if (this != null) _ = Commit(canonical);
            });
        }

        private async System.Threading.Tasks.Task Commit(string canonical)
        {
            if (saveButton != null) saveButton.interactable = false;
            RecoveryResult result = await AccountManager.SetRecoveryContact(canonical);
            if (this == null) return;
            if (saveButton != null) saveButton.interactable = true;

            if (!result.ok)
            {
                SetError(AccountManager.RecoveryErrorText(result));
                return;
            }

            Transform toastParent = transform.parent != null ? transform.parent : transform;
            ToastUI.ShowToast(toastParent, Localization.Get("account.contact_saved"));
            if (result.transferred) ToastUI.ShowToast(toastParent, Localization.Get("account.contact_moved"));
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
