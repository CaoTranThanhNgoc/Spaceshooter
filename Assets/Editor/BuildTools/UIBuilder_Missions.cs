using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using SpaceHawk.Core;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Missions overlay (Resources/Prefabs/UI/MissionsPanel.prefab): five rows
    /// in the Achievements list's own row layout - daily login streak, the three daily missions
    /// and the all-done bonus - inside the same bordered panel. MissionsPanel fills them at runtime.</summary>
    public static class UIBuilder_Missions
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private const float RowHeight = 100f;
        private const float RowSpacing = 14f;
        private const float FirstRowY = -52f;

        [MenuItem("Tools/Space Hawk/9. Build Missions Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("MissionsPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "DAILY MISSIONS", "missions.title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(0, 700));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            float Y(int row) => FirstRowY - row * (RowHeight + RowSpacing);
            AchievementRowView dailyRow = UIBuilder_Achievements.BuildRow(panel.transform, Y(0), RowHeight);
            AchievementRowView[] missionRows = new AchievementRowView[DailyMissions.Count];
            for (int i = 0; i < missionRows.Length; i++)
                missionRows[i] = UIBuilder_Achievements.BuildRow(panel.transform, Y(i + 1), RowHeight);
            AchievementRowView bonusRow = UIBuilder_Achievements.BuildRow(panel.transform, Y(DailyMissions.Count + 1), RowHeight);

            MissionsPanel comp = root.AddComponent<MissionsPanel>();
            comp.dailyRow = dailyRow;
            comp.missionRows = missionRows;
            comp.bonusRow = bonusRow;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/MissionsPanel.prefab");
            Debug.Log("[UIBuilder_Missions] Missions panel built.");
        }
    }
}
