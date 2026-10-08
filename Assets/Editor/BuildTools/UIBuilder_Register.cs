using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Create Account overlay (Resources/Prefabs/UI/RegisterPanel.prefab) -
    /// opened from PlayerProfilePanel's "CREATE ACCOUNT" button.</summary>
    public static class UIBuilder_Register
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);   // readable on the dark panel
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.25f, 1f);

        [MenuItem("Tools/Space Hawk/15. Build Register Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("RegisterPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "CREATE ACCOUNT", "account.register_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 780));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            // Running Y cursor (see UIBuilder_PlayerProfile for why: provably-positive gaps
            // instead of independently-guessed numbers that can silently touch or overlap).
            float y = -55;

            CreateText(panel.transform, "Description", "Create an account so you can recover your identity on another device.", 18, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 60), "account.register_desc").Flexible(12f);
            y -= 60 + 25; // Description height + gap -> UsernameInput top

            TMP_InputField usernameInput = CreateInputField(panel.transform, "UsernameInput", "Username",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 64), 20, "account.username_placeholder");
            y -= 64 + 4; // UsernameInput height + gap -> UsernameHint top

            // Unity's server-side username/password policies have no client-side hint of their
            // own - stating them up front avoids the player finding out only after a failed submit.
            CreateText(panel.transform, "UsernameHint", "3-20 characters: letters, numbers, . _ or -", 13, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 22), "account.username_hint").Flexible(10f);
            y -= 22 + 16; // UsernameHint height + gap -> PasswordInput top

            TMP_InputField passwordInput = CreateInputField(panel.transform, "PasswordInput", "Password",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 64), 30, "account.password_placeholder");
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            y -= 64 + 4; // PasswordInput height + gap -> PasswordHint top

            CreateText(panel.transform, "PasswordHint", "8-30 characters, with uppercase, lowercase and a number.", 13, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 22), "account.password_hint").Flexible(10f);
            y -= 22 + 16; // PasswordHint height + gap -> ConfirmPasswordInput top

            TMP_InputField confirmInput = CreateInputField(panel.transform, "ConfirmPasswordInput", "Confirm password",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 64), 30, "account.confirm_password_placeholder");
            confirmInput.contentType = TMP_InputField.ContentType.Password;
            y -= 64 + 16; // ConfirmPasswordInput height + gap -> ContactInput top

            // Where a code can be sent if the password is ever forgotten - an e-mail address, required.
            TMP_InputField contactInput = CreateInputField(panel.transform, "ContactInput", "Email",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 64), 60, "account.contact_placeholder");
            contactInput.contentType = TMP_InputField.ContentType.EmailAddress;   // the mail keyboard on phones
            y -= 64 + 4; // ContactInput height + gap -> ContactHint top

            CreateText(panel.transform, "ContactHint", "Only used to recover your password if you forget it.", 13, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 22), "account.contact_hint").Flexible(10f);
            y -= 22 + 16; // ContactHint height + gap -> ErrorLabel top

            // Height 60, not 50 - the longest mapped error strings (e.g. the "provider not
            // enabled" message) wrap to 2 lines at this width/font size, and 50px cut the second
            // line into the confirm button below it.
            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "", 18, TextAlignmentOptions.Center, ErrorColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 60)).Flexible(12f);
            errorLabel.gameObject.SetActive(false);
            y -= 60 + 16; // ErrorLabel height + gap -> ConfirmButton top

            Button confirmBtn = CreateTextButton(panel.transform, "ConfirmButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "CREATE ACCOUNT", 26, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(400, 80), Image.Type.Sliced, "account.register");

            RegisterPanel comp = root.AddComponent<RegisterPanel>();
            comp.usernameInput = usernameInput;
            comp.passwordInput = passwordInput;
            comp.confirmPasswordInput = confirmInput;
            comp.contactInput = contactInput;
            comp.errorLabel = errorLabel;
            comp.confirmButton = confirmBtn;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/RegisterPanel.prefab");
            Debug.Log("[UIBuilder_Register] Register panel built.");
        }
    }
}
