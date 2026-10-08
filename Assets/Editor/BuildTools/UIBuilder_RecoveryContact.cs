using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Recovery Contact overlay (Resources/Prefabs/UI/RecoveryContactPanel.prefab) -
    /// opened from PlayerProfilePanel to add or change the e-mail kept for password recovery.</summary>
    public static class UIBuilder_RecoveryContact
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.4f, 0.35f, 1f);
        private static readonly Color LinkColor = new Color(0.63f, 0.93f, 0.93f, 1f);

        [MenuItem("Tools/Space Hawk/21. Build Recovery Contact Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("RecoveryContactPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "RECOVERY CONTACT", "account.contact_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 470));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Vector2 top = new Vector2(0.5f, 1);
            float y = -50;

            CreateText(panel.transform, "Description", "Add an email so a forgotten password can be reset.", 18, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, y), new Vector2(660, 56), "account.contact_desc").Flexible(12f);
            y -= 56 + 8;

            TMP_Text current = CreateText(panel.transform, "CurrentLabel", "Current: pi***@example.com", 18, TextAlignmentOptions.Center, LinkColor,
                top, top, top, new Vector2(0, y), new Vector2(660, 30));
            y -= 30 + 12;

            TMP_InputField input = CreateInputField(panel.transform, "ContactInput", "Email",
                top, top, top, new Vector2(0, y), new Vector2(600, 64), 60, "account.contact_placeholder");
            input.contentType = TMP_InputField.ContentType.EmailAddress;   // the mail keyboard on phones
            y -= 64 + 4;

            TMP_Text hint = CreateText(panel.transform, "ContactHint", "Only used to recover your password if you forget it.", 14, TextAlignmentOptions.Center, MutedText,
                top, top, top, new Vector2(0, y), new Vector2(600, 24), "account.contact_hint").Flexible(10f);
            y -= 24 + 12;

            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "", 18, TextAlignmentOptions.Center, ErrorColor,
                top, top, top, new Vector2(0, y), new Vector2(660, 52)).Flexible(12f);
            errorLabel.enableAutoSizing = true;
            errorLabel.fontSizeMin = 12;
            errorLabel.fontSizeMax = 18;
            errorLabel.gameObject.SetActive(false);
            y -= 52 + 8;

            Button saveBtn = CreateTextButton(panel.transform, "SaveButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "SAVE", 26, BodyLight, top, top, top, new Vector2(0, y), new Vector2(400, 80), Image.Type.Sliced, "account.contact_save");

            RecoveryContactPanel comp = root.AddComponent<RecoveryContactPanel>();
            comp.currentLabel = current;
            comp.contactInput = input;
            comp.errorLabel = errorLabel;
            comp.saveButton = saveBtn;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/RecoveryContactPanel.prefab");
            Debug.Log("[UIBuilder_RecoveryContact] Recovery contact panel built.");
        }
    }
}
