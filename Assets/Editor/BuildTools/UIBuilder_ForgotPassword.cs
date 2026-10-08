using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the "Forgot password?" overlay (Resources/Prefabs/UI/ForgotPasswordPanel.prefab):
    /// step 1 asks for the username and the e-mail saved on the account, step 2 for the code that
    /// was sent and a new password. Both steps share one panel; ForgotPasswordPanel swaps them.</summary>
    public static class UIBuilder_ForgotPassword
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.4f, 0.35f, 1f);
        private static readonly Color StatusColor = new Color(0.45f, 0.9f, 0.7f, 1f);
        private static readonly Color LinkColor = new Color(0.63f, 0.93f, 0.93f, 1f);

        [MenuItem("Tools/Space Hawk/20. Build Forgot Password Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("ForgotPasswordPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "FORGOT PASSWORD?", "account.forgot_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 640));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Vector2 top = new Vector2(0.5f, 1);
            Vector2 topLeft = new Vector2(0, 1);

            TMP_Text description = CreateText(panel.transform, "Description", "", 18, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, -42), new Vector2(660, 66)).Flexible(12f);
            description.enableAutoSizing = true;
            description.fontSizeMin = 13;
            description.fontSizeMax = 18;

            // ---- step 1: username + contact
            GameObject step1 = NewUI("Step1", panel.transform);
            Anchor(RT(step1), top, top, top, new Vector2(0, -122), new Vector2(660, 260));

            TMP_InputField usernameInput = CreateInputField(step1.transform, "UsernameInput", "Username",
                top, top, top, new Vector2(0, 0), new Vector2(600, 64), 20, "account.username_placeholder");
            TMP_InputField contactInput = CreateInputField(step1.transform, "ContactInput", "Email",
                top, top, top, new Vector2(0, -80), new Vector2(600, 64), 60, "account.contact_placeholder");
            contactInput.contentType = TMP_InputField.ContentType.EmailAddress;   // the mail keyboard on phones
            TMP_Text contactHint = CreateText(step1.transform, "ContactHint", "Only used to recover your password if you forget it.", 14, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, -150), new Vector2(600, 24), "account.contact_hint").Flexible(10f);
            contactHint.enableAutoSizing = true;
            contactHint.fontSizeMin = 11;
            contactHint.fontSizeMax = 14;
            TMP_Text noContact = CreateText(step1.transform, "NoContactHint", "Never saved a contact?", 15, TextAlignmentOptions.Center, LinkColor,
                top, top, top, new Vector2(0, -182), new Vector2(600, 56), "account.forgot_no_contact").Flexible(11f);
            noContact.enableAutoSizing = true;
            noContact.fontSizeMin = 11;
            noContact.fontSizeMax = 15;

            // ---- step 2: code + new password
            GameObject step2 = NewUI("Step2", panel.transform);
            Anchor(RT(step2), top, top, top, new Vector2(0, -122), new Vector2(660, 260));

            TMP_InputField codeInput = CreateInputField(step2.transform, "CodeInput", "6-digit code",
                top, top, top, new Vector2(0, 0), new Vector2(600, 64), 8, "account.code_placeholder");
            codeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
            TMP_InputField newInput = CreateInputField(step2.transform, "NewPasswordInput", "New password",
                top, top, top, new Vector2(0, -80), new Vector2(600, 64), 30, "account.new_password_placeholder");
            newInput.contentType = TMP_InputField.ContentType.Password;
            TMP_Text passwordHint = CreateText(step2.transform, "PasswordHint", "8-30 characters, with uppercase, lowercase, a number and a symbol.", 13, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, -148), new Vector2(600, 22), "account.password_hint").Flexible(10f);
            passwordHint.enableAutoSizing = true;
            passwordHint.fontSizeMin = 10;
            passwordHint.fontSizeMax = 13;
            TMP_InputField confirmInput = CreateInputField(step2.transform, "ConfirmPasswordInput", "Confirm new password",
                top, top, top, new Vector2(0, -176), new Vector2(600, 64), 30, "account.confirm_new_password_placeholder");
            confirmInput.contentType = TMP_InputField.ContentType.Password;
            step2.SetActive(false);

            // ---- messages (an error or a status, never both) and the buttons
            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "", 18, TextAlignmentOptions.Center, ErrorColor,
                top, top, top, new Vector2(0, -392), new Vector2(660, 52)).Flexible(12f);
            errorLabel.enableAutoSizing = true;
            errorLabel.fontSizeMin = 12;
            errorLabel.fontSizeMax = 18;
            errorLabel.gameObject.SetActive(false);
            TMP_Text statusLabel = CreateText(panel.transform, "StatusLabel", "", 18, TextAlignmentOptions.Center, StatusColor,
                top, top, top, new Vector2(0, -392), new Vector2(660, 52)).Flexible(12f);
            statusLabel.enableAutoSizing = true;
            statusLabel.fontSizeMin = 12;
            statusLabel.fontSizeMax = 18;
            statusLabel.gameObject.SetActive(false);

            Button actionBtn = CreateTextButton(panel.transform, "ActionButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "SEND CODE", 26, BodyLight, top, top, top, new Vector2(0, -452), new Vector2(460, 80), Image.Type.Sliced, "account.forgot_send");
            TMP_Text actionLabel = actionBtn.GetComponentInChildren<TMP_Text>();
            actionLabel.enableAutoSizing = true;
            actionLabel.fontSizeMin = 16;
            actionLabel.fontSizeMax = 26;
            // the panel script sets the caption per step; it must not be put back by a language change
            LocalizedText actionLoc = actionLabel.GetComponent<LocalizedText>();
            if (actionLoc != null) Object.DestroyImmediate(actionLoc);

            Button resendBtn = CreateButton(panel.transform, "ResendButton", null, null, null,
                top, top, top, new Vector2(0, -544), new Vector2(460, 40));
            resendBtn.GetComponent<Image>().color = Color.clear;
            TMP_Text resendLabel = CreateText(resendBtn.transform, "Label", "SEND A NEW CODE", 17, TextAlignmentOptions.Center, LinkColor,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            resendLabel.fontStyle = FontStyles.Underline;
            resendBtn.gameObject.SetActive(false);

            ForgotPasswordPanel comp = root.AddComponent<ForgotPasswordPanel>();
            comp.descriptionLabel = description;
            comp.errorLabel = errorLabel;
            comp.statusLabel = statusLabel;
            comp.closeButton = closeBtn;
            comp.step1Root = step1;
            comp.usernameInput = usernameInput;
            comp.contactInput = contactInput;
            comp.step2Root = step2;
            comp.codeInput = codeInput;
            comp.newPasswordInput = newInput;
            comp.confirmPasswordInput = confirmInput;
            comp.resendButton = resendBtn;
            comp.resendLabel = resendLabel;
            comp.actionButton = actionBtn;
            comp.actionLabel = actionLabel;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/ForgotPasswordPanel.prefab");
            Debug.Log("[UIBuilder_ForgotPassword] Forgot password panel built.");
        }
    }
}
