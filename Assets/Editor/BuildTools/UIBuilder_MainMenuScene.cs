using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using SpaceHawk.UI;
using SpaceHawk.Data;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds Assets/Scenes/MainMenu.unity: one Canvas holding the Landing screen and the
    /// Level Select screen as sibling panels, switched by MenuUIRoot (no separate scene load
    /// between them, only Gameplay is a real scene transition).</summary>
    public static class UIBuilder_MainMenuScene
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private static readonly Color TitleTeal = new Color(0.63f, 0.93f, 0.93f, 1f);
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.83f, 0.2f, 1f);

        [MenuItem("Tools/Space Hawk/5. Build MainMenu Scene")]
        public static void Build()
        {
            Build(AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Resources/Data/LevelDatabase.asset"));
        }

        public static void Build(LevelDatabase database)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SceneBuilderUtil.CreateEventSystem();
            Canvas canvas = SceneBuilderUtil.CreateCanvas("Canvas");

            GameObject landing = BuildLandingPanel(canvas.transform, out MainMenuUI menuUI);
            GameObject levelSelect = BuildLevelSelectPanel(canvas.transform, database, out LevelSelectUI levelSelectUI);

            GameObject overlayRoot = NewUI("OverlayRoot", canvas.transform);
            Stretch(RT(overlayRoot));
            overlayRoot.transform.SetAsLastSibling();

            // Topmost so it covers the Landing/Level Select panels underneath while it fades out -
            // softens the hard cut every time this scene loads (cold start or returning from
            // Gameplay via Menu/Level Select).
            Image fadeOverlay = CreateStretchImage(canvas.transform, "FadeOverlay", null);
            fadeOverlay.color = Color.black;
            fadeOverlay.raycastTarget = false;
            fadeOverlay.transform.SetAsLastSibling();
            SceneFadeIn fadeIn = canvas.gameObject.AddComponent<SceneFadeIn>();
            fadeIn.overlay = fadeOverlay;

            menuUI.overlayRoot = overlayRoot.transform;
            levelSelectUI.overlayRoot = overlayRoot.transform;
            levelSelectUI.toastRoot = overlayRoot.transform;

            MenuUIRoot root = canvas.gameObject.AddComponent<MenuUIRoot>();
            root.landingPanel = landing;
            root.levelSelectPanel = levelSelect;
            menuUI.menuRoot = root;

            landing.SetActive(true);
            levelSelect.SetActive(false);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[UIBuilder_MainMenuScene] MainMenu scene built.");
        }

        // ---------------------------------------------------------------- Landing

        private static GameObject BuildLandingPanel(Transform canvasT, out MainMenuUI menuUI)
        {
            GameObject panel = NewUI("LandingPanel", canvasT);
            Stretch(RT(panel));

            CreateStretchImage(panel.transform, "Background", LoadUI("LandingScreen_Background"));
            GameObject safe = panel;

            CreateImage(safe.transform, "Logo", LoadUI("SpaceHawkLogo"),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(700, 262));

            // Box height 420 (was 340) and re-centered at y=-100 (was -40) to fit a third button
            // (QUIT) below Play/Settings with the same ~120px rhythm and ~44px padding at both
            // ends - was previously sized for exactly 2 buttons.
            CreateImage(safe.transform, "ButtonBox", LoadUI("BoxMenu"),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(560, 420), Image.Type.Sliced);

            Vector2 btnSize = new Vector2(420, 92);
            Button playBtn = CreateTextButton(safe.transform, "PlayButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "PLAY GAME", 32, BodyLight, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), btnSize, Image.Type.Sliced, "menu.play");
            Button settingsBtn = CreateTextButton(safe.transform, "SettingsButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "SETTINGS", 32, BodyLight, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), btnSize, Image.Type.Sliced, "menu.settings");
            Button quitBtn = CreateTextButton(safe.transform, "QuitButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "QUIT GAME", 32, BodyLight, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -220), btnSize, Image.Type.Sliced, "menu.quit");

            menuUI = panel.AddComponent<MainMenuUI>();
            menuUI.playButton = playBtn;
            menuUI.settingsButton = settingsBtn;
            menuUI.quitButton = quitBtn;

            return panel;
        }

        // ---------------------------------------------------------------- Level Select

        private static GameObject BuildLevelSelectPanel(Transform canvasT, LevelDatabase database, out LevelSelectUI levelSelectUI)
        {
            GameObject panel = NewUI("LevelSelectPanel", canvasT);
            Stretch(RT(panel));

            // The map (background + level nodes) scrolls horizontally across one or more pages
            // instead of being one static image - see LevelData.mapPage. The hand-illustrated
            // background art only has exactly 7 planets; cramming levels 8+ onto it either
            // overlapped new node CARDS (215x159, much bigger than a decorative planet) onto
            // existing ones or squeezed them into gaps too small for a real card.
            // Deliberately using database?.Count throughout, not a `database == null` guard -
            // LevelDatabase is a ScriptableObject, and Unity overloads == to report "fake null"
            // for some valid-but-not-yet-fully-settled objects even when the underlying C#
            // reference isn't actually null; ?. uses the real reference check and isn't fooled by
            // it. A `database == null` gate here silently skipped this whole loop, leaving
            // pageCount stuck at 1 even with levels 8-9 correctly saved at mapPage=1 on disk.
            // ReferenceEquals, not != null - LevelData is ALSO a ScriptableObject, so a freshly
            // AssetDatabase.CreateAsset'd one earlier in this same synchronous build pass (no
            // domain reload or SaveAssets+reimport in between) can be "fake null" to Unity's
            // overloaded != too, which silently skipped this check for every level and left
            // pageCount stuck at 1 even after the database?.Count fix above.
            int pageCount = 1;
            int levelCountForPaging = database?.Count ?? 0;
            for (int i = 0; i < levelCountForPaging; i++)
            {
                LevelData lvl = database.GetByIndex(i);
                if (!ReferenceEquals(lvl, null)) pageCount = Mathf.Max(pageCount, lvl.mapPage + 1);
            }

            (RectTransform mapViewport, RectTransform mapContent, RectTransform[] pageRects, Transform[] pageContainers, ScrollRect mapScrollRect) =
                BuildMapPages(panel.transform, pageCount);

            GameObject safe = panel;

            // Currency HUD, top-left (icon spans 50-106px; label starts well clear of it at 140px)
            CreateImage(safe.transform, "EnergyIcon", LoadUI("BateryIcon"),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -50), new Vector2(56, 56));
            TMP_Text energyLabel = CreateText(safe.transform, "EnergyLabel", "10/10", 28, TextAlignmentOptions.MidlineLeft, BodyLight,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, -50), new Vector2(160, 50));

            CreateImage(safe.transform, "CrystalIcon", LoadUI("CrystalIcon"),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -114), new Vector2(56, 56));
            TMP_Text crystalLabel = CreateText(safe.transform, "CrystalLabel", "0", 28, TextAlignmentOptions.MidlineLeft, Yellow,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, -114), new Vector2(160, 50));

            // Settings, top-right. Size was Vector2.zero - CreateButton (unlike CreateImage) never
            // calls SetNativeSize, so that left this button's RectTransform at literal 0x0: both
            // invisible and unclickable, not just hard to hit. Explicit 96x96 matches PlayerButton
            // right next to it below, which was already sized correctly.
            Button settingsBtn = CreateButton(safe.transform, "SettingsButton", LoadUI("Btn_Settings_Normal"), LoadUI("Btn_Settings_Hover"), LoadUI("Btn_Settings_Disable"),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -50), new Vector2(96, 96));
            settingsBtn.gameObject.AddComponent<SafeAreaCorner>();

            // Player profile, just left of Settings - icon shows whichever ship hull is currently
            // selected (see InventoryPanel), refreshed live if the player changes it. Tapping opens
            // PlayerProfilePanel, the same screen used for the very first "choose your name" prompt.
            Button playerBtn = CreateButton(safe.transform, "PlayerButton", LoadUI("Btn_Empty_Normal"), null, null,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-160, -50), new Vector2(96, 96));
            playerBtn.gameObject.AddComponent<SafeAreaCorner>();
            ShipHullSprites[] hulls = UIFactory.LoadAllShipHulls();
            // One icon per ship of the roster (ShipCatalog order: family * 5 + tier).
            Sprite[] hullIcons = new Sprite[ShipCatalog.Count];
            for (int i = 0; i < hullIcons.Length; i++) hullIcons[i] = hulls[ShipCatalog.FamilyOf(i)].levelSprites[ShipCatalog.TierOf(i)];
            Image playerIcon = CreateImage(playerBtn.transform, "Icon", hullIcons.Length > 0 ? hullIcons[0] : null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            playerIcon.preserveAspect = true;
            playerIcon.raycastTarget = false;

            // Help, third in the top-right group. It used to be the sixth bottom icon - but with
            // Endless added the bottom bar grew into the level-4 planet, and a "?" belongs next to
            // Settings anyway. No dedicated help icon exists in the sprite pack: the blank frame the
            // other icons are built on, with a plain "?" overlaid, keeps it consistent.
            Button howToPlayBtn = CreateButton(safe.transform, "HowToPlayButton", LoadUI("Btn_Empty_Normal"), LoadUI("Btn_Empty_Hover"), LoadUI("Btn_Empty_Disable"),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-270, -50), new Vector2(96, 96));
            howToPlayBtn.gameObject.AddComponent<SafeAreaCorner>();
            TMP_Text questionMark = CreateText(howToPlayBtn.transform, "QuestionMark", "?", 52, TextAlignmentOptions.Center, BodyLight,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            questionMark.outlineWidth = 0.15f;
            questionMark.outlineColor = Color.black;

            // Bottom icon bar
            // ENDLESS first: the loop arrow reads as "keeps going", and the far-left slot is the
            // most prominent one on the bar. MISSIONS carries a red counter for unclaimed rewards.
            Button endlessBtn = CreateBottomIcon(safe.transform, "EndlessButton", "Btn_Restart_Normal", 0, "ENDLESS", "levelselect.endless");
            Button missionsBtn = CreateBottomIcon(safe.transform, "MissionsButton", "Btn_Menu_Normal", 1, "MISSIONS", "levelselect.missions");
            Image badgeImage = CreateImage(missionsBtn.transform, "Badge", null,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-4, -8), new Vector2(40, 40));
            badgeImage.color = new Color(0.92f, 0.2f, 0.2f, 1f);
            TMP_Text badgeLabel = CreateText(badgeImage.transform, "Count", "1", 22, TextAlignmentOptions.Center, Color.white,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            badgeLabel.fontStyle = FontStyles.Bold;
            Button inventoryBtn = CreateBottomIcon(safe.transform, "InventoryButton", "Btn_inventory", 2, "INVENTORY", "levelselect.inventory");
            Button achievementsBtn = CreateBottomIcon(safe.transform, "AchievementsButton", "Btn_Achievement", 3, "ACHIEVEMENTS", "levelselect.achievements");
            Button leaderboardBtn = CreateBottomIcon(safe.transform, "LeaderboardButton", "Btn_Leaderboard", 4, "LEADERBOARD", "levelselect.leaderboard");

            GameObject nodePrefab = BuildLevelNodePrefab();

            levelSelectUI = panel.AddComponent<LevelSelectUI>();
            levelSelectUI.database = database;
            levelSelectUI.nodePrefab = nodePrefab.GetComponent<LevelNodeView>();
            levelSelectUI.pageContainers = pageContainers;
            levelSelectUI.pageRects = pageRects;
            levelSelectUI.mapViewport = mapViewport;
            levelSelectUI.mapContent = mapContent;
            levelSelectUI.mapScrollRect = mapScrollRect;
            levelSelectUI.energyLabel = energyLabel;
            levelSelectUI.crystalLabel = crystalLabel;
            levelSelectUI.settingsButton = settingsBtn;
            levelSelectUI.missionsButton = missionsBtn;
            levelSelectUI.missionsBadge = badgeImage.gameObject;
            levelSelectUI.missionsBadgeImage = badgeImage;
            levelSelectUI.missionsBadgeLabel = badgeLabel;
            levelSelectUI.endlessButton = endlessBtn;
            levelSelectUI.inventoryButton = inventoryBtn;
            levelSelectUI.achievementsButton = achievementsBtn;
            levelSelectUI.leaderboardButton = leaderboardBtn;
            levelSelectUI.howToPlayButton = howToPlayBtn;
            levelSelectUI.playerButton = playerBtn;
            levelSelectUI.playerIcon = playerIcon;
            levelSelectUI.hullIcons = hullIcons;

            return panel;
        }

        // Guessed page width for the BUILD-TIME layout only - LevelSelectUI.LayoutMapPages
        // resizes every page (and MapContent as a whole) to the viewport's actual measured width
        // the first time this screen opens, so the real device's aspect ratio is what ultimately
        // decides page width, not this constant. It only needs to be a reasonable starting size
        // so the scene doesn't look broken if something inspects it before Play.
        private const float MapPageWidthGuess = 1920f;

        /// <summary>Builds the horizontally-scrolling map: one ScrollRect viewport, one wide
        /// Content, and `pageCount` pages inside it (each its own copy of LevelSelect_Background
        /// plus its own node container) laid out left to right. Returns the pieces LevelSelectUI
        /// needs to populate nodes into the right page and keep pages exactly one viewport wide.</summary>
        private static (RectTransform viewport, RectTransform content, RectTransform[] pageRects, Transform[] pageContainers, ScrollRect scrollRect)
            BuildMapPages(Transform panelT, int pageCount)
        {
            GameObject viewportGo = NewUI("MapViewport", panelT);
            Stretch(RT(viewportGo));
            viewportGo.transform.SetAsFirstSibling(); // behind the currency/settings/bottom-bar HUD
            viewportGo.AddComponent<RectMask2D>();
            // Transparent but raycastable - lets ScrollRect catch drags over the empty space
            // between planets, not just on top of the opaque background art.
            Image viewportBg = viewportGo.AddComponent<Image>();
            viewportBg.color = new Color(0f, 0f, 0f, 0f);
            viewportBg.raycastTarget = true;

            GameObject contentGo = NewUI("MapContent", viewportGo.transform);
            Anchor(RT(contentGo), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(MapPageWidthGuess * pageCount, 0));

            RectTransform[] pageRects = new RectTransform[pageCount];
            Transform[] pageContainers = new Transform[pageCount];
            for (int i = 0; i < pageCount; i++)
            {
                GameObject pageGo = NewUI($"Page{i}", contentGo.transform);
                Anchor(RT(pageGo), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(MapPageWidthGuess * i, 0), new Vector2(MapPageWidthGuess, 0));
                pageRects[i] = RT(pageGo);

                CreateStretchImage(pageGo.transform, "Background", LoadUI("LevelSelect_Background"));

                GameObject nodesGo = NewUI("NodesContainer", pageGo.transform);
                Stretch(RT(nodesGo));
                pageContainers[i] = nodesGo.transform;
            }

            ScrollRect scrollRect = viewportGo.AddComponent<ScrollRect>();
            scrollRect.viewport = RT(viewportGo);
            scrollRect.content = RT(contentGo);
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.scrollSensitivity = 28f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.15f;

            return (RT(viewportGo), RT(contentGo), pageRects, pageContainers, scrollRect);
        }

        private static Button CreateBottomIcon(Transform parent, string name, string spriteBase, int slotIndex, string caption, string locKey = null)
        {
            float x = 70 + slotIndex * 175f;

            // Icon + caption share ONE SafeAreaCorner-adjusted slot container instead of each
            // getting its own component - putting the component on the button alone (as before)
            // shifted the icon off the safe-area edge but left its caption text behind at the
            // original build-time position, so the two drifted apart on a device whose real
            // safe area differs from the reference (exactly the "icon and text far apart,
            // misaligned" bug reported). Moving the shared PARENT guarantees they can't separate.
            GameObject slot = NewUI(name + "Slot", parent);
            Anchor(RT(slot), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 0), new Vector2(160, 116));
            slot.AddComponent<SafeAreaCorner>();

            Sprite normal = LoadUI(spriteBase);
            Button btn = CreateButton(slot.transform, name, normal, null, null,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 78), new Vector2(110, 116));
            CreateText(slot.transform, name + "Caption", caption, 17, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(55, 30), new Vector2(160, 32), locKey);
            return btn;
        }

        private static GameObject BuildLevelNodePrefab()
        {
            GameObject root = NewUI("LevelNode", null);
            Anchor(RT(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(215, 159));

            Image bg = root.AddComponent<Image>();
            bg.sprite = LoadUI("Lvl0Star");
            bg.preserveAspect = true;
            bg.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;

            // The ribbon band on Lvl0-3Star sits just below the sprite's vertical middle.
            TMP_Text numberLabel = CreateText(root.transform, "Number", "1", 34, TextAlignmentOptions.Center, new Color(0.15f, 0.2f, 0.25f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -9), new Vector2(130, 50));

            LevelNodeView view = root.AddComponent<LevelNodeView>();
            view.button = button;
            view.background = bg;
            view.numberLabel = numberLabel;
            view.starBackgrounds = new[]
            {
                LoadUI("Lvl0Star"), LoadUI("Lvl1Star"), LoadUI("Lvl2Star"), LoadUI("Lvl3Star")
            };
            view.lockedBackground = LoadUI("LvlLock");

            return SceneBuilderUtil.SaveAsPrefab(root, "Assets/Prefabs/UI/LevelNode.prefab");
        }
    }
}
