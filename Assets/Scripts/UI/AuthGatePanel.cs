using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>First-run gate shown once at game launch, before the player has any identity (no
    /// chosen name and no linked account yet) - offers Sign In / Create Account / Continue as
    /// Guest. Whichever path is taken, HasChosenName() ends up true and this screen never shows
    /// again (the local save and UGS's own session token remember the choice from then on).</summary>
    public class AuthGatePanel : MonoBehaviour
    {
        public Button loginButton;
        public Button registerButton;
        public Button guestButton;
        public Button settingsButton;
        public Button quitButton;

        private void Awake()
        {
            if (loginButton != null) loginButton.onClick.AddListener(OpenLogin);
            if (registerButton != null) registerButton.onClick.AddListener(OpenRegister);
            if (guestButton != null) guestButton.onClick.AddListener(ContinueAsGuest);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void OnEnable()
        {
            AccountManager.AccountLinked += OnAccountLinked;
        }

        private void OnDisable()
        {
            AccountManager.AccountLinked -= OnAccountLinked;
        }

        private void OnAccountLinked()
        {
            if (this == null) return;
            Close();
            EnsureNamed();
        }

        private void ContinueAsGuest()
        {
            Close();
            EnsureNamed();
        }

        private void EnsureNamed()
        {
            if (LeaderboardManager.HasChosenName()) return;
            OpenOverlay("PlayerProfilePanel");
        }

        private void OpenLogin()
        {
            OpenOverlay("LoginPanel");
        }

        private void OpenRegister()
        {
            OpenOverlay("RegisterPanel");
        }

        private void OpenSettings()
        {
            OpenOverlay("SettingsPanel");
        }

        // Before choosing Sign In / Create Account / Guest, a brand-new player has no other way
        // to reach settings (e.g. language) or leave the game entirely - every other screen
        // already has both, this one previously had neither.
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenOverlay(string resourceName)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + resourceName);
            if (prefab == null) return;
            Instantiate(prefab, transform.parent != null ? transform.parent : transform);
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
