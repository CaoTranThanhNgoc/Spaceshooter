using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    public class LevelSelectUI : MonoBehaviour
    {
        public LevelDatabase database;
        public LevelNodeView nodePrefab;

        [Tooltip("One entry per horizontally-scrolled page of LevelSelect_Background.png - index " +
                 "matches LevelData.mapPage. Nodes are parented into pageContainers[level.mapPage].")]
        public Transform[] pageContainers;
        [Tooltip("Same length/order as pageContainers - each page's own full-size rect, resized to " +
                 "the viewport's actual measured width at runtime so pages line up exactly with a " +
                 "swipe regardless of the device's real aspect ratio (the build-time script can only " +
                 "guess a reference width).")]
        public RectTransform[] pageRects;
        public RectTransform mapViewport;
        public RectTransform mapContent;
        public ScrollRect mapScrollRect;

        public TMP_Text energyLabel;
        public TMP_Text crystalLabel;
        public Button settingsButton;
        public Button missionsButton;
        [Tooltip("Small red counter on the Missions icon - how many rewards are waiting to be claimed.")]
        public GameObject missionsBadge;
        public Image missionsBadgeImage;
        public TMP_Text missionsBadgeLabel;
        public Button endlessButton;
        public Button inventoryButton;
        public Button achievementsButton;
        public Button leaderboardButton;
        public Button howToPlayButton;
        public Button playerButton;
        public Image playerIcon;
        [Tooltip("Level-1 sprite of each selectable hull (see PlayerShip/InventoryPanel), in " +
                 "SaveManager.GetSelectedShip() order (ShipCatalog index) - the player button's icon picks from these.")]
        public Sprite[] hullIcons;
        public Transform overlayRoot;
        public Transform toastRoot;

        private void Awake()
        {
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (missionsButton != null) missionsButton.onClick.AddListener(OpenMissions);
            if (endlessButton != null) endlessButton.onClick.AddListener(OpenEndless);
            if (missionsBadgeImage != null) missionsBadgeImage.sprite = SpaceHawk.Gameplay.ProceduralSprites.Dot;
            if (inventoryButton != null) inventoryButton.onClick.AddListener(OpenInventory);
            if (achievementsButton != null) achievementsButton.onClick.AddListener(OpenAchievements);
            if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OpenLeaderboard);
            if (howToPlayButton != null) howToPlayButton.onClick.AddListener(OpenHowToPlay);
            if (playerButton != null) playerButton.onClick.AddListener(OpenPlayerProfile);
        }

        private void OnEnable()
        {
            // Players who cleared levels before the skill rating existed get a starting score for
            // each (from its stars) instead of a rating of zero - a one-off, never overwrites real scores.
            SkillScore.MigrateLegacyScores(database);
            LayoutMapPages();
            Populate();
            RefreshCurrency();
            RefreshPlayerIcon();
            RefreshMissionsBadge();
            ScrollToCurrentPage();
            DailyMissions.Changed += RefreshMissionsBadge;
            SaveManager.CrystalsChanged += OnCrystalsChanged;
            SaveManager.EnergyChanged += OnEnergyChanged;
            SaveManager.SelectedShipChanged += OnSelectedShipChanged;
            SaveManager.ProfileChanged += OnProfileChanged;
        }

        // Pages are built at a guessed reference width (see UIBuilder_MainMenuScene) since the
        // build-time script has no idea what device this actually runs on. Resizing/repositioning
        // them here, against the viewport's own real measured width, makes each page exactly one
        // viewport wide on ANY aspect ratio - without this, a swipe could land mid-way between two
        // pages on a device much wider or narrower than the reference.
        private void LayoutMapPages()
        {
            if (mapViewport == null || mapContent == null || pageRects == null || pageRects.Length == 0) return;

            float pageWidth = mapViewport.rect.width;
            if (pageWidth <= 0f) return;

            for (int i = 0; i < pageRects.Length; i++)
            {
                RectTransform page = pageRects[i];
                if (page == null) continue;
                page.sizeDelta = new Vector2(pageWidth, page.sizeDelta.y);
                page.anchoredPosition = new Vector2(pageWidth * i, page.anchoredPosition.y);
            }

            mapContent.sizeDelta = new Vector2(pageWidth * pageRects.Length, mapContent.sizeDelta.y);
        }

        // Returning players shouldn't have to manually swipe back to wherever they last were -
        // land on the page holding their furthest-unlocked level every time this screen opens.
        private void ScrollToCurrentPage()
        {
            if (mapScrollRect == null || database == null || pageRects == null || pageRects.Length <= 1) return;

            int targetPage = 0;
            for (int i = 0; i < database.Count; i++)
            {
                if (!SaveManager.IsLevelUnlocked(i)) break;
                LevelData level = database.GetByIndex(i);
                if (level != null) targetPage = Mathf.Clamp(level.mapPage, 0, pageRects.Length - 1);
            }

            mapScrollRect.horizontalNormalizedPosition = targetPage / (float)(pageRects.Length - 1);
        }

        private void OnDisable()
        {
            DailyMissions.Changed -= RefreshMissionsBadge;
            SaveManager.CrystalsChanged -= OnCrystalsChanged;
            SaveManager.EnergyChanged -= OnEnergyChanged;
            SaveManager.SelectedShipChanged -= OnSelectedShipChanged;
            SaveManager.ProfileChanged -= OnProfileChanged;
        }

        private void OnSelectedShipChanged(int shipIndex)
        {
            RefreshPlayerIcon();
        }

        // Signing in or out swapped the whole profile: the map's unlocked levels and stars, the
        // balances and the badge all belong to the other one now.
        private void OnProfileChanged()
        {
            if (this == null) return;
            SkillScore.MigrateLegacyScores(database);
            Populate();
            RefreshCurrency();
            RefreshPlayerIcon();
            RefreshMissionsBadge();
            ScrollToCurrentPage();
        }

        private void Populate()
        {
            if (database == null || nodePrefab == null || pageContainers == null || pageContainers.Length == 0) return;

            foreach (Transform container in pageContainers)
            {
                if (container == null) continue;
                foreach (Transform child in container) Destroy(child.gameObject);
            }

            for (int i = 0; i < database.Count; i++)
            {
                LevelData level = database.GetByIndex(i);
                int page = Mathf.Clamp(level.mapPage, 0, pageContainers.Length - 1);
                Transform container = pageContainers[page];
                if (container == null) continue;

                LevelNodeView node = Instantiate(nodePrefab, container);
                bool unlocked = SaveManager.IsLevelUnlocked(i);
                int stars = SaveManager.GetStars(level.levelId);
                node.Setup(level, unlocked, stars, OnLevelSelected);
            }
        }

        private void OnLevelSelected(LevelData level)
        {
            // Guests can try the game without committing to an account, but progress/leaderboard
            // features only make sense with a persistent identity - gate deeper levels here
            // rather than letting a guest grind far in and risk losing it all on a new device.
            if (!AccountManager.IsLinked && level.displayNumber > AccountManager.GuestLevelCap)
            {
                ToastUI.ShowToast(toastRoot != null ? toastRoot : transform,
                    Localization.Format("levelselect.guest_level_locked_fmt", AccountManager.GuestLevelCap));
                OpenPlayerProfile();
                return;
            }

            if (!SaveManager.TrySpendEnergy(level.energyCost))
            {
                EnergyRefillDialog.Show(overlayRoot != null ? overlayRoot : transform);
                return;
            }
            GameManager.StartLevel(level);
        }

        private void RefreshCurrency()
        {
            OnCrystalsChanged(SaveManager.GetCrystals());
            OnEnergyChanged(SaveManager.GetEnergy());
        }

        private void OnCrystalsChanged(int value)
        {
            if (crystalLabel != null) crystalLabel.text = value.ToString();
        }

        private void OnEnergyChanged(int value)
        {
            if (energyLabel != null) energyLabel.text = $"{value}/{SaveManager.MaxEnergy}";
        }

        private void RefreshPlayerIcon()
        {
            if (playerIcon == null || hullIcons == null || hullIcons.Length == 0) return;
            int index = Mathf.Clamp(SaveManager.GetSelectedShip(), 0, hullIcons.Length - 1);
            if (hullIcons[index] != null) playerIcon.sprite = hullIcons[index];
        }

        private void OpenSettings()
        {
            OpenOverlay("SettingsPanel");
        }

        private void OpenAchievements()
        {
            OpenOverlay("AchievementsPanel");
        }

        private void OpenMissions()
        {
            OpenOverlay("MissionsPanel");
        }

        /// <summary>Endless costs energy like any level; no account is needed since it has no
        /// leaderboard - only a personal best.</summary>
        private void OpenEndless()
        {
            LevelData endless = EndlessLevel.GetOrCreate();
            if (!SaveManager.TrySpendEnergy(endless.energyCost))
            {
                EnergyRefillDialog.Show(overlayRoot != null ? overlayRoot : transform);
                return;
            }
            GameManager.StartLevel(endless);
        }

        /// <summary>Counts what is ready to collect: the login reward, each finished-but-unclaimed
        /// mission and the all-done bonus.</summary>
        private void RefreshMissionsBadge()
        {
            if (missionsBadge == null) return;

            int ready = SaveManager.CanClaimDailyReward() ? 1 : 0;
            for (int i = 0; i < DailyMissions.Count; i++)
                if (DailyMissions.IsComplete(i) && !DailyMissions.IsClaimed(i)) ready++;
            if (DailyMissions.CanClaimBonus) ready++;

            missionsBadge.SetActive(ready > 0);
            if (missionsBadgeLabel != null) missionsBadgeLabel.text = ready.ToString();
        }

        private void OpenInventory()
        {
            OpenOverlay("InventoryPanel");
        }

        private void OpenHowToPlay()
        {
            OpenOverlay("HowToPlayPanel");
        }

        private void OpenLeaderboard()
        {
            if (!AccountManager.IsLinked)
            {
                ToastUI.ShowToast(toastRoot != null ? toastRoot : transform, Localization.Get("levelselect.guest_leaderboard_locked"));
                OpenPlayerProfile();
                return;
            }
            OpenOverlay("LeaderboardPanel");
        }

        private void OpenPlayerProfile()
        {
            OpenOverlay("PlayerProfilePanel");
        }

        private void OpenOverlay(string resourceName)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + resourceName);
            if (prefab == null) return;
            Transform parent = overlayRoot != null ? overlayRoot : transform;
            Instantiate(prefab, parent);
        }
    }
}
