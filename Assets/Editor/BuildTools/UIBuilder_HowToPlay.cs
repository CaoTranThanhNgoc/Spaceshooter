using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the How To Play overlay (Resources/Prefabs/UI/HowToPlayPanel.prefab) -
    /// accessible any time from Level Select, unlike MoveTutorialHint which only ever appears once
    /// during a brand-new player's first level. Same scrollable-rows pattern as Achievements:
    /// fixed-height rows baked at build time inside a ScrollRect viewport.</summary>
    public static class UIBuilder_HowToPlay
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color DarkText = new Color(0.12f, 0.16f, 0.2f, 1f);
        private static readonly Color MutedText = new Color(0.3f, 0.4f, 0.45f, 1f);

        private const float RowHeight = 130f;
        private const float RowSpacing = 16f;

        private struct Tip
        {
            public Sprite icon;
            public string titleKey;
            public string descKey;
            public string titleFallback;
            public string descFallback;
        }

        [MenuItem("Tools/Space Hawk/11. Build How To Play Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("HowToPlayPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "HOW TO PLAY", "howtoplay.title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(0, 700));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Tip[] tips =
            {
                new Tip { icon = UIFactory.LoadShip("Ship_01", "Ship_LVL_1"), titleKey = "howtoplay.controls_title", descKey = "howtoplay.controls_desc",
                    titleFallback = "DRAG TO MOVE", descFallback = "Drag anywhere on screen to move your ship. It fires automatically." },
                new Tip { icon = UIFactory.LoadShip("Boss/Boss_01", "Boss_Full"), titleKey = "howtoplay.objective_title", descKey = "howtoplay.objective_desc",
                    titleFallback = "DESTROY ENOUGH ENEMIES", descFallback = "Destroy at least the required number of enemies to clear a level. Letting too many escape ends in defeat." },
                new Tip { icon = UIFactory.LoadShip("Bonus_Items", "HP_Bonus"), titleKey = "howtoplay.powerups_title", descKey = "howtoplay.powerups_desc",
                    titleFallback = "COLLECT POWER-UPS", descFallback = "Destroyed enemies may drop power-ups - shields, rapid fire, bombs, and more." },
                new Tip { icon = UIFactory.LoadShip("Boss/Boss_03", "Boss_Full"), titleKey = "howtoplay.boss_title", descKey = "howtoplay.boss_desc",
                    titleFallback = "DEFEAT THE BOSS", descFallback = "Some levels end in a boss fight. The boss won't leave until it's destroyed - hold your ground!" },
                new Tip { icon = LoadUI("BateryIcon"), titleKey = "howtoplay.energy_title", descKey = "howtoplay.energy_desc",
                    titleFallback = "ENERGY", descFallback = "Each level costs Energy to play. It regenerates over time, so come back later if you run out." },
                new Tip { icon = LoadUI("CrystalIcon"), titleKey = "howtoplay.upgrade_title", descKey = "howtoplay.upgrade_desc",
                    titleFallback = "UPGRADE YOUR SHIP", descFallback = "Earn Crystals from levels and achievements to upgrade your ship or unlock new hulls in the Inventory." },
            };

            GameObject viewport = NewUI("Viewport", panel.transform);
            Stretch(RT(viewport), 0, 0, 50, 30);
            viewport.AddComponent<RectMask2D>();

            GameObject content = NewUI("Content", viewport.transform);
            float contentHeight = tips.Length * (RowHeight + RowSpacing);
            Anchor(RT(content), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, contentHeight));

            ScrollRect scrollRect = panel.AddComponent<ScrollRect>();
            scrollRect.content = RT(content);
            scrollRect.viewport = RT(viewport);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.scrollSensitivity = 28f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            for (int i = 0; i < tips.Length; i++)
            {
                float y = -i * (RowHeight + RowSpacing);
                BuildRow(content.transform, y, tips[i]);
            }

            HowToPlayPanel comp = root.AddComponent<HowToPlayPanel>();
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/HowToPlayPanel.prefab");
            Debug.Log("[UIBuilder_HowToPlay] How To Play panel built.");
        }

        private static void BuildRow(Transform parent, float y, Tip tip)
        {
            GameObject row = NewUI("Row", parent);
            Anchor(RT(row), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(-60, RowHeight));

            Image bg = row.AddComponent<Image>();
            bg.sprite = LoadUI("Achievement_Box");
            bg.type = Image.Type.Sliced;

            Image icon = CreateImage(row.transform, "Icon", tip.icon,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(84, 84));
            icon.preserveAspect = true;

            // Fixed width rather than stretched to the row's own (device-dependent) width - same
            // approach the achievement rows use for their own Title/Subtitle - comfortably
            // narrower than the row's minimum actual width (~1207 at the narrowest reference
            // aspect ratio this panel is ever built for) so it never gets clipped.
            CreateText(row.transform, "Title", tip.titleFallback, 22, TextAlignmentOptions.TopLeft, DarkText,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(126, -20), new Vector2(1000, 30), tip.titleKey);
            CreateText(row.transform, "Desc", tip.descFallback, 16, TextAlignmentOptions.TopLeft, MutedText,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(126, -54), new Vector2(1000, 68), tip.descKey);
        }
    }
}
