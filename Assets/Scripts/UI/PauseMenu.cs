using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    public class PauseMenu : MonoBehaviour
    {
        public Button resumeButton;
        public Button restartButton;
        public Button settingsButton;
        public Button menuButton;
        public Transform overlayRoot;

        private void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (menuButton != null) menuButton.onClick.AddListener(ToMenu);
        }

        private void OnEnable()
        {
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            Time.timeScale = 1f;
            Destroy(gameObject);
        }

        private void Restart()
        {
            // Same energy gate LevelSelectUI/GameOverPanel apply - without this a mid-level pause
            // gave a completely free restart, unlike every other way into a level.
            LevelData level = GameManager.SelectedLevel;
            int cost = level != null ? level.energyCost : 0;
            if (!SaveManager.TrySpendEnergy(cost))
            {
                EnergyRefillDialog.Show(transform);
                return;
            }

            GameManager.RestartCurrentLevel();
        }

        private void OpenSettings()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/SettingsPanel");
            if (prefab == null) return;
            Transform parent = overlayRoot != null ? overlayRoot : transform.parent;
            Instantiate(prefab, parent);
        }

        private void ToMenu()
        {
            GameManager.ReturnToMenu(true);
        }
    }
}
