using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using SpaceHawk.Core;
using SpaceHawk.UI;

namespace SpaceHawk.Tests.PlayMode
{
    /// <summary>Not a regression test: renders the real overlay prefabs (and the main menu scene)
    /// at several screen shapes in both languages and writes PNGs to a folder for eyeballing the
    /// layout. [Explicit], so a normal test run skips it - run it by name from the Test Runner, or
    /// headless with -testFilter "UIScreenshotTests", WITHOUT -nographics (it needs a GPU to draw).
    /// Output folder: the SPACEHAWK_SHOT_DIR environment variable, else &lt;project&gt;/UIShots.</summary>
    [Explicit("Renders PNG snapshots of the UI for visual review; needs a graphics device.")]
    public class UIScreenshotTests
    {
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1920, 1080), // standard 16:9 phone / reference
            new Vector2Int(2532, 1170), // tall modern phone (the Device Simulator's iPhone)
            new Vector2Int(1440, 1080), // 4:3 tablet - the narrowest the layouts must survive
        };

        private static string OutDir
        {
            get
            {
                string dir = System.Environment.GetEnvironmentVariable("SPACEHAWK_SHOT_DIR");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "..", "UIShots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            DailyMissions.Clock = () => System.DateTime.Now;
            Localization.SetLanguage(Language.English);
            Time.timeScale = 1f;
        }

        private class Rig
        {
            public Camera camera;
            public Canvas canvas;
            public RenderTexture target;
            public GameObject root;
        }

        private static Rig BuildRig(Vector2Int size)
        {
            Rig rig = new Rig { root = new GameObject("ShotRig") };

            rig.target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            GameObject camGo = new GameObject("ShotCamera");
            camGo.transform.SetParent(rig.root.transform);
            rig.camera = camGo.AddComponent<Camera>();
            rig.camera.clearFlags = CameraClearFlags.SolidColor;
            rig.camera.backgroundColor = new Color(0.05f, 0.08f, 0.2f, 1f);
            rig.camera.orthographic = true;
            rig.camera.targetTexture = rig.target;
            rig.camera.enabled = false;

            GameObject canvasGo = new GameObject("ShotCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(rig.root.transform);
            rig.canvas = canvasGo.AddComponent<Canvas>();
            ConfigureCanvas(rig.canvas, rig.camera);
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return rig;
        }

        private static void ConfigureCanvas(Canvas canvas, Camera camera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
        }

        private static void Save(Rig rig, string name)
        {
            Canvas.ForceUpdateCanvases();
            rig.camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rig.target;
            Texture2D tex = new Texture2D(rig.target.width, rig.target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rig.target.width, rig.target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        private static void Cleanup(Rig rig)
        {
            rig.camera.targetTexture = null;
            rig.target.Release();
            Object.Destroy(rig.root);
        }

        private delegate void Prepare(GameObject instance);

        private static IEnumerator ShootPrefab(string prefabName, Prepare prepare, string tag = "")
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + prefabName);
            Assert.IsNotNull(prefab, prefabName + " prefab missing");

            foreach (Language language in new[] { Language.English, Language.Vietnamese })
            {
                foreach (Vector2Int screen in Screens)
                {
                    Localization.SetLanguage(language);
                    Rig rig = BuildRig(screen);
                    GameObject instance = Object.Instantiate(prefab, rig.canvas.transform);
                    yield return null;
                    prepare?.Invoke(instance);
                    yield return null;
                    yield return null;

                    Save(rig, $"{prefabName}{tag}_{(language == Language.English ? "en" : "vi")}_{screen.x}x{screen.y}");
                    Cleanup(rig);
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator GameOver_WithRevive()
        {
            yield return ShootPrefab("GameOverPanel", go =>
            {
                GameOverPanel panel = go.GetComponent<GameOverPanel>();
                panel.SetReason(Localization.Get("gameover.reason_defeated"));
                panel.SetReviveOption(true, null);
            }, "_revive");
        }

        [UnityTest]
        public IEnumerator GameOver_NoRevive()
        {
            yield return ShootPrefab("GameOverPanel", go =>
            {
                GameOverPanel panel = go.GetComponent<GameOverPanel>();
                panel.SetReason(Localization.Get("gameover.reason_not_enough_kills"));
                panel.SetReviveOption(false, null);
            }, "_norevive");
        }

        [UnityTest]
        public IEnumerator Victory()
        {
            yield return ShootPrefab("VictoryPanel", go =>
            {
                VictoryPanel panel = go.GetComponent<VictoryPanel>();
                panel.SetStars(3);
                panel.SetNextLevel(null);
                panel.SetLevelNumber(3);
                panel.SetScore(new SpaceHawk.Data.LevelScoreBreakdown { killPoints = 180, healthBonus = 95, speedBonus = 58, difficulty = 1.3f, total = 433 },
                               380, 12000, 12053);
            });
        }

        [UnityTest]
        public IEnumerator Achievements()
        {
            SaveManager.Data.enemiesDestroyed = 40;
            SaveManager.Data.shipLevel = 3;
            yield return ShootPrefab("AchievementsPanel", null);
        }

        [UnityTest]
        public IEnumerator Missions()
        {
            // Mid-day state: one mission done and waiting to be claimed, one part-way, the login reward ready.
            DailyMissions.Clock = () => new System.DateTime(2026, 10, 5, 12, 0, 0);
            MissionInfo first = DailyMissions.Get(0);
            DailyMissions.Report(first.kind, first.target);
            MissionInfo second = DailyMissions.Get(1);
            DailyMissions.Report(second.kind, Mathf.Max(1, second.target / 2));
            yield return ShootPrefab("MissionsPanel", null);
        }

        [UnityTest]
        public IEnumerator GameOver_EndlessResult()
        {
            yield return ShootPrefab("GameOverPanel", go =>
            {
                GameOverPanel panel = go.GetComponent<GameOverPanel>();
                panel.SetReason(Localization.Format("endless.result_fmt", 12, 1840) + "\n" +
                                Localization.Get("endless.new_best") + "   " + Localization.Format("endless.reward_fmt", 55));
                panel.SetReviveOption(true, null);
            }, "_endless");
        }

        [UnityTest]
        public IEnumerator EnergyDialog()
        {
            yield return ShootPrefab("EnergyRefillDialog", null);
        }

        [UnityTest]
        public IEnumerator Settings()
        {
            yield return ShootPrefab("SettingsPanel", null);
        }

        /// <summary>A returning player half way through the campaign: Hawk I-III and Viper I-II owned,
        /// wearing Hawk III, mid upgrades.</summary>
        private static void MidGameHangar(int clearedLevels, int crystals)
        {
            SaveManager.Data.crystals = crystals;
            SaveManager.Data.shipLevel = 3;
            SaveManager.Data.highestUnlockedLevelIndex = clearedLevels;
            foreach (int owned in new[] { 0, 1, 2, 5, 6 }) SaveManager.Data.ownedShips[owned] = true;
            SaveManager.Data.selectedShip = 2;
        }

        [UnityTest]
        public IEnumerator Inventory()
        {
            MidGameHangar(5, 3200);
            yield return ShootPrefab("InventoryPanel", null);
        }

        [UnityTest]
        public IEnumerator Inventory_Buyable()
        {
            // Hawk IV: the campaign gate is met and Hawk III is owned, so it can be bought.
            MidGameHangar(9, 6000);
            yield return ShootPrefab("InventoryPanel", go => go.GetComponent<InventoryPanel>().PreviewShip(3), "_buy");
        }

        [UnityTest]
        public IEnumerator Inventory_Blocked()
        {
            // Phantom III: neither Phantom II owned nor the campaign far enough.
            MidGameHangar(5, 20000);
            yield return ShootPrefab("InventoryPanel", go => go.GetComponent<InventoryPanel>().PreviewShip(12), "_blocked");
        }

        [UnityTest]
        public IEnumerator Inventory_TopShip()
        {
            // A late-game Viper V with every ability.
            MidGameHangar(13, 500);
            for (int i = 5; i < 10; i++) SaveManager.Data.ownedShips[i] = true;
            SaveManager.Data.selectedShip = 9;
            SaveManager.Data.shipLevel = 5;
            yield return ShootPrefab("InventoryPanel", null, "_top");
        }

        [UnityTest]
        public IEnumerator Leaderboard()
        {
            yield return ShootPrefab("LeaderboardPanel", null);
        }

        [UnityTest]
        public IEnumerator PlayerProfile()
        {
            SaveManager.Data.playerName = "TestPilot";
            yield return ShootPrefab("PlayerProfilePanel", null);
        }

        [UnityTest]
        public IEnumerator PlayerProfile_NameLocked()
        {
            // The weekly name lock is on: the line above the name field says when it can change again.
            SaveManager.Data.playerName = "TestPilot";
            SaveManager.SetNameChangeUnlock(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 4 * 86400 + 7 * 3600);
            yield return ShootPrefab("PlayerProfilePanel", null, "_namelocked");
        }

        [UnityTest]
        public IEnumerator PlayerProfile_FreshGuest()
        {
            // A guest nobody named yet: the game's own name is already in the field.
            SaveManager.Data.playerName = "";
            SaveManager.EnsureDefaultName();
            yield return ShootPrefab("PlayerProfilePanel", null, "_freshguest");
        }

        [UnityTest]
        public IEnumerator PlayerProfile_SignedIn()
        {
            SaveManager.Data.playerName = "TestPilot";
            SaveManager.SetAccountLinked(true, "pilot_hawk");
            yield return ShootPrefab("PlayerProfilePanel", null, "_signedin");
        }

        [UnityTest]
        public IEnumerator Login_WithRecentAccounts()
        {
            string[] names = { "nova.star", "skyrider99", "pilot_hawk" };
            foreach (string n in names) SpaceHawk.Online.AccountManager.RememberUsername(n);
            try
            {
                yield return ShootPrefab("LoginPanel", null);
            }
            finally
            {
                foreach (string n in names) SpaceHawk.Online.AccountManager.ForgetUsername(n);
            }
        }

        [UnityTest]
        public IEnumerator Login_NoRecentAccounts()
        {
            yield return ShootPrefab("LoginPanel", null, "_none");
        }

        [UnityTest]
        public IEnumerator ForgotPassword_Step1()
        {
            yield return ShootPrefab("ForgotPasswordPanel", null);
        }

        [UnityTest]
        public IEnumerator ForgotPassword_Step2()
        {
            yield return ShootPrefab("ForgotPasswordPanel", go => go.GetComponent<ForgotPasswordPanel>().OpenCodeStep("pilot@example.com"), "_step2");
        }

        [UnityTest]
        public IEnumerator RecoveryContact()
        {
            SaveManager.SetAccountLinked(true, "pilot_hawk");
            SaveManager.SetRecoveryContact("pilot@example.com");
            yield return ShootPrefab("RecoveryContactPanel", null);
        }

        [UnityTest]
        public IEnumerator Register()
        {
            yield return ShootPrefab("RegisterPanel", null);
        }

        private class QuietRecovery : SpaceHawk.Online.IRecoveryBackend
        {
            private static System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> Ok() => System.Threading.Tasks.Task.FromResult(SpaceHawk.Online.RecoveryResult.Ok);
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> SendVerification(string c) => Ok();
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> VerifyContact(string c, string code) => Ok();
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> SetContact(string u, string c) => Ok();
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> RequestReset(string u, string c) => Ok();
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> ConfirmReset(string u, string c, string code, string pw) => Ok();
            public System.Threading.Tasks.Task<SpaceHawk.Online.RecoveryResult> DeleteAccountData() => Ok();
        }

        [UnityTest]
        public IEnumerator VerifyContact()
        {
            SpaceHawk.Online.IRecoveryBackend real = SpaceHawk.Online.AccountManager.Recovery;
            SpaceHawk.Online.AccountManager.Recovery = new QuietRecovery();
            try
            {
                yield return ShootPrefab("VerifyContactPanel", go => go.GetComponent<VerifyContactPanel>().Begin("pilot@example.com", null));
            }
            finally
            {
                SpaceHawk.Online.AccountManager.Recovery = real;
            }
        }

        [UnityTest]
        public IEnumerator Toasts_LongMessages_Stacked()
        {
            // The profile screen with three notices at once, in the language whose sentences run longest.
            SaveManager.Data.playerName = "TestPilot";
            yield return ShootPrefab("PlayerProfilePanel", go =>
            {
                Transform parent = go.transform.parent;
                ToastUI.ShowToast(parent, Localization.Get("account.signing_out"));
                ToastUI.ShowToast(parent, Localization.Get("account.register_success_nocontact"));
                ToastUI.ShowToast(parent, Localization.Get("account.contact_moved"));
            }, "_toasts");
        }

        [UnityTest]
        public IEnumerator ChangePassword()
        {
            SaveManager.SetAccountLinked(true, "pilot_hawk");
            yield return ShootPrefab("ChangePasswordPanel", null);
        }

        [UnityTest]
        public IEnumerator MainMenu_LevelSelect()
        {
            // Open straight on the level map with a few levels unlocked, like a returning player.
            SaveManager.Data.highestUnlockedLevelIndex = 3;
            SaveManager.Data.accountLinked = true;
            SaveManager.Data.playerName = "TestPilot"; // otherwise the first-run auth gate covers the map
            GameManagerBridge.OpenLevelSelect();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == GameManager.MainMenuSceneName);
            yield return null;
            yield return null;

            foreach (Vector2Int screen in Screens)
            {
                RenderTexture target = new RenderTexture(screen.x, screen.y, 24, RenderTextureFormat.ARGB32);
                GameObject camGo = new GameObject("ShotCamera");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.orthographic = true;
                cam.targetTexture = target;
                cam.enabled = false;

                Canvas[] canvases = Object.FindObjectsByType<Canvas>();
                foreach (Canvas c in canvases)
                {
                    if (!c.isRootCanvas) continue;
                    ConfigureCanvas(c, cam);
                }
                yield return null;

                // The map lays its pages out against the viewport's width when it is enabled, so
                // a new screen shape needs a fresh enable - exactly what a real device does on open.
                LevelSelectUI levelSelect = Object.FindAnyObjectByType<LevelSelectUI>();
                if (levelSelect != null)
                {
                    levelSelect.enabled = false;
                    yield return null;
                    levelSelect.enabled = true;
                }
                yield return null;
                yield return null;

                Rig rig = new Rig { camera = cam, target = target, root = camGo };
                Save(rig, $"LevelSelect_{screen.x}x{screen.y}");
                Cleanup(rig);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Gameplay_Level1_WithBeamAndSpread()
        {
            yield return ShootGameplay(0, "Gameplay_L1", true);
        }

        [UnityTest]
        public IEnumerator Gameplay_Endless_Wave()
        {
            SaveManager.Data.highestUnlockedLevelIndex = 20;
            SaveManager.RecordEndlessRun(12, 900);
            GameManager.StartLevel(SpaceHawk.Data.EndlessLevel.GetOrCreate());
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == GameManager.GameplaySceneName);
            yield return null;

            SpaceHawk.Gameplay.PlayerShip player = Object.FindAnyObjectByType<SpaceHawk.Gameplay.PlayerShip>();
            player.GetComponent<SpaceHawk.Gameplay.Health>().SetInvincible(true);

            // Doors open, the "WAVE 1" caption flashes, enemies arrive.
            SpaceHawk.Gameplay.EnemySpawner spawner = Object.FindAnyObjectByType<SpaceHawk.Gameplay.EnemySpawner>();
            float until = Time.realtimeSinceStartup + 14f;
            while (Time.realtimeSinceStartup < until && spawner.CurrentWave < 1) yield return null;
            float settle = Time.realtimeSinceStartup + 0.9f;
            while (Time.realtimeSinceStartup < settle) yield return null;

            foreach (Vector2Int screen in new[] { Screens[0], Screens[1] })
            {
                RenderTexture target = new RenderTexture(screen.x, screen.y, 24, RenderTextureFormat.ARGB32);
                Camera cam = Camera.main;
                RenderTexture previousTarget = cam.targetTexture;
                cam.targetTexture = target;
                foreach (Canvas c in Object.FindObjectsByType<Canvas>())
                {
                    if (!c.isRootCanvas) continue;
                    bool wasOverlay = c.renderMode == RenderMode.ScreenSpaceOverlay;
                    ConfigureCanvas(c, cam);
                    if (wasOverlay) c.sortingOrder += 1000;
                }
                yield return null;
                yield return null;

                Rig rig = new Rig { camera = cam, target = target, root = null };
                Save(rig, $"Gameplay_Endless_{screen.x}x{screen.y}");
                cam.targetTexture = previousTarget;
                target.Release();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Gameplay_Level9_Plain()
        {
            yield return ShootGameplay(8, "Gameplay_L9", false);
        }

        /// <summary>Beam power-up held on a live enemy (enemies made unkillable so the splash lasts):
        /// the ship is slid under the nearest enemy each frame so the beam keeps striking it.</summary>
        [UnityTest]
        public IEnumerator Gameplay_BeamImpact()
        {
            SaveManager.Data.highestUnlockedLevelIndex = 20;
            SpaceHawk.Data.LevelDatabase db = Resources.Load<SpaceHawk.Data.LevelDatabase>("Data/LevelDatabase");
            GameManager.StartLevel(db.GetByIndex(3));
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == GameManager.GameplaySceneName);
            yield return null;

            SpaceHawk.Gameplay.PlayerShip player = Object.FindAnyObjectByType<SpaceHawk.Gameplay.PlayerShip>();
            player.GetComponent<SpaceHawk.Gameplay.Health>().SetInvincible(true);
            player.CollectPowerUp(SpaceHawk.Gameplay.PowerUpType.Beam);

            // Wait for something to fly into the beam's reach.
            float until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until && NearestEnemyAbove(player) == null) yield return null;

            foreach (Vector2Int screen in new[] { Screens[0], Screens[1] })
            {
                RenderTexture target = new RenderTexture(screen.x, screen.y, 24, RenderTextureFormat.ARGB32);
                Camera cam = Camera.main;
                RenderTexture previousTarget = cam.targetTexture;
                cam.targetTexture = target;
                foreach (Canvas c in Object.FindObjectsByType<Canvas>())
                {
                    if (!c.isRootCanvas) continue;
                    bool wasOverlay = c.renderMode == RenderMode.ScreenSpaceOverlay;
                    ConfigureCanvas(c, cam);
                    if (wasOverlay) c.sortingOrder += 1000;
                }

                for (int shot = 0; shot < 3; shot++)
                {
                    for (int f = 0; f < 9; f++)
                    {
                        foreach (SpaceHawk.Gameplay.Enemy e in Object.FindObjectsByType<SpaceHawk.Gameplay.Enemy>())
                            e.GetComponent<SpaceHawk.Gameplay.Health>().SetInvincible(true);
                        SpaceHawk.Gameplay.Enemy near = NearestEnemyAbove(player);
                        if (near != null)
                        {
                            Vector3 p = player.transform.position;
                            player.transform.position = new Vector3(near.transform.position.x, p.y, p.z);
                        }
                        yield return null;
                    }

                    Rig rig = new Rig { camera = cam, target = target, root = null };
                    Save(rig, $"Gameplay_BeamImpact{shot}_{screen.x}x{screen.y}");
                }

                cam.targetTexture = previousTarget;
                target.Release();
                yield return null;
            }
        }

        // Ships of the roster in the real Gameplay scene: sprite size, nose and engine positions, hitbox.
        [UnityTest] public IEnumerator Gameplay_Ship_HawkI() { yield return ShootGameplay(2, "Gameplay_ShipHawk1", false, 0); }
        [UnityTest] public IEnumerator Gameplay_Ship_HawkV() { yield return ShootGameplay(2, "Gameplay_ShipHawk5", false, 4); }
        [UnityTest] public IEnumerator Gameplay_Ship_ViperV() { yield return ShootGameplay(2, "Gameplay_ShipViper5", false, 9); }
        [UnityTest] public IEnumerator Gameplay_Ship_PhantomV() { yield return ShootGameplay(2, "Gameplay_ShipPhantom5", false, 14); }

        private static SpaceHawk.Gameplay.Enemy NearestEnemyAbove(SpaceHawk.Gameplay.PlayerShip player)
        {
            SpaceHawk.Gameplay.Enemy best = null;
            float bestY = float.MaxValue;
            foreach (SpaceHawk.Gameplay.Enemy e in Object.FindObjectsByType<SpaceHawk.Gameplay.Enemy>())
            {
                float y = e.transform.position.y;
                if (y < player.transform.position.y + 1.5f || y > 4.2f) continue;
                if (y < bestY) { bestY = y; best = e; }
            }
            return best;
        }

        private static IEnumerator ShootGameplay(int levelIndex, string name, bool buffs, int ship = -1)
        {
            SaveManager.Data.highestUnlockedLevelIndex = 20;
            if (ship >= 0)
            {
                SaveManager.Data.ownedShips[ship] = true;
                SaveManager.Data.selectedShip = ship;
                SaveManager.Data.shipLevel = 3;
            }
            SpaceHawk.Data.LevelDatabase db = Resources.Load<SpaceHawk.Data.LevelDatabase>("Data/LevelDatabase");
            GameManager.StartLevel(db.GetByIndex(levelIndex));
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == GameManager.GameplaySceneName);
            yield return null;

            SpaceHawk.Gameplay.PlayerShip player = Object.FindAnyObjectByType<SpaceHawk.Gameplay.PlayerShip>();
            player.GetComponent<SpaceHawk.Gameplay.Health>().SetInvincible(true);

            // Let the intro doors open and the first waves arrive on screen.
            float until = Time.realtimeSinceStartup + 9f;
            while (Time.realtimeSinceStartup < until) yield return null;

            if (buffs)
            {
                player.CollectPowerUp(SpaceHawk.Gameplay.PowerUpType.Spread);
                player.CollectPowerUp(SpaceHawk.Gameplay.PowerUpType.Beam);
                float wait = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < wait) yield return null;
            }

            foreach (Vector2Int screen in new[] { Screens[0], Screens[1] })
            {
                RenderTexture target = new RenderTexture(screen.x, screen.y, 24, RenderTextureFormat.ARGB32);
                Camera cam = Camera.main;
                RenderTexture previousTarget = cam.targetTexture;
                cam.targetTexture = target;

                foreach (Canvas c in Object.FindObjectsByType<Canvas>())
                {
                    if (!c.isRootCanvas) continue;
                    bool wasOverlay = c.renderMode == RenderMode.ScreenSpaceOverlay;
                    ConfigureCanvas(c, cam);
                    // In the game these are screen-space overlays, always drawn over the world;
                    // as camera canvases they would sort among the sprites and get covered by them.
                    if (wasOverlay) c.sortingOrder += 1000;
                }
                yield return null;
                yield return null;

                Rig rig = new Rig { camera = cam, target = target, root = null };
                Save(rig, $"{name}_{screen.x}x{screen.y}");
                cam.targetTexture = previousTarget;
                target.Release();
                yield return null;
            }
        }

        /// <summary>GameManager.ReturnToMenu(true) is exactly "open the MainMenu scene on Level Select".</summary>
        private static class GameManagerBridge
        {
            public static void OpenLevelSelect() => GameManager.ReturnToMenu(true);
        }
    }
}
