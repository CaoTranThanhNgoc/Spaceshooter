using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Verify Contact overlay (Resources/Prefabs/UI/VerifyContactPanel.prefab): the
    /// 6-digit code that was sent to the player's e-mail is typed back here, proving the
    /// contact is theirs. Shown by RegisterPanel and RecoveryContactPanel.</summary>
    public static class UIBuilder_VerifyContact
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.4f, 0.35f, 1f);
        private static readonly Color StatusColor = new Color(0.45f, 0.9f, 0.7f, 1f);
        private static readonly Color LinkColor = new Color(0.63f, 0.93f, 0.93f, 1f);

        [MenuItem("Tools/Space Hawk/22. Build Verify Contact Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("VerifyContactPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "VERIFY CONTACT", "account.verify_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 500));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Vector2 top = new Vector2(0.5f, 1);
            float y = -45;

            TMP_Text description = CreateText(panel.transform, "Description", "We sent a 6-digit code. Enter it to confirm this is yours.", 18, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, y), new Vector2(660, 76)).Flexible(12f);
            y -= 76 + 14;

            TMP_InputField codeInput = CreateInputField(panel.transform, "CodeInput", "6-digit code",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 8, "account.code_placeholder");
            codeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
            y -= 64 + 12;

            // An error or a status, never both: they share one spot.
            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "", 18, TextAlignmentOptions.Center, ErrorColor,
                top, top, top, new Vector2(0, y), new Vector2(660, 56)).Flexible(12f);
            errorLabel.gameObject.SetActive(false);
            TMP_Text statusLabel = CreateText(panel.transform, "StatusLabel", "", 18, TextAlignmentOptions.Center, StatusColor,
                top, top, top, new Vector2(0, y), new Vector2(660, 56)).Flexible(12f);
            statusLabel.gameObject.SetActive(false);
            y -= 56 + 10;

            Button verifyBtn = CreateTextButton(panel.transform, "VerifyButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "VERIFY", 26, BodyLight, top, top, top, new Vector2(0, y), new Vector2(460, 80), Image.Type.Sliced, "account.verify_button");
            TMP_Text verifyLabel = verifyBtn.GetComponentInChildren<TMP_Text>();
            verifyLabel.enableAutoSizing = true;
            verifyLabel.fontSizeMin = 16;
            verifyLabel.fontSizeMax = 26;
            y -= 80 + 10;

            Button resendBtn = CreateButton(panel.transform, "ResendButton", null, null, null,
                top, top, top, new Vector2(0, y), new Vector2(460, 40));
            resendBtn.GetComponent<Image>().color = Color.clear;
            TMP_Text resendLabel = CreateText(resendBtn.transform, "Label", "SEND A NEW CODE", 17, TextAlignmentOptions.Center, LinkColor,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            resendLabel.fontStyle = FontStyles.Underline;
            // the panel script sets its caption (with the countdown); a language change must not reset it
            LocalizedText resendLoc = resendLabel.GetComponent<LocalizedText>();
            if (resendLoc != null) Object.DestroyImmediate(resendLoc);

            VerifyContactPanel comp = root.AddComponent<VerifyContactPanel>();
            comp.descriptionLabel = description;
            comp.codeInput = codeInput;
            comp.errorLabel = errorLabel;
            comp.statusLabel = statusLabel;
            comp.verifyButton = verifyBtn;
            comp.resendButton = resendBtn;
            comp.resendLabel = resendLabel;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/VerifyContactPanel.prefab");
            Debug.Log("[UIBuilder_VerifyContact] Verify contact panel built.");
        }
    }
}
