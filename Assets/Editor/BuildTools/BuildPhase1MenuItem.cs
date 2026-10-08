using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SpaceHawk.Data;

namespace SpaceHawk.EditorTools
{
    /// <summary>Single entry point that runs every Phase 1 builder in order. Safe to re-run:
    /// each step updates its own assets/scenes in place rather than duplicating them.</summary>
    public static class BuildPhase1MenuItem
    {
        [MenuItem("Tools/Space Hawk/0. Build Everything (Phase 1)")]
        public static void Run()
        {
            UISpriteImportSettings.ApplyAll();
            AudioImportSettings.ApplyAll();
            FontImportSettings.ApplyAll();

            LevelDatabase database = LevelContentBuilder.Build();
            AchievementDatabase achievementDatabase = AchievementContentBuilder.Build();
            GameplayPrefabBuilder.BuiltPrefabs prefabs = GameplayPrefabBuilder.Build();

            UIBuilder_Overlays.BuildAll();
            UIBuilder_Achievements.Build(achievementDatabase);
            UIBuilder_Missions.Build();
            UIBuilder_Inventory.Build();
            UIBuilder_HowToPlay.Build();
            UIBuilder_Leaderboard.Build();
            UIBuilder_ConfirmDialog.Build();
            UIBuilder_EnergyDialog.Build();
            UIBuilder_Register.Build();
            UIBuilder_Login.Build();
            UIBuilder_ChangePassword.Build();
            UIBuilder_ForgotPassword.Build();
            UIBuilder_RecoveryContact.Build();
            UIBuilder_VerifyContact.Build();
            UIBuilder_PlayerProfile.Build();
            UIBuilder_AuthGate.Build();
            UIBuilder_MainMenuScene.Build(database);
            UIBuilder_GameplayScene.Build(prefabs);

            RegisterScenesInBuildSettings();
            ProjectConfigurator.Apply();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BuildPhase1MenuItem] Phase 1 build complete.");
        }

        private static void RegisterScenesInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Gameplay.unity", true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
