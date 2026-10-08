using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Energy Refill dialog (Resources/Prefabs/UI/EnergyRefillDialog.prefab),
    /// shown whenever the player tries to start a level without enough energy.</summary>
    public static class UIBuilder_EnergyDialog
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.83f, 0.2f, 1f);

        [MenuItem("Tools/Space Hawk/15. Build Energy Dialog")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("EnergyRefillDialog", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            // Title goes in the pill DarkBackground.png draws at the top, like every other overlay -
            // left empty it reads as a missing label.
            UIBuilder_Overlays.BuildDimTitle(root.transform, "OUT OF ENERGY", "energy.title");

            GameObject card = NewUI("Card", root.transform);
            Anchor(RT(card), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 360));
            Image cardImg = card.AddComponent<Image>();
            cardImg.sprite = LoadUI("BoxMenu");
            cardImg.type = Image.Type.Sliced;
            cardImg.raycastTarget = true;

            CreateText(card.transform, "Message", "Energy comes back 1 point every 5 minutes. Refill now to keep playing.", 22, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(680, 76), "energy.message").Flexible(13f);

            Button refill = CreateTextButton(card.transform, "RefillButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "REFILL ALL: 30 CRYSTAL", 26, Yellow, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -142), new Vector2(560, 80));
            Button close = CreateTextButton(card.transform, "CloseButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), LoadUI("Disable_Btn"),
                "CLOSE", 24, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -246), new Vector2(260, 76), Image.Type.Sliced, "energy.close");

            EnergyRefillDialog dialog = root.AddComponent<EnergyRefillDialog>();
            dialog.refillButton = refill;
            dialog.refillLabel = refill.GetComponentInChildren<TMP_Text>();
            dialog.closeButton = close;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/EnergyRefillDialog.prefab");
            Debug.Log("[UIBuilder_EnergyDialog] Energy dialog built.");
        }
    }
}
