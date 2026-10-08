using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpaceHawk.UI
{
    /// <summary>Generic Yes/No confirmation modal - used for destructive, irreversible actions
    /// (right now just Delete Account) where a plain button tap isn't enough of a safety check.</summary>
    public class ConfirmDialog : MonoBehaviour
    {
        public TMP_Text titleLabel;
        public TMP_Text messageLabel;
        public Button yesButton;
        public Button noButton;

        private Action _onConfirm;

        private void Awake()
        {
            if (yesButton != null) yesButton.onClick.AddListener(OnYes);
            if (noButton != null) noButton.onClick.AddListener(Close);
        }

        public void Setup(string title, string message, Action onConfirm)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (messageLabel != null) messageLabel.text = message;
            _onConfirm = onConfirm;
        }

        /// <summary>Fails closed: if the prefab is missing, nothing happens rather than silently
        /// performing whatever destructive action was being confirmed.</summary>
        public static void Show(Transform parent, string title, string message, Action onConfirm)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/ConfirmDialog");
            if (prefab == null)
            {
                Debug.LogWarning("[ConfirmDialog] Missing ConfirmDialog prefab - action was not confirmed.");
                return;
            }
            GameObject go = Instantiate(prefab, parent);
            ConfirmDialog dialog = go.GetComponent<ConfirmDialog>();
            if (dialog != null) dialog.Setup(title, message, onConfirm);
        }

        private void OnYes()
        {
            _onConfirm?.Invoke();
            Close();
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
