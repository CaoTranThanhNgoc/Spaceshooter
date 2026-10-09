using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Core;
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
            // Every profile has a display name from the start (a guest shows up under the same name here and
            // on the Leaderboard).
            SaveManager.EnsureDefaultName();

            // The very first thing a brand-new player sees, before they ever touch Play - once answered
            // (guest, account created or signed in) this device never shows it again, whatever happens later.
            if (!SaveManager.HasChosenIdentity())
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
