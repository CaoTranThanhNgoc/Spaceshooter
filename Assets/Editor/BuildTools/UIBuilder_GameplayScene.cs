using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using SpaceHawk.Gameplay;
using SpaceHawk.UI;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds Assets/Scenes/Gameplay.unity: camera, the 4 parallax background sets,
    /// the player/enemy/meteor spawners wired to the gameplay prefabs, and the in-level HUD.</summary>
    public static class UIBuilder_GameplayScene
    {
        private const string ScenePath = "Assets/Scenes/Gameplay.unity";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);

        [MenuItem("Tools/Space Hawk/6. Build Gameplay Scene")]
        public static void Build()
        {
            GameplayPrefabBuilder.BuiltPrefabs prefabs = GameplayPrefabBuilder.Build();
            Build(prefabs);
        }

        public static void Build(GameplayPrefabBuilder.BuiltPrefabs prefabs)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            ParallaxBackgroundController parallax = BuildParallaxBackground();
            SceneBuilderUtil.CreateEventSystem();

            PlayerShip player = BuildPlayer(prefabs.player);
            EnemySpawner spawner = BuildSpawner(prefabs.enemyLights, prefabs.enemyHeavys, prefabs.enemyBoss);
            MeteorSpawner meteorSpawner = BuildMeteorSpawner(prefabs.meteors);

            Canvas hudCanvas = SceneBuilderUtil.CreateCanvas("HUDCanvas");
            HUDController hud = BuildHUD(hudCanvas.transform);

            GameObject overlayRoot = NewUI("OverlayRoot", hudCanvas.transform);
            Stretch(RT(overlayRoot));
            overlayRoot.transform.SetAsLastSibling();

            LevelIntroBanner introBanner = BuildLevelIntroBanner(hudCanvas.transform);
            introBanner.transform.SetAsLastSibling();

            MoveTutorialHint moveHint = BuildMoveTutorialHint(hudCanvas.transform, player);
            moveHint.transform.SetAsLastSibling();

            GameObject controllerGO = new GameObject("GameplayController");
            GameplayController controller = controllerGO.AddComponent<GameplayController>();
            controller.player = player;
            controller.spawner = spawner;
            controller.hud = hud;
            controller.overlayRoot = overlayRoot.transform;
            controller.parallaxBackground = parallax;
            controller.introBanner = introBanner;
            controller.moveTutorialHint = moveHint;
            controller.meteorSpawner = meteorSpawner;

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[UIBuilder_GameplayScene] Gameplay scene built.");
        }

        private static void BuildCamera()
        {
            GameObject camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            Camera cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.02f, 0.05f, 0.08f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGO.AddComponent<CameraShake>();
        }

        private struct LayerDef
        {
            public string file;
            public float speed;
            public int order;

            public LayerDef(string file, float speed, int order)
            {
                this.file = file;
                this.speed = speed;
                this.order = order;
            }
        }

        private static ParallaxBackgroundController BuildParallaxBackground()
        {
            GameObject root = new GameObject("ParallaxRoot");
            ParallaxBackgroundController controller = root.AddComponent<ParallaxBackgroundController>();

            string[] setNames = { "Space_BG_01", "Space_BG_02", "Space_BG_03", "Space_BG_04" };
            LayerDef[] layers =
            {
                new LayerDef("BG", 0.15f, -100),
                new LayerDef("Stars", 0.4f, -90),
                new LayerDef("Planets", 0.7f, -80),
                new LayerDef("Meteors", 1.3f, -70),
            };

            GameObject[] sets = new GameObject[setNames.Length];
            for (int s = 0; s < setNames.Length; s++)
            {
                GameObject setGO = new GameObject(setNames[s]);
                setGO.transform.SetParent(root.transform, false);

                foreach (LayerDef layer in layers)
                {
                    Sprite sprite = LoadShip($"Background/{setNames[s]}/Layers", layer.file);
                    GameObject layerGO = new GameObject(layer.file, typeof(SpriteRenderer));
                    layerGO.transform.SetParent(setGO.transform, false);
                    layerGO.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

                    ParallaxLayer pl = layerGO.AddComponent<ParallaxLayer>();
                    pl.sprite = sprite;
                    pl.scrollSpeed = layer.speed;
                    pl.sortingOrder = layer.order;
                }

                sets[s] = setGO;
            }

            controller.sets = sets;
            controller.ActivateSet(0);
            return controller;
        }

        private static PlayerShip BuildPlayer(GameObject playerPrefab)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            instance.transform.position = new Vector3(0f, -3f, 0f);
            return instance.GetComponent<PlayerShip>();
        }

        private static EnemySpawner BuildSpawner(GameObject[] lightPrefabs, GameObject[] heavyPrefabs, GameObject bossPrefab)
        {
            GameObject go = new GameObject("EnemySpawner", typeof(EnemySpawner));
            EnemySpawner spawner = go.GetComponent<EnemySpawner>();
            spawner.lightEnemyPrefabs = System.Array.ConvertAll(lightPrefabs, p => p.GetComponent<Enemy>());
            spawner.heavyEnemyPrefabs = System.Array.ConvertAll(heavyPrefabs, p => p.GetComponent<Enemy>());
            spawner.bossEnemyPrefab = bossPrefab.GetComponent<Enemy>();

            Transform[] points = new Transform[4];
            float[] xs = { -3f, -1f, 1f, 3f };
            for (int i = 0; i < xs.Length; i++)
            {
                GameObject p = new GameObject($"SpawnPoint{i}");
                p.transform.SetParent(go.transform);
                p.transform.position = new Vector3(xs[i], 6f, 0f);
                points[i] = p.transform;
            }
            spawner.spawnPoints = points;

            return spawner;
        }

        private static MeteorSpawner BuildMeteorSpawner(GameObject[] meteorPrefabs)
        {
            GameObject go = new GameObject("MeteorSpawner", typeof(MeteorSpawner));
            MeteorSpawner spawner = go.GetComponent<MeteorSpawner>();
            spawner.meteorPrefabs = meteorPrefabs;
            return spawner;
        }

        // Dark teal-navy, in the same family as the rest of the game's UI (health bar, panel
        // captions, HUD accents) instead of the purple tried before - distinct enough from the
        // starfield backdrop to read as an actual solid door, without introducing an off-theme hue.
        private static readonly Color DoorColor = new Color(0.04f, 0.15f, 0.19f, 1f);
        private static readonly Color DoorTrimColor = new Color(1f, 0.83f, 0.2f, 0.9f);

        /// <summary>Full-width "blast doors" level-start transition - see LevelIntroBanner for the
        /// slide/fade sequence this only lays out. Built inactive; GameplayController calls
        /// LevelIntroBanner.Show(...) once it knows the level's actual numbers, and holds off
        /// starting enemy spawns until the whole sequence (doors closed -> text -> doors open)
        /// finishes, so the fight never starts hidden behind or racing this.</summary>
        private static LevelIntroBanner BuildLevelIntroBanner(Transform canvasT)
        {
            GameObject root = NewUI("LevelIntroBanner", canvasT);
            Stretch(RT(root));

            Image leftDoor = CreateImage(root.transform, "LeftDoor", null,
                new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            leftDoor.color = DoorColor;
            BuildDoorTrim(leftDoor.transform);

            Image rightDoor = CreateImage(root.transform, "RightDoor", null,
                new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rightDoor.color = DoorColor;
            BuildDoorTrim(rightDoor.transform);

            GameObject textGO = NewUI("Text", root.transform);
            Anchor(RT(textGO), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 260));
            CanvasGroup textGroup = textGO.AddComponent<CanvasGroup>();
            textGroup.interactable = false;
            textGroup.blocksRaycasts = false;

            Image titleBg = CreateImage(textGO.transform, "TitleBg", LoadUI("TitleBox"),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(640, 130), Image.Type.Sliced);
            TMP_Text titleLabel = CreateText(titleBg.transform, "Label", "LEVEL 1", 54, TextAlignmentOptions.Center, Color.white,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            titleLabel.outlineWidth = 0.25f;
            titleLabel.outlineColor = new Color(0.1f, 0.6f, 0.65f, 1f);
            titleLabel.fontStyle = FontStyles.Bold;

            // Small flanking diamonds - cheap way to read as "decorated banner" rather than a
            // plain text box, since the sprite pack has no dedicated ornament asset for this.
            BuildDiamond(titleBg.transform, -370f);
            BuildDiamond(titleBg.transform, 370f);

            Image objectiveBg = CreateImage(textGO.transform, "ObjectiveBg", LoadUI("BgHudBar"),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(780, 100), Image.Type.Sliced);
            objectiveBg.color = new Color(0.05f, 0.16f, 0.2f, 0.92f);

            Image objectiveIcon = CreateImage(objectiveBg.transform, "Icon", UIFactory.LoadShip("Boss/Boss_01", "Boss_Full"),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(84, 84));
            objectiveIcon.preserveAspect = true;

            TMP_Text objectiveLabel = CreateText(objectiveBg.transform, "Label", "", 32, TextAlignmentOptions.Center, DoorTrimColor,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(50, 0), new Vector2(-140, 0));
            objectiveLabel.outlineWidth = 0.2f;
            objectiveLabel.outlineColor = Color.black;
            objectiveLabel.fontStyle = FontStyles.Bold;

            LevelIntroBanner banner = root.AddComponent<LevelIntroBanner>();
            banner.leftDoor = RT(leftDoor.gameObject);
            banner.rightDoor = RT(rightDoor.gameObject);
            banner.textGroup = textGroup;
            banner.titleLabel = titleLabel;
            banner.objectiveLabel = objectiveLabel;
            root.SetActive(false);
            return banner;
        }

        /// <summary>Thin gold trim along a door's own top/bottom edges, so it reads as an actual
        /// constructed panel instead of a flat color rectangle - no seam accent at the inner edge
        /// this time (a glow band + bright line there previously read as one big stripe bisecting
        /// the screen, not a pair of doors).</summary>
        private static void BuildDoorTrim(Transform door)
        {
            CreateImage(door, "TrimTop", null, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 6)).color = DoorTrimColor;
            CreateImage(door, "TrimBottom", null, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 6)).color = DoorTrimColor;
        }

        private static void BuildDiamond(Transform parent, float x)
        {
            Image diamond = CreateImage(parent, "Diamond", null,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(18, 18));
            diamond.color = DoorTrimColor;
            diamond.rectTransform.localEulerAngles = new Vector3(0, 0, 45f);
        }

        /// <summary>Shown once ever (see MoveTutorialHint/SaveManager.HasSeenMoveTutorial) right
        /// after the level-intro doors open - a pulsing ring near where the ship actually spawns
        /// (world (0,-3,0) - see BuildPlayer) plus a plain-language label, since the touch-drag
        /// control scheme is otherwise never explained anywhere in the game.</summary>
        private static MoveTutorialHint BuildMoveTutorialHint(Transform canvasT, PlayerShip player)
        {
            GameObject root = NewUI("MoveTutorialHint", canvasT);
            Anchor(RT(root), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(500, 170));
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            Image circle = CreateImage(root.transform, "PulseCircle", LoadUI("Achievement_BlueDot"),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(120, 120));
            circle.color = new Color(1f, 1f, 1f, 0.9f);

            TMP_Text label = CreateText(root.transform, "Label", "DRAG TO MOVE YOUR SHIP", 26, TextAlignmentOptions.Center, Color.white,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(480, 44), "tutorial.drag_to_move");
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;

            MoveTutorialHint hint = root.AddComponent<MoveTutorialHint>();
            hint.group = group;
            hint.pulseCircle = circle.rectTransform;
            hint.label = label;
            hint.player = player;
            root.SetActive(false);
            return hint;
        }

        private static HUDController BuildHUD(Transform canvasT)
        {
            // First child so every other HUD element draws on top of it - a full-screen red pulse
            // once HP drops critical, transparent (alpha 0) the rest of the time.
            Image lowHealthOverlay = CreateStretchImage(canvasT, "LowHealthOverlay", null);
            lowHealthOverlay.color = new Color(1f, 0.15f, 0.15f, 0f);
            lowHealthOverlay.raycastTarget = false;

            // HealthBar_Line is the asset pack's own dedicated health-bar frame (paired with the
            // HealthBar fill sprite below) - BgHudBar is a generic HUD bar reused everywhere else
            // (kills bar, inventory stats), not specific to health. Its declared 9-slice border is
            // 10px sides / 8px top-bottom (see UISpriteImportSettings), so the fill is inset to
            // exactly that instead of the larger margins BgHudBar's own border happened to want.
            GameObject frame = NewUI("HealthBarFrame", canvasT);
            Anchor(RT(frame), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -50), new Vector2(HudColumnWidth, 50));
            Image frameImg = frame.AddComponent<Image>();
            frameImg.sprite = LoadUI("HealthBar_Line");
            frameImg.type = Image.Type.Sliced;

            GameObject fill = NewUI("HealthFill", frame.transform);
            Anchor(RT(fill), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(256, 34));
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = LoadUI("HealthBar");
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;

            // Win-condition readout, right next to the health bar it shares a row with - a mini
            // gauge in the same frame+fill language as the health bar instead of a bare line of
            // text, with a small enemy-ship icon so what the numbers mean reads at a glance.
            (TMP_Text killsLabel, Image killsFill) = BuildKillsBar(canvasT);

            TMP_Text levelLabel = CreateText(canvasT, "LevelLabel", "LEVEL 1", 30, TextAlignmentOptions.Center, BodyLight,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(400, 50));

            TMP_Text scoreLabel = CreateText(canvasT, "ScoreLabel", "SCORE 0", 22, TextAlignmentOptions.Center, new Color(1f, 0.83f, 0.2f, 1f),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(400, 36));
            scoreLabel.outlineWidth = 0.2f;
            scoreLabel.outlineColor = Color.black;

            // Size was Vector2.zero - CreateButton (unlike CreateImage) never calls SetNativeSize,
            // so this left the button's RectTransform at literal 0x0: invisible and unclickable
            // (the exact same bug found and fixed on Level Select's and Auth Gate's own icon
            // buttons). Explicit 96x96 matches those.
            Button pauseBtn = CreateButton(canvasT, "PauseButton", LoadUI("Btn_Menu_Normal"), LoadUI("Btn_Menu_Hover"), LoadUI("Btn_Menu_Disable"),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -50), new Vector2(96, 96));
            pauseBtn.gameObject.AddComponent<SafeAreaCorner>();

            BuffIconSlot[] buffIcons = BuildBuffIcons(canvasT);
            (GameObject bossBarRoot, Image bossBarFill) = BuildBossBar(canvasT);

            // Endless mode's big "WAVE N" caption - hidden until HUDController flashes it as each
            // wave begins, centred well above the ship's playfield.
            TMP_Text waveBanner = CreateText(canvasT, "WaveBanner", "WAVE 1", 84, TextAlignmentOptions.Center, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1200, 130));
            waveBanner.fontStyle = FontStyles.Bold;
            waveBanner.outlineWidth = 0.25f;
            waveBanner.outlineColor = Color.black;
            waveBanner.gameObject.SetActive(false);

            HUDController hud = canvasT.gameObject.AddComponent<HUDController>();
            hud.healthFill = fillImg;
            hud.levelLabel = levelLabel;
            hud.scoreLabel = scoreLabel;
            hud.killsLabel = killsLabel;
            hud.killsFill = killsFill;
            hud.pauseButton = pauseBtn;
            hud.buffIcons = buffIcons;
            hud.bossBarRoot = bossBarRoot;
            hud.bossBarFill = bossBarFill;
            hud.lowHealthOverlay = lowHealthOverlay;
            hud.waveBannerLabel = waveBanner;
            return hud;
        }

        // Same width as HealthBarFrame (280) so the two stacked bars line up flush instead of
        // one poking out past the other.
        private const float HudColumnWidth = 280f;

        /// <summary>Mini gauge for the level's kill quota: frame + enemy-ship icon + a fill bar
        /// that creeps up toward requiredKillRatio, with the raw "X/Y" count overlaid on the fill.
        /// Sits in the top-left corner directly under the health bar (same column width, stacked
        /// as its own row) - sharing the health bar's own row felt cramped, and the bottom of the
        /// screen felt out of place for something read at a glance during play.</summary>
        private static (TMP_Text label, Image fill) BuildKillsBar(Transform canvasT)
        {
            GameObject root = NewUI("KillsBar", canvasT);
            Anchor(RT(root), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -110), new Vector2(HudColumnWidth, 50));
            Image frame = root.AddComponent<Image>();
            frame.sprite = LoadUI("BgHudBar");
            frame.type = Image.Type.Sliced;

            Image icon = CreateImage(root.transform, "Icon", UIFactory.LoadShip("Boss/Boss_01", "Boss_Full"),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(38, 38));
            icon.preserveAspect = true;

            Image fill = CreateImage(root.transform, "Fill", LoadUI("HealthBar"),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(HudColumnWidth - 66, 24));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(1f, 0.55f, 0.25f, 1f);

            TMP_Text label = CreateText(root.transform, "Label", "0/0", 18, TextAlignmentOptions.Center, Color.white,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(HudColumnWidth - 66, 36));
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;

            return (label, fill);
        }

        /// <summary>Wide bar across the very top of the screen, hidden until HUDController shows
        /// it on Enemy.BossSpawned - sits directly above LevelLabel with no gap so it reads as
        /// this fight's own dedicated readout rather than fighting the level/score text for space.</summary>
        private static (GameObject root, Image fill) BuildBossBar(Transform canvasT)
        {
            GameObject root = NewUI("BossHealthBar", canvasT);
            Anchor(RT(root), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -15), new Vector2(700, 28));

            // Same HealthBar_Line + HealthBar pairing as the player's own health bar (was
            // BgHudBar, a generic frame reused everywhere) - same concept/sprites for both, this
            // bar just stays its own wide top-screen shape rather than matching pixel dimensions.
            Image frame = root.AddComponent<Image>();
            frame.sprite = LoadUI("HealthBar_Line");
            frame.type = Image.Type.Sliced;

            // Inset to HealthBar_Line's declared border (10px sides, 8px top/bottom) at this
            // frame's native 28px height, so the fill sits flush inside it instead of overflowing
            // past the frame's own inner edge the way the old 16px-tall fill did.
            Image fill = CreateImage(root.transform, "Fill", LoadUI("HealthBar"),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(676, 12));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(1f, 0.35f, 0.3f, 1f);

            // Overlaid centered on top of the fill (not placed above the bar) - this bar already
            // sits flush against the screen's very top edge, so anything above it risks going into
            // unsafe/notch territory on real devices.
            TMP_Text label = CreateText(root.transform, "Label", "BOSS", 16, TextAlignmentOptions.Center, Color.white,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;

            root.SetActive(false);
            return (root, fill);
        }

        /// <summary>Small row of icons under the health bar, one per timed power-up
        /// (Damage/Rockets/Barrier/Armor/Magnet from Bonus_Items) - hidden until HUDController
        /// activates them while that buff is running, with a countdown overlay.</summary>
        private static BuffIconSlot[] BuildBuffIcons(Transform canvasT)
        {
            (string folder, string sprite, PowerUpType type, Color tint)[] defs =
            {
                ("Bonus_Items", "Damage_Bonus", PowerUpType.Damage, Color.white),
                ("Bonus_Items", "Rockets_Bonus", PowerUpType.Rockets, Color.white),
                ("Bonus_Items", "Barrier_Bonus", PowerUpType.Barrier, Color.white),
                ("Bonus_Items", "Armor_Bonus", PowerUpType.Armor, Color.white),
                ("Bonus_Items", "Magnet_Bonus", PowerUpType.Magnet, Color.white),
                ("Bonus_Items", "Enemy_Speed_Debuff", PowerUpType.SlowEnemies, Color.white),
                // No dedicated 5-way-spread icon art exists - reuse the Rockets icon tinted green
                // so it still reads as a distinct buff at a glance rather than a duplicate.
                ("Bonus_Items", "Rockets_Bonus", PowerUpType.Spread, new Color(0.5f, 1f, 0.4f, 1f)),
                ("Weapons", "ray_start", PowerUpType.Beam, new Color(1f, 0.22f, 0.16f, 1f)),
            };

            BuffIconSlot[] slots = new BuffIconSlot[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                // Starting layout only - HUDController.Update() repacks whichever ones are
                // actually active left-to-right at runtime, closing gaps left by inactive slots.
                // Y sits below the health bar AND the KillsBar row now stacked under it.
                float x = HUDController.BuffIconBaseX + i * (HUDController.BuffIconSize + HUDController.BuffIconSpacing);
                GameObject slot = NewUI("Buff_" + defs[i].type, canvasT);
                Anchor(RT(slot), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -174), new Vector2(HUDController.BuffIconSize, HUDController.BuffIconSize));

                Image icon = slot.AddComponent<Image>();
                icon.sprite = UIFactory.LoadShip(defs[i].folder, defs[i].sprite);
                icon.preserveAspect = true;
                icon.color = defs[i].tint;

                TMP_Text label = CreateText(slot.transform, "Label", "0", 20, TextAlignmentOptions.Center, Color.white,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                label.outlineWidth = 0.25f;
                label.outlineColor = Color.black;

                slot.SetActive(false);
                slots[i] = new BuffIconSlot { type = defs[i].type, root = slot, label = label };
            }

            return slots;
        }
    }
}
