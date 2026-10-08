using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceHawk.Data;

namespace SpaceHawk.Core
{
    /// <summary>
    /// Scene flow + hand-off state between the MainMenu scene (Landing + Level Select screens)
    /// and the Gameplay scene. Plain static class: LevelData is a ScriptableObject asset
    /// reference, so it survives a scene load without any DontDestroyOnLoad object.
    /// </summary>
    public static class GameManager
    {
        public const string MainMenuSceneName = "MainMenu";
        public const string GameplaySceneName = "Gameplay";

        public static LevelData SelectedLevel { get; private set; }

        /// <summary>True when MainMenu scene should open straight to the Level Select screen
        /// (returning from Gameplay) instead of the Landing screen (cold start).</summary>
        public static bool ReturnToLevelSelectOnMenuLoad { get; private set; }

        public static void StartLevel(LevelData level)
        {
            SelectedLevel = level;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameplaySceneName);
        }

        public static void RestartCurrentLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameplaySceneName);
        }

        public static void ReturnToMenu(bool openLevelSelect)
        {
            ReturnToLevelSelectOnMenuLoad = openLevelSelect;
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }
}
