using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Player Profile overlay (Resources/Prefabs/UI/PlayerProfilePanel.prefab)
    /// - opened from the player icon button on Level Select, both for the first-ever naming
    /// prompt and as an always-available "change my name" screen afterward.</summary>
    public static class UIBuilder_PlayerProfile
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedText = new Color(0.6f, 0.73f, 0.78f, 1f);   // readable on the dark panel
        private static readonly Color AccentColor = new Color(0.45f, 0.9f, 0.95f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.83f, 0.2f, 1f);

        [MenuItem("Tools/Space Hawk/13. Build Player Profile Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("PlayerProfilePanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            // Titled "Pilot Profile", not "Choose Your Pilot Name" - this screen now covers both
            // the display name AND the account section below it, opened from a single icon button.
            UIBuilder_Overlays.BuildDimTitle(root.transform, "PILOT PROFILE", "profile.panel_title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 760));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            // Every Y below is derived from a single running cursor (top edge of the PREVIOUS
            // element's bottom, minus a fixed gap) instead of independently-guessed numbers, so
            // gaps between neighbors are provably positive rather than something to eyeball. All
            // elements here use the top-pivot (0.5, 1) convention EXCEPT the stats icon/label
            // pairs, which use a left-middle pivot (0, 0.5) - noted separately below.
            float y = -32; // top edge of Header1

            // --- Section 1: Display name ---
            TMP_Text header1 = CreateText(panel.transform, "SectionHeader1", "1. DISPLAY NAME", 22, TextAlignmentOptions.Center, AccentColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 32), "profile.section_name");
            header1.fontStyle = FontStyles.Bold;
            y -= 32 + 8; // Header1 height + gap -> Description top

            // No LocalizedText on it: PlayerProfilePanel sets this line itself (what the name is for, or - while the
            // weekly lock is on - when it can be changed again).
            TMP_Text nameHint = CreateText(panel.transform, "Description", "This name will be shown on the Leaderboard for everyone to see.", 17, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(640, 40), null).Flexible(12f);
            y -= 40 + 20; // Description height + gap -> NameInput top

            TMP_InputField nameInput = CreateInputField(panel.transform, "NameInput", "Enter a name...",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(600, 64), 16, "leaderboard.name_placeholder");
            y -= 64 + 20; // NameInput height + gap -> Confirm button top

            // No RANDOM button: every profile already has a name (the game's "PilotXXXX" for a guest, the
            // username for a new account - see NameService), and the player can just type over it.
            Button confirmBtn = CreateTextButton(panel.transform, "ConfirmButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), LoadUI("Disable_Btn"),
                "CONFIRM", 24, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(280, 64), Image.Type.Sliced, "leaderboard.confirm");
            y -= 64 + 24; // button row height + gap -> Divider1 top

            // Dividers are plain flat-tinted rects rather than sprites, since none of the pack's
            // decorative line art is actually a straight horizontal divider shape.
            Image divider1 = CreateImage(panel.transform, "Divider1", null,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 3));
            divider1.color = new Color(0.63f, 0.93f, 0.93f, 0.4f);
            divider1.raycastTarget = false;
            y -= 3 + 24; // divider height + gap -> Header2 top

            // --- Section 2: Lifetime stats (score, crystals, energy) - read-only, refreshed each
            // time the panel opens by PlayerProfilePanel.RefreshStats().
            TMP_Text header2 = CreateText(panel.transform, "SectionHeader2", "2. STATS", 22, TextAlignmentOptions.Center, AccentColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 32), "profile.section_stats");
            header2.fontStyle = FontStyles.Bold;
            y -= 32 + 14; // Header2 height + gap -> stats row TOP edge

            // Icon + label pairs use a LEFT-MIDDLE pivot (0, 0.5), not the centered (0.5, 1) pivot
            // everything else in this panel uses - with a centered pivot, anchoredPosition.x is
            // the MIDDLE of each element, so a 44px icon and a 150px label placed 50px apart by
            // their centers overlapped almost entirely (the original bug). A left pivot makes
            // anchoredPosition.x the element's own left edge, so [icon][gap][label] lay out as
            // plain adjacent spans. `y` here is the row's TOP edge, so the pivot's Y (its own
            // vertical center) is y minus half the row height (18 of the 36px row).
            float statsRowTop = y;
            float statsRowCenterY = statsRowTop - 18;
            CreateImage(panel.transform, "EnergyIcon", LoadUI("BateryIcon"),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, 0.5f), new Vector2(-162, statsRowCenterY), new Vector2(36, 36));
            TMP_Text energyLabel = CreateText(panel.transform, "EnergyLabel", "10/10", 22, TextAlignmentOptions.MidlineLeft, BodyLight,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, 0.5f), new Vector2(-118, statsRowCenterY), new Vector2(90, 36));

            CreateImage(panel.transform, "CrystalIcon", LoadUI("CrystalIcon"),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, 0.5f), new Vector2(28, statsRowCenterY), new Vector2(36, 36));
            TMP_Text crystalLabel = CreateText(panel.transform, "CrystalLabel", "0", 22, TextAlignmentOptions.MidlineLeft, Yellow,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, 0.5f), new Vector2(72, statsRowCenterY), new Vector2(90, 36));
            y -= 36 + 10; // stats row height + gap -> ScoreLabel top

            // No locKey here - the {0} score value must be substituted at runtime via
            // Localization.Format, which PlayerProfilePanel.RefreshStats already does; a LocalizedText
            // component would instead overwrite it with the raw "Score: {0}" template on every
            // language change.
            TMP_Text scoreLabel = CreateText(panel.transform, "ScoreLabel", "Skill rating: 0", 18, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 26));
            y -= 26 + 20; // ScoreLabel height + gap -> Divider2 top

            Image divider2 = CreateImage(panel.transform, "Divider2", null,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 3));
            divider2.color = new Color(0.63f, 0.93f, 0.93f, 0.4f);
            divider2.raycastTarget = false;
            y -= 3 + 24; // divider height + gap -> Header3 top

            // --- Section 3: Online account - only 2 of these 4 buttons are ever visible at once,
            // PlayerProfilePanel.RefreshAccountStatus toggles them by guest/linked state. The pair
            // not shown by default here (SignOut/Delete) is also built inactive so the two pairs
            // can never visually overlap even for a single frame before the script runs.
            TMP_Text header3 = CreateText(panel.transform, "SectionHeader3", "3. ONLINE ACCOUNT", 22, TextAlignmentOptions.Center, AccentColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 32), "profile.section_account");
            header3.fontStyle = FontStyles.Bold;
            y -= 32 + 10; // Header3 height + gap -> AccountStatus top

            // No locKey on AccountStatus/AccountHint - both mix in runtime data (username) or
            // depend on guest/linked state, which PlayerProfilePanel already re-applies (via
            // RefreshAccountStatus) on Localization.LanguageChanged; a static LocalizedText would
            // otherwise reset them to their guest-state build-time literal on a language switch.
            TMP_Text accountStatus = CreateText(panel.transform, "AccountStatus", "Playing as Guest", 20, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 30)).Flexible(13f);
            accountStatus.fontStyle = FontStyles.Bold;
            y -= 30 + 8; // AccountStatus height + gap -> AccountHint top

            TMP_Text accountHint = CreateText(panel.transform, "AccountHint", "Create an account to keep your progress if you switch devices.", 15, TextAlignmentOptions.Center, MutedText,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(660, 36)).Flexible(11f);

            // Signed in: the hint line gives way to the recovery contact (what a forgotten password is
            // reset through) and a button to add / change it. PlayerProfilePanel swaps the two.
            TMP_Text recoveryLabel = CreateText(panel.transform, "RecoveryLabel", "Recovery: pi***@example.com", 16, TextAlignmentOptions.MidlineRight, BodyLight,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-80, y), new Vector2(500, 40));
            recoveryLabel.enableAutoSizing = true;
            recoveryLabel.fontSizeMin = 11;
            recoveryLabel.fontSizeMax = 16;
            Button recoveryBtn = CreateTextButton(panel.transform, "RecoveryButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "CHANGE", 16, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(265, y), new Vector2(130, 40), Image.Type.Sliced);
            TMP_Text recoveryBtnLabel = recoveryBtn.GetComponentInChildren<TMP_Text>();
            recoveryLabel.gameObject.SetActive(false);
            recoveryBtn.gameObject.SetActive(false);
            y -= 36 + 20; // AccountHint height + gap -> button row top

            // Button row bottom edge = y - 64. Panel height (740) leaves ~40px of clear padding
            // below it instead of the ~2px that was there before (buttons visually touching the
            // panel's own border).
            Button registerBtn = CreateTextButton(panel.transform, "RegisterButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "CREATE ACCOUNT", 19, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-160, y), new Vector2(290, 64), Image.Type.Sliced, "account.register");
            Button signInBtn = CreateTextButton(panel.transform, "SignInButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "SIGN IN", 19, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(160, y), new Vector2(290, 64), Image.Type.Sliced, "account.signin");
            // Signed in: three buttons side by side (each 210 wide, 15 apart = the 660 content width).
            Button signOutBtn = CreateTextButton(panel.transform, "SignOutButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "LOG OUT", 18, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-225, y), new Vector2(210, 64), Image.Type.Sliced, "account.signout");
            Button changePasswordBtn = CreateTextButton(panel.transform, "ChangePasswordButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "CHANGE PASSWORD", 18, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(210, 64), Image.Type.Sliced, "account.change_password");
            Button deleteBtn = CreateTextButton(panel.transform, "DeleteAccountButton", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "DELETE ACCOUNT", 18, new Color(1f, 0.55f, 0.5f, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(225, y), new Vector2(210, 64), Image.Type.Sliced, "account.delete");
            foreach (Button b in new[] { signOutBtn, changePasswordBtn, deleteBtn })
            {
                TMP_Text caption = b.GetComponentInChildren<TMP_Text>();
                caption.enableAutoSizing = true;
                caption.fontSizeMin = 12;
                caption.fontSizeMax = 18;
            }
            signOutBtn.gameObject.SetActive(false);
            changePasswordBtn.gameObject.SetActive(false);
            deleteBtn.gameObject.SetActive(false);

            PlayerProfilePanel comp = root.AddComponent<PlayerProfilePanel>();
            comp.nameInput = nameInput;
            comp.nameHintLabel = nameHint;
            comp.confirmButton = confirmBtn;
            comp.closeButton = closeBtn;
            comp.scoreLabel = scoreLabel;
            comp.crystalLabel = crystalLabel;
            comp.energyLabel = energyLabel;
            comp.accountStatusLabel = accountStatus;
            comp.accountHintLabel = accountHint;
            comp.registerButton = registerBtn;
            comp.signInButton = signInBtn;
            comp.signOutButton = signOutBtn;
            comp.deleteAccountButton = deleteBtn;
            comp.changePasswordButton = changePasswordBtn;
            comp.recoveryLabel = recoveryLabel;
            comp.recoveryButton = recoveryBtn;
            comp.recoveryButtonLabel = recoveryBtnLabel;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/PlayerProfilePanel.prefab");
            Debug.Log("[UIBuilder_PlayerProfile] Player profile panel built.");
        }
    }
}
