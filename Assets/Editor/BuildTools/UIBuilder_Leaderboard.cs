using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Leaderboard overlay (Resources/Prefabs/UI/LeaderboardPanel.prefab).
    /// Same scrollable fixed-row-slot pattern as Achievements/HowToPlay, except these rows start
    /// empty - LeaderboardPanel fills them from a live GetTopScores call once the panel opens,
    /// since (unlike achievements) the content genuinely isn't known at build time.</summary>
    public static class UIBuilder_Leaderboard
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.83f, 0.2f, 1f);

        private const float RowHeight = 76f;
        private const float RowSpacing = 12f;
        private const int MaxRows = 10;

        [MenuItem("Tools/Space Hawk/12. Build Leaderboard Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("LeaderboardPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "LEADERBOARD", "levelselect.leaderboard");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(0, 700));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            TMP_Text loadingLabel = CreateText(panel.transform, "LoadingLabel", "Loading...", 26, TextAlignmentOptions.Center, BodyLight,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).Flexible(14f);
            TMP_Text errorLabel = CreateText(panel.transform, "ErrorLabel", "Could not load leaderboard - check your connection and try again.", 22, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 120)).Flexible(12f);
            // Both start hidden - OnEnable decides which (if either) to show once the actual
            // GetTopScores call resolves. Leaving loadingLabel active by default previously meant
            // it sat there forever behind whatever opened on top of it (e.g. the old in-panel name
            // prompt) instead of only appearing while a request is genuinely in flight.
            loadingLabel.gameObject.SetActive(false);
            errorLabel.gameObject.SetActive(false);

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

            LeaderboardRowView[] rows = new LeaderboardRowView[MaxRows];
            for (int i = 0; i < MaxRows; i++)
            {
                float y = -i * (RowHeight + RowSpacing);
                rows[i] = BuildRow(content.transform, y);
            }

            LeaderboardPanel comp = root.AddComponent<LeaderboardPanel>();
            comp.closeButton = closeBtn;
            comp.rows = rows;
            comp.loadingLabel = loadingLabel.gameObject;
            comp.errorLabel = errorLabel.gameObject;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/LeaderboardPanel.prefab");
            Debug.Log("[UIBuilder_Leaderboard] Leaderboard panel built.");
        }

        private static LeaderboardRowView BuildRow(Transform parent, float y)
        {
            GameObject row = NewUI("Row", parent);
            Anchor(RT(row), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(-60, RowHeight));

            // BgHudBar + light text + outline, not the lighter Leaderboard_Box sprite - that
            // combo is already proven readable elsewhere in this HUD (health/kills/boss bars);
            // Leaderboard_Box's own translucency let the dark panel behind it bleed through and
            // wash out the dark text that used to sit on it.
            Image bg = row.AddComponent<Image>();
            bg.sprite = LoadUI("BgHudBar");
            bg.type = Image.Type.Sliced;

            TMP_Text rank = CreateText(row.transform, "Rank", "#1", 24, TextAlignmentOptions.MidlineLeft, BodyLight,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(90, 40));
            rank.fontStyle = FontStyles.Bold;
            rank.outlineWidth = 0.2f;
            rank.outlineColor = Color.black;

            TMP_Text name = CreateText(row.transform, "Name", "Pilot0000", 22, TextAlignmentOptions.MidlineLeft, BodyLight,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(500, 40));
            name.outlineWidth = 0.2f;
            name.outlineColor = Color.black;

            TMP_Text score = CreateText(row.transform, "Score", "0", 22, TextAlignmentOptions.MidlineRight, Yellow,
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(220, 40));
            score.fontStyle = FontStyles.Bold;
            score.outlineWidth = 0.2f;
            score.outlineColor = Color.black;

            LeaderboardRowView view = row.AddComponent<LeaderboardRowView>();
            view.background = bg;
            view.rankLabel = rank;
            view.nameLabel = name;
            view.scoreLabel = score;

            row.SetActive(false);
            return view;
        }
    }
}
