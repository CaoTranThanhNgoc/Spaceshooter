using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Sign In overlay (Resources/Prefabs/UI/LoginPanel.prefab) - opened from
    /// PlayerProfilePanel's "SIGN IN" button, to restore an already-registered account. Besides
    /// the two fields it carries the helpers for a player who forgot something: the usernames
    /// already used on this device as tap-to-fill chips, a SHOW button for the password and a
    /// "Forgot password?" link.</summary>
    public static class UIBuilder_Login
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);   // readable on the dark panel
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.25f, 1f);
        private static readonly Color LinkColor = new Color(0.63f, 0.93f, 0.93f, 1f);
        private const int RecentSlots = 3;

        [MenuItem("Tools/Space Hawk/16. Build Login Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("LoginPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "SIGN IN", "account.signin_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 580));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Vector2 top = new Vector2(0.5f, 1);
            Vector2 centre = new Vector2(0.5f, 0.5f);

            // Running Y cursor, as in the other account panels: gaps that are positive by construction.
            float y = -45;
            CreateText(panel.transform, "Description", "Sign in to restore an existing account.", 18, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, y), new Vector2(660, 40), "account.signin_desc").Flexible(12f);
            y -= 40 + 16;

            TMP_InputField usernameInput = CreateInputField(panel.transform, "UsernameInput", "Username",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 20, "account.username_placeholder");
            y -= 64 + 8;

            // Accounts already used on this device - for a player who forgot the exact username.
            GameObject recentRow = NewUI("RecentRow", panel.transform);
            Anchor(RT(recentRow), top, top, top, new Vector2(0, y), new Vector2(600, 62));
            CreateText(recentRow.transform, "RecentLabel", "Used on this device - tap to fill:", 14, TextAlignmentOptions.MidlineLeft, LinkColor,
                top, top, top, new Vector2(0, 0), new Vector2(600, 22), "account.recent_label");

            Button[] recentButtons = new Button[RecentSlots];
            TMP_Text[] recentLabels = new TMP_Text[RecentSlots];
            float chipWidth = 190f, chipGap = 15f;
            float chipsStart = -(RecentSlots * chipWidth + (RecentSlots - 1) * chipGap) / 2f + chipWidth / 2f;
            for (int i = 0; i < RecentSlots; i++)
            {
                Button chip = CreateButton(recentRow.transform, $"RecentChip{i}", LoadUI("BgDropdownContent"), null, null,
                    top, top, centre, new Vector2(chipsStart + i * (chipWidth + chipGap), -42), new Vector2(chipWidth, 36), Image.Type.Sliced);
                recentButtons[i] = chip;
                TMP_Text label = CreateText(chip.transform, "Label", "username", 17, TextAlignmentOptions.Center, BodyLight,
                    Vector2.zero, Vector2.one, centre, Vector2.zero, Vector2.zero);
                label.enableAutoSizing = true;
                label.fontSizeMin = 11;
                label.fontSizeMax = 17;
                label.margin = new Vector4(8, 0, 8, 0);
                recentLabels[i] = label;
            }
            y -= 62 + 10;

            TMP_InputField passwordInput = CreateInputField(panel.transform, "PasswordInput", "Password",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 30, "account.password_placeholder");
            passwordInput.contentType = TMP_InputField.ContentType.Password;

            // SHOW / HIDE sits inside the right end of the password field.
            Button showBtn = CreateTextButton(panel.transform, "ShowPasswordButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "SHOW", 16, BodyLight, top, top, centre, new Vector2(248, y - 32), new Vector2(92, 42), Image.Type.Sliced, "account.show_password");
            y -= 64 + 4;

            Button forgotBtn = CreateButton(panel.transform, "ForgotPasswordButton", null, null, null,
                top, top, new Vector2(1, 1), new Vector2(300, y), new Vector2(300, 32));
            forgotBtn.GetComponent<Image>().color = Color.clear;
            TMP_Text forgotLabel = CreateText(forgotBtn.transform, "Label", "Forgot password?", 17, TextAlignmentOptions.MidlineRight, LinkColor,
                Vector2.zero, Vector2.one, centre, Vector2.zero, Vector2.zero, "account.forgot_password");
            forgotLabel.fontStyle = FontStyles.Underline;
            y -= 32 + 6;

            // Height 60, not 50 - the longest mapped error strings wrap to 2 lines at this
            // width/font size, and 50px cut the second line into the confirm button below it.
            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "", 18, TextAlignmentOptions.Center, ErrorColor,
                top, top, top, new Vector2(0, y), new Vector2(660, 56)).Flexible(12f);
            errorLabel.gameObject.SetActive(false);
            y -= 56 + 8;

            Button confirmBtn = CreateTextButton(panel.transform, "ConfirmButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "SIGN IN", 26, BodyLight, top, top, top, new Vector2(0, y), new Vector2(400, 80), Image.Type.Sliced, "account.signin");

            LoginPanel comp = root.AddComponent<LoginPanel>();
            comp.usernameInput = usernameInput;
            comp.passwordInput = passwordInput;
            comp.errorLabel = errorLabel;
            comp.confirmButton = confirmBtn;
            comp.closeButton = closeBtn;
            comp.recentRow = recentRow;
            comp.recentButtons = recentButtons;
            comp.recentLabels = recentLabels;
            comp.showPasswordButton = showBtn;
            comp.showPasswordLabel = showBtn.GetComponentInChildren<TMP_Text>();
            comp.forgotPasswordButton = forgotBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/LoginPanel.prefab");
            Debug.Log("[UIBuilder_Login] Login panel built.");
        }
    }
}
