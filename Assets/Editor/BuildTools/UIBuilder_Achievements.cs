using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using SpaceHawk.Data;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Achievements overlay (Resources/Prefabs/UI/AchievementsPanel.prefab).
    /// Rows are pre-built fixed-height slots inside a scrolling viewport (see the ScrollRect
    /// below) - MaxRows just needs to be >= AchievementContentBuilder's actual achievement count,
    /// since AchievementsPanel only fills min(rows.Length, database.achievements.Length) of them.</summary>
    public static class UIBuilder_Achievements
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color TitleTeal = new Color(0.63f, 0.93f, 0.93f, 1f);
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        // The row art (Achievement_Box / statusBox) is a translucent light wash over the dark panel,
        // not an opaque light card - so the text on it has to be light too. It used to be dark
        // grey, which was barely readable against the panel in the real game.
        private static readonly Color RowText = new Color(0.9f, 0.97f, 0.98f, 1f);
        private static readonly Color RowSubtleText = new Color(0.62f, 0.85f, 0.88f, 1f);

        private const float RowHeight = 118f;
        private const float RowSpacing = 16f;
        private const int MaxRows = 16;

        // The Progress/Reward/Status cluster was originally hand-placed in pixels for a row at
        // this width (the narrow ~4:3 tablet baseline: 0.88 * 1440 canvas width - 60 margin).
        // Converting each of those pixel offsets to a fraction of this reference width, and
        // reading them back as fractional anchors, keeps their spacing proportional to the row's
        // actual width instead of staying glued to the left while a wide phone row balloons the
        // gap in front of the (right-anchored) status button.
        private const float ReferenceRowWidth = 1207.2f;
        private static float Frac(float px) => px / ReferenceRowWidth;

        [MenuItem("Tools/Space Hawk/8. Build Achievements Panel")]
        public static void Build()
        {
            Build(AssetDatabase.LoadAssetAtPath<AchievementDatabase>("Assets/Resources/Data/AchievementDatabase.asset"));
        }

        public static void Build(AchievementDatabase database)
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("AchievementsPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "ACHIEVEMENTS", "achievements.title");

            // Stretched by width (% of screen) so it never overflows on narrower aspect ratios;
            // height stays fixed since the canvas always matches height 1:1.
            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(0, 700));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            // Rows scroll inside a fixed-height viewport rather than the panel being sized to fit
            // all of them - keeps rows a comfortable fixed height regardless of how many
            // achievements exist, and never lets the list spill past the panel's own border the
            // way a purely fixed panel height would once the row count changes.
            GameObject viewport = NewUI("Viewport", panel.transform);
            Stretch(RT(viewport), 0, 0, 50, 30);
            viewport.AddComponent<RectMask2D>();

            GameObject content = NewUI("Content", viewport.transform);
            float contentHeight = MaxRows * (RowHeight + RowSpacing);
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

            AchievementRowView[] rows = new AchievementRowView[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                float y = -i * (RowHeight + RowSpacing);
                rows[i] = BuildRow(content.transform, y);
            }

            AchievementsPanel comp = root.AddComponent<AchievementsPanel>();
            comp.database = database;
            comp.rows = rows;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/AchievementsPanel.prefab");
            Debug.Log("[UIBuilder_Achievements] Achievements panel built.");
        }

        /// <summary>One row of the achievement-style list - also reused by the Missions panel, which
        /// is why it is public and takes the row height/spacing from the caller's own layout.</summary>
        public static AchievementRowView BuildRow(Transform parent, float y, float height = RowHeight)
        {
            GameObject row = NewUI("Row", parent);
            // Stretched by width (fixed 60px total margin), fixed height, stacked from the top
            // by explicit Y - no ScrollRect/LayoutGroup involved.
            Anchor(RT(row), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(-60, height));

            Image bg = row.AddComponent<Image>();
            bg.sprite = LoadUI("Achievement_Box");
            bg.type = Image.Type.Sliced;

            // Every element below is centered as a group around the row's own vertical middle
            // (anchor y=0.5), each pair (label above, graphic below) split symmetrically around 0.
            CreateImage(row.transform, "Dot", LoadUI("Achievement_BlueDot"),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 16), Vector2.zero);

            TMP_Text title = CreateText(row.transform, "Title", "DESTROY ENEMY", 22, TextAlignmentOptions.MidlineLeft, RowText,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(58, 15), new Vector2(300, 30));
            TMP_Text subtitle = CreateText(row.transform, "Subtitle", "DESTROY 10 ENEMY", 15, TextAlignmentOptions.MidlineLeft, RowSubtleText,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(58, -15), new Vector2(300, 22));

            TMP_Text progressLabel = CreateText(row.transform, "ProgressLabel", "0 / 10", 16, TextAlignmentOptions.Center, RowText,
                new Vector2(Frac(400), 0.5f), new Vector2(Frac(400), 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(180, 22));
            CreateImage(row.transform, "ProgressBarBg", LoadUI("Achievement_ProgressBar"),
                new Vector2(Frac(400), 0.5f), new Vector2(Frac(400), 0.5f), new Vector2(0, 0.5f), new Vector2(0, -14), new Vector2(201, 39));
            Image progressFill = CreateImage(row.transform, "ProgressBarFill", null,
                new Vector2(Frac(410), 0.5f), new Vector2(Frac(410), 0.5f), new Vector2(0, 0.5f), new Vector2(0, -14), new Vector2(181, 25));
            progressFill.color = new Color(0.35f, 0.85f, 0.85f, 1f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            CreateImage(row.transform, "RewardIcon", LoadUI("Achievement_CrystalICon"),
                new Vector2(Frac(650), 0.5f), new Vector2(Frac(650), 0.5f), new Vector2(0, 0.5f), new Vector2(0, -14), new Vector2(42, 42));
            TMP_Text rewardLabel = CreateText(row.transform, "RewardLabel", "100", 20, TextAlignmentOptions.MidlineLeft, TitleTeal,
                new Vector2(Frac(700), 0.5f), new Vector2(Frac(700), 0.5f), new Vector2(0, 0.5f), new Vector2(0, -14), new Vector2(90, 30));

            Button statusButton = CreateButton(row.transform, "StatusButton", LoadUI("Achievement_statusBox"), null, null,
                new Vector2(Frac(ReferenceRowWidth - 40), 0.5f), new Vector2(Frac(ReferenceRowWidth - 40), 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(190, 70));
            TMP_Text statusLabel = CreateText(statusButton.transform, "Label", "IN PROGRESS", 16, TextAlignmentOptions.Center, RowText,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            AchievementRowView view = row.AddComponent<AchievementRowView>();
            view.titleLabel = title;
            view.subtitleLabel = subtitle;
            view.progressLabel = progressLabel;
            view.progressFill = progressFill;
            view.rewardLabel = rewardLabel;
            view.statusButton = statusButton;
            view.statusButtonImage = statusButton.GetComponent<Image>();
            view.statusButtonLabel = statusLabel;
            view.statusInProgressSprite = LoadUI("Achievement_statusBox");
            view.statusClaimSprite = LoadUI("Achievement_ClaimBtn");
            view.statusCompleteSprite = LoadUI("Achievement_CompleteBOx");

            return view;
        }
    }
}
