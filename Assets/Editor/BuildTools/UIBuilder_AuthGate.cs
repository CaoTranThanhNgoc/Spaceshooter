using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the first-run "who are you" gate (Resources/Prefabs/UI/AuthGatePanel.prefab)
    /// - shown once by MainMenuUI at launch, before the player has any chosen name or linked
    /// account. Deliberately has no close (X) button: it must resolve to Sign In, Create Account,
    /// or Continue as Guest, matching the other two panels' own build/behavior.</summary>
    public static class UIBuilder_AuthGate
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.3f, 0.4f, 0.45f, 1f);

        [MenuItem("Tools/Space Hawk/17. Build Auth Gate Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("AuthGatePanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "WELCOME, PILOT", "authgate.title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(700, 480));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            CreateText(panel.transform, "Description",
                "Sign in to keep your progress across devices, or jump right in as a guest.", 20, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -55), new Vector2(600, 70), "authgate.desc").Flexible(12f);

            Vector2 btnSize = new Vector2(420, 80);
            Button loginBtn = CreateTextButton(panel.transform, "LoginButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), null,
                "SIGN IN", 26, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), btnSize, Image.Type.Sliced, "account.signin");
            Button registerBtn = CreateTextButton(panel.transform, "RegisterButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), null,
                "CREATE ACCOUNT", 26, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -265), btnSize, Image.Type.Sliced, "account.register");
            Button guestBtn = CreateTextButton(panel.transform, "GuestButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), null,
                "PLAY AS GUEST", 26, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -360), btnSize, Image.Type.Sliced, "account.continue_guest");

            // Settings icon, top-right of the SCREEN (root, not the card) - same sprites/position
            // convention as Level Select's own settings button. Before choosing Sign In / Create
            // Account / Guest, a brand-new player had no way to reach settings (e.g. language) at
            // all, unlike every other screen in the game.
            Button settingsBtn = CreateButton(root.transform, "SettingsButton", LoadUI("Btn_Settings_Normal"), LoadUI("Btn_Settings_Hover"), LoadUI("Btn_Settings_Disable"),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -50), new Vector2(96, 96));
            settingsBtn.gameObject.AddComponent<SafeAreaCorner>();

            // Quit as a plain understated text link below the card, not a full button - it's a
            // legitimate escape hatch from this first-run gate (a player who doesn't want to sign
            // in/register/play right now previously had no way to leave at all), but shouldn't
            // visually compete with the 3 real choices above it.
            GameObject quitGo = NewUI("QuitButton", root.transform);
            Anchor(RT(quitGo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(320, 44));
            Image quitBg = quitGo.AddComponent<Image>();
            quitBg.color = new Color(0f, 0f, 0f, 0f);
            quitBg.raycastTarget = true;
            Button quitBtn = quitGo.AddComponent<Button>();
            quitBtn.targetGraphic = quitBg;
            quitBtn.transition = Selectable.Transition.ColorTint;
            quitGo.AddComponent<ButtonClickSound>();
            CreateText(quitGo.transform, "Label", "QUIT GAME", 18, TextAlignmentOptions.Center, MutedText,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "authgate.quit");

            AuthGatePanel comp = root.AddComponent<AuthGatePanel>();
            comp.loginButton = loginBtn;
            comp.registerButton = registerBtn;
            comp.guestButton = guestBtn;
            comp.settingsButton = settingsBtn;
            comp.quitButton = quitBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/AuthGatePanel.prefab");
            Debug.Log("[UIBuilder_AuthGate] Auth gate panel built.");
        }
    }
}
