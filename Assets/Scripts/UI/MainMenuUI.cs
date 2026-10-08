using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        public Button playButton;
        public Button settingsButton;
        public Button quitButton;
        public MenuUIRoot menuRoot;
        public Transform overlayRoot;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        }

        private void Start()
        {
            // The very first thing a brand-new player sees, before they ever touch Play - once
            // resolved (guest name chosen, or an account linked), HasChosenName() stays true and
            // this never appears again on later launches.
            if (!LeaderboardManager.HasChosenName())
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/AuthGatePanel");
                if (prefab != null) Instantiate(prefab, overlayRoot != null ? overlayRoot : transform.parent);
            }
        }

        private void OnPlay()
        {
            if (menuRoot != null) menuRoot.ShowLevelSelect(true);
        }

        private void OnSettings()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/SettingsPanel");
            if (prefab == null) return;
            Transform parent = overlayRoot != null ? overlayRoot : transform.parent;
            Instantiate(prefab, parent);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
