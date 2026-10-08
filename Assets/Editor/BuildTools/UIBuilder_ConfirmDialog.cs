using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the generic Yes/No confirmation overlay (Resources/Prefabs/UI/
    /// ConfirmDialog.prefab) - ConfirmDialog.Show fills in the title/message/callback at
    /// runtime, so this is built once and reused for any destructive confirmation later added.</summary>
    public static class UIBuilder_ConfirmDialog
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color DarkText = new Color(0.12f, 0.16f, 0.2f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);   // readable on the dark panel
        private static readonly Color DangerColor = new Color(0.9f, 0.3f, 0.25f, 1f);

        [MenuItem("Tools/Space Hawk/14. Build Confirm Dialog")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("ConfirmDialog", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            // Every other overlay in the game dims with the DarkBackground sprite, not a flat
            // translucent color - this one used a flat Color(0,0,0,0.7) instead, which wasn't
            // opaque enough to hide whatever panel opened it (e.g. PlayerProfilePanel), letting
            // both panels' text show through and overlap each other.
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            GameObject card = NewUI("Card", root.transform);
            Anchor(RT(card), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 340));
            Image cardImg = card.AddComponent<Image>();
            cardImg.sprite = LoadUI("BoxMenu");
            cardImg.type = Image.Type.Sliced;
            cardImg.raycastTarget = true;

            TMP_Text title = CreateText(card.transform, "Title", "ARE YOU SURE?", 28, TextAlignmentOptions.Center, DangerColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(620, 44));
            title.fontStyle = FontStyles.Bold;

            TMP_Text message = CreateText(card.transform, "Message", "", 20, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(600, 120)).Flexible(13f);

            Vector2 btnSize = new Vector2(240, 72);
            Button noBtn = CreateTextButton(card.transform, "NoButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "NO", 24, BodyLight, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-140, 36), btnSize, Image.Type.Sliced, "confirm.no");
            Button yesBtn = CreateTextButton(card.transform, "YesButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "YES", 24, DangerColor, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(140, 36), btnSize, Image.Type.Sliced, "confirm.yes");

            ConfirmDialog dialog = root.AddComponent<ConfirmDialog>();
            dialog.titleLabel = title;
            dialog.messageLabel = message;
            dialog.yesButton = yesBtn;
            dialog.noButton = noBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/ConfirmDialog.prefab");
            Debug.Log("[UIBuilder_ConfirmDialog] Confirm dialog built.");
        }
    }
}
