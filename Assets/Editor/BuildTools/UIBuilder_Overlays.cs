using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the small overlay prefabs (Settings, Pause, Game Over, Victory, Toast)
    /// that get instantiated on demand at runtime via Resources.Load("Prefabs/UI/...").</summary>
    public static class UIBuilder_Overlays
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color TitleTeal = new Color(0.63f, 0.93f, 0.93f, 1f);
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.83f, 0.2f, 1f);

        [MenuItem("Tools/Space Hawk/4. Build Overlay Prefabs")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(OutFolder);
            BuildToast();
            BuildSettingsPanel();
            BuildPauseMenu();
            BuildGameOverPanel();
            BuildVictoryPanel();
            AssetDatabase.SaveAssets();
            Debug.Log("[UIBuilder_Overlays] Overlay prefabs built.");
        }

        // ---------------------------------------------------------------- Toast

        private static void BuildToast()
        {
            GameObject root = NewUI("Toast", null);
            // y=20, not 70 - the tallest overlay panel (PlayerProfilePanel, 740px on a 1080-tall
            // reference canvas at its usual -40 offset) has its own bottom edge at y=130, so a
            // 74px-tall toast anchored at y=70 (spanning 70-144) clipped into that panel's bottom
            // border/buttons. y=20 (spanning 20-94) clears it with room to spare.
            Anchor(RT(root), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(560, 74));

            // The button art is see-through, so a notice drawn over a panel let that panel's text show
            // through it. A solid backdrop sits behind the frame (ToastUI resizes the root; all three
            // layers stretch with it).
            Image backdrop = CreateStretchImage(root.transform, "Backdrop", null);
            backdrop.color = new Color(0.03f, 0.08f, 0.12f, 0.97f);
            Stretch(RT(backdrop.gameObject), 10, 10, 8, 8);
            CreateStretchImage(root.transform, "Frame", LoadUI("Normal_LongBtn"), Image.Type.Sliced);

            TMP_Text label = CreateText(root.transform, "Label", "Coming soon", 30, TextAlignmentOptions.Center, BodyLight,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            ToastUI toast = root.AddComponent<ToastUI>();
            toast.label = label;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/Toast.prefab");
        }

        // ---------------------------------------------------------------- Settings

        // Two columns instead of one long list: left = display/system (Resolution, Fullscreen,
        // Language), right = audio (Volume, Sound Effects). Column A is the taller of the two (3
        // rows vs 2), so it sets the panel's required height. Rows are hand-placed in pixels
        // assuming this whole two-column block (x=60 to x=990) sits flush against the panel's
        // left edge; centering it on the panel's horizontal middle instead, offset by half its
        // width, keeps it centered at any panel width instead of leaving a lopsided empty gap.
        private const float ColumnWidth = 440f;
        private const float ColumnGap = 50f;
        private const float ColumnAX = 60f;
        private const float ColumnBX = ColumnAX + ColumnWidth + ColumnGap;
        private const float SettingsContentLeft = ColumnAX;
        private const float SettingsContentRight = ColumnBX + ColumnWidth;
        private const float SettingsContentCenterOffset = (SettingsContentLeft + SettingsContentRight) / 2f;

        private static void BuildSettingsPanel()
        {
            GameObject root = NewUI("SettingsPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            BuildDimTitle(root.transform, "SETTINGS", "settings.title");

            // Stretched by width (% of whatever the screen's actual width is) instead of a fixed
            // pixel size, so the panel never overflows off-screen on narrower aspect ratios.
            // Height stays fixed since the canvas always matches height 1:1 (see CreateCanvas).
            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.13f, 0.5f), new Vector2(0.87f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(0, 700));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Transform p = panel.transform;
            Vector2 rowAnchor = new Vector2(0.5f, 1);
            float X(float original) => original - SettingsContentCenterOffset;

            // Column A, row 0: resolution
            LabelAt(p, "DROP DOWN", -100f, "settings.resolution_label", X(ColumnAX));
            Button resBtn = CreateButton(p, "ResolutionButton", LoadUI("BgDropdownContent"), null, null,
                rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnAX), -178), new Vector2(ColumnWidth, 68), Image.Type.Sliced);
            TMP_Text resLabel = CreateText(resBtn.transform, "Label", "1920 X 1080", 26, TextAlignmentOptions.Left, Yellow,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(24, 0), new Vector2(-70, 0));
            Image resArrow = CreateImage(resBtn.transform, "Arrow", LoadUI("BtnArrowDown"), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), Vector2.zero);

            // Column A, row 1: fullscreen toggle
            LabelAt(p, "DISPLAY", -280f, "settings.display", X(ColumnAX));
            Toggle fsToggle = CreateToggle(p, "FullscreenToggle", LoadUI("RadioButtonBOx"), LoadUI("RadioButtonFill"),
                rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnAX), -358), new Vector2(58, 59));
            CreateText(p, "FullscreenLabel", "FULL SCREEN", 28, TextAlignmentOptions.MidlineLeft, BodyLight,
                rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnAX + 74), -358), new Vector2(300, 50), "settings.fullscreen");

            // Column A, row 2: language - tap to cycle between the 2 available languages. No
            // dropdown arrow here (unlike Resolution) since tapping cycles directly rather than
            // opening a list to pick from - the arrow previously shown implied the wrong behavior.
            LabelAt(p, "LANGUAGE", -460f, "settings.language", X(ColumnAX));
            Button langBtn = CreateButton(p, "LanguageButton", LoadUI("BgDropdownContent"), null, null,
                rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnAX), -538), new Vector2(ColumnWidth, 68), Image.Type.Sliced);
            TMP_Text langLabel = CreateText(langBtn.transform, "Label", "ENGLISH", 26, TextAlignmentOptions.Left, Yellow,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(24, 0), new Vector2(-24, 0));

            // Column B, row 0: volume slider
            LabelAt(p, "MUSIC / SFX VOLUME", -100f, "settings.volume", X(ColumnBX));
            Slider slider = CreateSlider(p, "VolumeSlider", LoadUI("BgSlidebar"), LoadUI("YellowSlidebar"), LoadUI("Slider"),
                rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnBX), -178), new Vector2(ColumnWidth, 40));

            // Column B, row 1: sfx on/off. Both stay clickable always - SettingsPanel dims
            // whichever one isn't active rather than using Unity's disabled-sprite state for that
            // (which showed up as a plain white box instead of a properly styled button).
            LabelAt(p, "SOUND EFFECTS", -280f, "settings.sfx", X(ColumnBX));
            Button onBtn = CreateTextButton(p, "OptionOn", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "ON", 28, BodyLight, rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnBX), -358), new Vector2(200, 74), Image.Type.Sliced, "settings.on");
            Button offBtn = CreateTextButton(p, "OptionOff", LoadUI("Normal_Btn"), LoadUI("Hover_Btn"), null,
                "OFF", 28, BodyLight, rowAnchor, rowAnchor, new Vector2(0, 1), new Vector2(X(ColumnBX + 220), -358), new Vector2(200, 74), Image.Type.Sliced, "settings.off");

            Button closeBtn = BuildHeaderCloseButton(root.transform);

            SettingsPanel comp = root.AddComponent<SettingsPanel>();
            comp.resolutionButton = resBtn;
            comp.resolutionLabel = resLabel;
            comp.resolutionArrow = resArrow;
            comp.volumeSlider = slider;
            comp.optionOnButton = onBtn;
            comp.optionOnImage = onBtn.GetComponent<Image>();
            comp.optionOffButton = offBtn;
            comp.optionOffImage = offBtn.GetComponent<Image>();
            comp.fullscreenToggle = fsToggle;
            comp.languageButton = langBtn;
            comp.languageLabel = langLabel;
            comp.closeButton = closeBtn;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/SettingsPanel.prefab");
        }

        // ---------------------------------------------------------------- Pause

        private static void BuildPauseMenu()
        {
            GameObject root = NewUI("PauseMenu", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            BuildDimTitle(root.transform, "PAUSED", "pause.title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(0, 600));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            // No X close button here - RESUME right below already closes this screen, and having
            // both felt redundant with a 4-button menu this small.
            Vector2 btnSize = new Vector2(520, 92);
            Button resume = CreateTextButton(panel.transform, "Resume", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "RESUME", 32, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), btnSize, Image.Type.Sliced, "pause.resume");
            Button restart = CreateTextButton(panel.transform, "Restart", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "RESTART", 32, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), btnSize, Image.Type.Sliced, "pause.restart");
            Button settings = CreateTextButton(panel.transform, "Settings", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "SETTINGS", 32, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), btnSize, Image.Type.Sliced, "pause.settings");
            Button menu = CreateTextButton(panel.transform, "Menu", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "MENU", 32, BodyLight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -420), btnSize, Image.Type.Sliced, "pause.menu");

            PauseMenu comp = root.AddComponent<PauseMenu>();
            comp.resumeButton = resume;
            comp.restartButton = restart;
            comp.settingsButton = settings;
            comp.menuButton = menu;
            comp.overlayRoot = root.transform;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/PauseMenu.prefab");
        }

        // ---------------------------------------------------------------- Game Over

        private static void BuildGameOverPanel()
        {
            GameObject root = NewUI("GameOverPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            BuildDimTitle(root.transform, "GAME OVER", "gameover.title");

            CreateImage(root.transform, "Badge", LoadUI("GameOver_Badges"), new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(681, 230));
            TMP_Text badgeLabel = CreateText(root.transform, "BadgeLabel", "GAME OVER", 40, TextAlignmentOptions.Center, Color.white,
                new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 60), "gameover.title");
            badgeLabel.outlineWidth = 0.2f;
            badgeLabel.outlineColor = Color.black;

            // Why the level ended - dying and failing to destroy enough enemies both land here now,
            // and without this the player has no way to tell which one happened. Two lines tall:
            // an Endless run also reports its wave, score, best-record and Crystal payout here.
            TMP_Text reasonLabel = CreateText(root.transform, "ReasonLabel", "", 22, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 90)).Flexible(14f);
            reasonLabel.outlineWidth = 0.15f;
            reasonLabel.outlineColor = Color.black;

            // Revive row - GameOverPanel hides it when reviving isn't possible (a not-enough-kills
            // defeat, or the one revive per attempt already used).
            GameObject reviveRow = NewUI("ReviveRow", root.transform);
            Anchor(RT(reviveRow), new Vector2(0.5f, 0.375f), new Vector2(0.5f, 0.375f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 90));
            Button revive = CreateTextButton(reviveRow.transform, "Revive", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "REVIVE (40 CRYSTAL)", 28, Yellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 90));

            Vector2 btnSize = new Vector2(420, 90);
            Button retry = CreateTextButton(root.transform, "Retry", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "RETRY", 32, BodyLight, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(-240, 0), btnSize, Image.Type.Sliced, "gameover.retry");
            Button menu = CreateTextButton(root.transform, "Menu", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "MENU", 32, BodyLight, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(240, 0), btnSize, Image.Type.Sliced, "gameover.menu");

            GameOverPanel comp = root.AddComponent<GameOverPanel>();
            comp.retryButton = retry;
            comp.menuButton = menu;
            comp.reasonLabel = reasonLabel;
            comp.reviveRow = reviveRow;
            comp.reviveButton = revive;
            comp.reviveLabel = revive.GetComponentInChildren<TMP_Text>();

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/GameOverPanel.prefab");
        }

        // ---------------------------------------------------------------- Victory

        private static void BuildVictoryPanel()
        {
            GameObject root = NewUI("VictoryPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            BuildDimTitle(root.transform, "VICTORY", "victory.title");

            // Composition: a left column (the ribbon with its flag pointing down at the medal, then
            // the medal itself - what you earned) beside a right column (the score card - how it was
            // scored), then the buttons. Everything about this run is one group around the screen's
            // centre; before, the numbers were split to either side of the medal and read as two
            // unrelated blocks.
            const float GroupY = 0.5f;
            const float RibbonScale = 0.75f;
            const float RibbonWidth = 681f * RibbonScale;
            const float RibbonHeight = 225f * RibbonScale;
            const float MedalWidth = 330f;
            const float MedalHeight = 244f;
            const float ColumnGap = 8f;
            const float CardWidth = 640f;
            const float CardHeight = 330f;
            const float GroupGap = 40f;

            float leftColumnWidth = Mathf.Max(RibbonWidth, MedalWidth);
            float leftColumnHeight = RibbonHeight + ColumnGap + MedalHeight;
            float groupWidth = leftColumnWidth + GroupGap + CardWidth;
            float leftX = -groupWidth * 0.5f + leftColumnWidth * 0.5f;
            float cardX = groupWidth * 0.5f - CardWidth * 0.5f;
            float ribbonY = leftColumnHeight * 0.5f - RibbonHeight * 0.5f;
            float medalY = -leftColumnHeight * 0.5f + MedalHeight * 0.5f;

            CreateImage(root.transform, "Badge", LoadUI("Victory_Badges"), new Vector2(0.5f, GroupY), new Vector2(0.5f, GroupY), new Vector2(0.5f, 0.5f),
                new Vector2(leftX, ribbonY), new Vector2(RibbonWidth, RibbonHeight));
            // The ribbon band sits above the image's own geometric center (the flag shape hanging
            // below the ribbon adds extra height at the bottom), so the label needs a matching lift.
            TMP_Text badgeLabel = CreateText(root.transform, "BadgeLabel", "VICTORY", 40 * RibbonScale, TextAlignmentOptions.Center, Color.white,
                new Vector2(0.5f, GroupY), new Vector2(0.5f, GroupY), new Vector2(0.5f, 0.5f), new Vector2(leftX, ribbonY + 23f * RibbonScale), new Vector2(420 * RibbonScale, 60 * RibbonScale), "victory.title");
            badgeLabel.outlineWidth = 0.2f;
            badgeLabel.outlineColor = Color.black;

            // Lvl0Star..Lvl3Star are each a complete badge with their own 0-3 stars already drawn
            // on it (the same art used for a level's node on the Level Select map) - so this is
            // one image swapped by index, not 3 separate star icons side by side. The level number
            // goes on its plate, exactly as on the map.
            Image starBadge = CreateImage(root.transform, "StarBadge", LoadUI("Lvl0Star"),
                new Vector2(0.5f, GroupY), new Vector2(0.5f, GroupY), new Vector2(0.5f, 0.5f), new Vector2(leftX, medalY), new Vector2(MedalWidth, MedalHeight));
            TMP_Text medalNumber = CreateText(starBadge.transform, "Number", "1", 50, TextAlignmentOptions.Center, new Color(0.15f, 0.2f, 0.25f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -13), new Vector2(190, 64));

            // ---- score card: four breakdown rows, a rule, the total, then a rating footer.
            GameObject card = NewUI("ScoreCard", root.transform);
            Anchor(RT(card), new Vector2(0.5f, GroupY), new Vector2(0.5f, GroupY), new Vector2(0.5f, 0.5f), new Vector2(cardX, 0), new Vector2(CardWidth, CardHeight));
            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = LoadUI("Achievement_Box");
            cardBg.type = Image.Type.Sliced;
            cardBg.raycastTarget = false;

            const float InnerWidth = 576f;
            TMP_Text breakdownLabels = CreateCardText(card.transform, "BreakdownLabels", 26, TextAlignmentOptions.TopLeft, BodyLight, -26f, 152f, InnerWidth);
            TMP_Text breakdownValues = CreateCardText(card.transform, "BreakdownValues", 26, TextAlignmentOptions.TopRight, Color.white, -26f, 152f, InnerWidth);
            breakdownLabels.lineSpacing = 12f;
            breakdownValues.lineSpacing = 12f;

            Image rule = CreateImage(card.transform, "Rule", null,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -194), new Vector2(InnerWidth, 3));
            rule.color = new Color(0.63f, 0.93f, 0.93f, 0.45f);
            rule.raycastTarget = false;

            TMP_Text totalLabel = CreateCardText(card.transform, "TotalLabel", 30, TextAlignmentOptions.MidlineLeft, BodyLight, -206f, 56f, InnerWidth);
            TMP_Text newBestLabel = CreateCardText(card.transform, "NewBestLabel", 26, TextAlignmentOptions.Center, Yellow, -206f, 56f, InnerWidth);
            TMP_Text totalValue = CreateCardText(card.transform, "TotalValue", 44, TextAlignmentOptions.MidlineRight, Yellow, -206f, 56f, InnerWidth);
            totalLabel.fontStyle = FontStyles.Bold;
            newBestLabel.fontStyle = FontStyles.Bold;
            totalValue.fontStyle = FontStyles.Bold;

            Color subtle = new Color(0.62f, 0.85f, 0.88f, 1f);
            TMP_Text bestBeforeLabel = CreateCardText(card.transform, "BestBeforeLabel", 19, TextAlignmentOptions.MidlineLeft, subtle, -274f, 30f, InnerWidth);
            TMP_Text ratingLabel = CreateCardText(card.transform, "RatingLabel", 19, TextAlignmentOptions.MidlineRight, BodyLight, -274f, 30f, InnerWidth);

            Vector2 btnSize = new Vector2(420, 90);
            Button cont = CreateTextButton(root.transform, "Continue", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "CONTINUE", 32, BodyLight, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(-240, 0), btnSize);
            Button menu = CreateTextButton(root.transform, "Menu", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "MENU", 32, BodyLight, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(240, 0), btnSize, Image.Type.Sliced, "victory.menu");

            VictoryPanel comp = root.AddComponent<VictoryPanel>();
            comp.medalNumberLabel = medalNumber;
            comp.breakdownLabels = breakdownLabels;
            comp.breakdownValues = breakdownValues;
            comp.totalLabel = totalLabel;
            comp.totalValue = totalValue;
            comp.newBestLabel = newBestLabel;
            comp.bestBeforeLabel = bestBeforeLabel;
            comp.ratingLabel = ratingLabel;
            comp.starBadge = starBadge;
            comp.starBadgeSprites = new[] { LoadUI("Lvl0Star"), LoadUI("Lvl1Star"), LoadUI("Lvl2Star"), LoadUI("Lvl3Star") };
            comp.continueButton = cont;
            comp.continueButtonLabel = cont.GetComponentInChildren<TMP_Text>();
            comp.menuButton = menu;

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/VictoryPanel.prefab");
        }

        /// <summary>A text line pinned to the top of the score card, `y` below its top edge.</summary>
        private static TMP_Text CreateCardText(Transform card, string name, float fontSize, TextAlignmentOptions align, Color color, float y, float height, float width)
        {
            TMP_Text text = CreateText(card, name, "", fontSize, align, color,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(width, height));
            text.outlineWidth = 0.12f;
            text.outlineColor = Color.black;
            return text;
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>Title text roughly over DarkBackground.png's built-in header pill. Purely
        /// decorative text - doesn't need to be exactly centered on anything, just readable near
        /// the top of the screen.</summary>
        public static void BuildDimTitle(Transform root, string title, string locKey = null)
        {
            if (string.IsNullOrEmpty(title)) return;

            // The baked-in pill isn't perfectly screen-centered in DarkBackground.png (it sits
            // ~34px right of true center at this reference resolution) - offset to match.
            // Width 460 (not the box's visual pill width) gives ~28 Vietnamese characters of
            // headroom at this font size - a title longer than that word-wraps to a second line
            // that the 50px-tall box can't fit, which is exactly what happened with the old
            // "CHON TEN PHI CONG CUA BAN" title (25 chars) overlapping the panel below it.
            TMP_Text label = CreateText(root, "TitleLabel", title, 30, TextAlignmentOptions.Center, Color.white,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(34, -64), new Vector2(460, 50), locKey);
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;
        }

        // DarkBackground.png is 2568x1447 with a 40px 9-slice border, and the overlay roots draw
        // it Sliced over the whole canvas. The empty notch the X belongs in is baked into that
        // art at the (sprite-pixel) rectangle below. A sliced image keeps its 40px borders at
        // a fixed size and stretches only the middle, so a point inside the stretched middle sits
        // at  border + t * (canvasSize - 2*border)  for a fixed fraction t. That is exactly a
        // fractional anchor (t) plus a fixed pixel offset, so the X stays glued to the notch at
        // every aspect ratio instead of drifting like a plain corner offset does.
        private const float BgWidth = 2568f, BgHeight = 1447f, BgBorder = 40f;
        private const float NotchCenterX = 2373f, NotchCenterY = 120.5f;   // measured on the PNG
        private const float CloseButtonWidth = 70f, CloseButtonHeight = 73f;

        /// <summary>Close (X) button pinned over the empty notch drawn into DarkBackground.png's
        /// header art (top-right). Deliberately NOT safe-area adjusted: the notch is ~160 canvas
        /// px in from the screen edge, well clear of device corners/cutouts, and a per-device
        /// nudge is exactly what made it slide off the notch before.</summary>
        public static Button BuildHeaderCloseButton(Transform root)
        {
            float middleW = BgWidth - 2f * BgBorder, middleH = BgHeight - 2f * BgBorder;
            float tx = (NotchCenterX - BgBorder) / middleW;
            float ty = (NotchCenterY - BgBorder) / middleH;
            // anchor fraction measured from the left / top; offset = border * (1 - 2t)
            Vector2 anchor = new Vector2(tx, 1f - ty);
            Vector2 offset = new Vector2(BgBorder * (1f - 2f * tx), -BgBorder * (1f - 2f * ty));
            Button btn = CreateButton(root, "CloseButton", LoadUI("CloseBtn"), null, null,
                anchor, anchor, new Vector2(0.5f, 0.5f), offset, new Vector2(CloseButtonWidth, CloseButtonHeight));
            return btn;
        }

        /// <summary>A small caption label pinned to the panel at a given depth (x=60 from the
        /// panel's own left edge by default). Callers leave >=60px before the control that
        /// follows it so nothing overlaps.</summary>
        private static void LabelAt(Transform panel, string text, float y, string locKey = null, float? x = null)
        {
            float resolvedX = x ?? 60f;
            Vector2 anchor = x.HasValue ? new Vector2(0.5f, 1) : new Vector2(0, 1);
            CreateText(panel, $"Label_{text}", text, 28, TextAlignmentOptions.MidlineLeft, TitleTeal,
                anchor, anchor, new Vector2(0, 1), new Vector2(resolvedX, y), new Vector2(500, 44), locKey);
        }
    }
}
