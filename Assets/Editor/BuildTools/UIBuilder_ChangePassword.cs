using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Change Password overlay (Resources/Prefabs/UI/ChangePasswordPanel.prefab)
    /// - opened from PlayerProfilePanel's "CHANGE PASSWORD" button while signed in.</summary>
    public static class UIBuilder_ChangePassword
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);   // readable on the dark panel
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.25f, 1f);

        [MenuItem("Tools/Space Hawk/19. Build Change Password Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("ChangePasswordPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "CHANGE PASSWORD", "account.change_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 640));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Vector2 top = new Vector2(0.5f, 1);
            float y = -55;

            CreateText(panel.transform, "Description", "Enter your current password, then choose a new one.", 18, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, y), new Vector2(660, 40), "account.change_desc").Flexible(12f);
            y -= 40 + 22;

            TMP_InputField currentInput = CreateInputField(panel.transform, "CurrentPasswordInput", "Current password",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 30, "account.current_password_placeholder");
            currentInput.contentType = TMP_InputField.ContentType.Password;
            y -= 64 + 24;

            TMP_InputField newInput = CreateInputField(panel.transform, "NewPasswordInput", "New password",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 30, "account.new_password_placeholder");
            newInput.contentType = TMP_InputField.ContentType.Password;
            y -= 64 + 4;

            CreateText(panel.transform, "PasswordHint", "8-30 characters, with uppercase, lowercase and a number.", 13, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, y), new Vector2(600, 22), "account.password_hint").Flexible(10f);
            y -= 22 + 16;

            TMP_InputField confirmInput = CreateInputField(panel.transform, "ConfirmNewPasswordInput", "Confirm new password",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 30, "account.confirm_new_password_placeholder");
            confirmInput.contentType = TMP_InputField.ContentType.Password;
            y -= 64 + 16;

            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "", 18, TextAlignmentOptions.Center, ErrorColor,
                top, top, top, new Vector2(0, y), new Vector2(660, 56)).Flexible(12f);
            errorLabel.gameObject.SetActive(false);
            y -= 56 + 12;

            Button confirmBtn = CreateTextButton(panel.transform, "ConfirmButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "CHANGE PASSWORD", 26, BodyLight, top, top, top, new Vector2(0, y), new Vector2(460, 80), Image.Type.Sliced, "account.change_password");
            TMP_Text confirmLabel = confirmBtn.GetComponentInChildren<TMP_Text>();
            confirmLabel.enableAutoSizing = true;
            confirmLabel.fontSizeMin = 16;
            confirmLabel.fontSizeMax = 26;

            ChangePasswordPanel comp = root.AddComponent<ChangePasswordPanel>();
            comp.currentInput = currentInput;
            comp.newInput = newInput;
            comp.confirmInput = confirmInput;
            comp.errorLabel = errorLabel;
            comp.confirmButton = confirmBtn;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/ChangePasswordPanel.prefab");
            Debug.Log("[UIBuilder_ChangePassword] Change password panel built.");
        }
    }
}
