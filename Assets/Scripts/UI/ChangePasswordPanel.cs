using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Changes the password of the signed-in account - opened from PlayerProfilePanel. It
    /// needs the current password: the Authentication service has no other way to change one, and
    /// no e-mail reset (see LoginPanel's "Forgot password?").</summary>
    public class ChangePasswordPanel : MonoBehaviour
    {
        public TMP_InputField currentInput;
        public TMP_InputField newInput;
        public TMP_InputField confirmInput;
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

        private async void Submit()
        {
            string current = currentInput != null ? currentInput.text : "";
            string next = newInput != null ? newInput.text : "";
            string confirm = confirmInput != null ? confirmInput.text : "";

            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(next) || string.IsNullOrEmpty(confirm))
            {
                SetError(Localization.Get("account.fill_all_fields"));
                return;
            }
            if (next != confirm)
            {
                SetError(Localization.Get("account.passwords_dont_match"));
                return;
            }
            if (!AccountManager.IsPasswordValid(next))
            {
                SetError(Localization.Get("account.error_weak_password"));
                return;
            }
            if (next == current)
            {
                SetError(Localization.Get("account.same_password"));
                return;
            }

            SetError(null);
            if (confirmButton != null) confirmButton.interactable = false;
            (bool success, string error) = await AccountManager.ChangePassword(current, next);
            if (this == null) return;
            if (confirmButton != null) confirmButton.interactable = true;

            if (success)
            {
                ToastUI.ShowToast(transform.parent != null ? transform.parent : transform, Localization.Get("account.change_success"));
                if (currentInput != null) currentInput.interactable = false;
                if (newInput != null) newInput.interactable = false;
                if (confirmInput != null) confirmInput.interactable = false;
                await Task.Delay(1500);
                if (this == null) return;
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
