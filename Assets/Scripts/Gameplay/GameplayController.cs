using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.UI;
using SpaceHawk.Online;

namespace SpaceHawk.Gameplay
{
    /// <summary>Wires the Gameplay scene together: starts the selected level's waves,
    /// watches for the player dying or the last wave clearing, and shows the matching overlay.</summary>
    public class GameplayController : MonoBehaviour
    {
        public PlayerShip player;
        public EnemySpawner spawner;
        public HUDController hud;
        public Transform overlayRoot;
        public ParallaxBackgroundController parallaxBackground;
        public LevelIntroBanner introBanner;
        public MoveTutorialHint moveTutorialHint;
        [Tooltip("Endless mode thickens the meteor shower as the waves climb.")]
        public MeteorSpawner meteorSpawner;

        // Dying once is survivable for a price in Crystals, but only once per attempt - otherwise
        // a rich player could never actually lose.
        private const float ReviveHpFraction = 0.6f;

        private LevelData _level;
        private bool _endless;
        private int _endlessCrystalsPaid;
        private bool _endlessNewBest;
        private bool _levelEnded;
        private bool _reviveUsed;
        private float _fightStartTime;
        private GameObject _gameOverPanelObject;
        private AudioSource _musicSource;

        private void Start()
        {
            _level = GameManager.SelectedLevel;
            if (_level == null)
            {
                Debug.LogWarning("[GameplayController] No level selected, returning to menu.");
                GameManager.ReturnToMenu(true);
                return;
            }

            if (parallaxBackground != null) parallaxBackground.ActivateSet(_level.backgroundSetIndex);

            // PlayerShip.Awake() already pulled the Inventory-upgraded HP/damage/sprite from
            // SaveManager on its own, so there is nothing to re-apply here.

            AudioManager.EnsureInitialized(); // neither scene has its own AudioListener - see AudioManager
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.clip = GameAudio.Background;
            _musicSource.loop = true;
            _musicSource.volume = 0.35f; // sits under SFX, not over it
            _musicSource.spatialBlend = 0f;
            _musicSource.playOnAwake = false;
            _musicSource.Play();

            _endless = _level.isEndless;
            player.Died += HandleDefeat;
            player.SecondWindUsed += OnSecondWindUsed;
            spawner.AllEnemiesCleared += HandleVictory;
            if (_endless) spawner.WaveStarted += OnEndlessWaveStarted;
            DailyMissions.MissionCompleted += OnMissionCompleted;
            // Real-time achievement toasts for kill-based achievements - the only kind that
            // changes continuously during actual gameplay rather than in a menu.
            spawner.KillCountChanged += OnKillCountChangedForAchievements;
            if (hud != null)
            {
                hud.Bind(player, _level, spawner);
                hud.PauseRequested += () => ShowPanel("PauseMenu");
            }

            ShowLevelIntro();
        }

        /// <summary>Announces the level and its win condition behind closed "doors" before
        /// anything spawns - enemy waves only start once the doors finish sliding back open, so
        /// the fight never starts hidden behind (or racing) this sequence. Falls straight through
        /// to BeginLevel if the banner is missing so the level still works without it.</summary>
        private void ShowLevelIntro()
        {
            if (introBanner == null)
            {
                player.BeginFight();
                spawner.BeginLevel(_level);
                return;
            }

            string title;
            string objective;
            if (_endless)
            {
                int best = SaveManager.GetEndlessBestWave();
                title = Localization.Get("endless.title");
                objective = best > 0 ? Localization.Format("endless.objective_fmt", best) : Localization.Get("endless.objective_first");
            }
            else
            {
                // Just the required count - not "required/total spawned", which showed a different
                // second number than the in-game HUD gauge (current/required) and read as the two not
                // agreeing with each other.
                title = Localization.Get("hud.level") + " " + _level.displayNumber;
                objective = Localization.Format("levelintro.objective_fmt", _level.GetRequiredKillCount());
            }
            introBanner.Show(title, objective, OnIntroComplete);
        }

        private void OnDestroy()
        {
            DailyMissions.MissionCompleted -= OnMissionCompleted;
            if (player != null) player.SecondWindUsed -= OnSecondWindUsed;
        }

        private void OnSecondWindUsed()
        {
            Transform parent = overlayRoot != null ? overlayRoot : transform;
            ToastUI.ShowToast(parent, Localization.Get("ship.second_wind_used"));
        }

        private void OnMissionCompleted(int index)
        {
            Transform parent = overlayRoot != null ? overlayRoot : transform;
            ToastUI.ShowToast(parent, Localization.Format("missions.toast_done_fmt", DailyMissions.GetTitle(DailyMissions.Get(index))));
        }

        /// <summary>A new Endless wave: tick the "reach wave N" mission, give the scenery and the
        /// meteor shower their step up, and celebrate any achievement the new wave just earned.</summary>
        private void OnEndlessWaveStarted(int wave)
        {
            DailyMissions.ReportMax(MissionKind.EndlessWave, wave);

            if (parallaxBackground != null)
                parallaxBackground.ActivateSet(EndlessWaves.BackgroundSetFor(wave, parallaxBackground.SetCount));

            if (meteorSpawner != null)
            {
                meteorSpawner.minInterval = Mathf.Max(0.5f, 1.2f - 0.025f * wave);
                meteorSpawner.maxInterval = Mathf.Max(1.2f, 3f - 0.06f * wave);
            }
        }

        /// <summary>Endless has no win: a run ends here, so record it, pay out the Crystals for the
        /// waves survived (only the difference since an earlier death if the player revived) and
        /// describe the result for the Game Over panel.</summary>
        private string BuildEndlessResult()
        {
            int wave = Mathf.Max(1, spawner.CurrentWave);
            int score = SkillScore.ForEndless(spawner.TotalPoints, wave);

            bool improved = SaveManager.RecordEndlessRun(wave, score);
            _endlessNewBest |= improved;
            _ = LeaderboardManager.SubmitLocalScore();

            int owed = Mathf.Max(0, EndlessWaves.RewardFor(wave) - _endlessCrystalsPaid);
            if (owed > 0)
            {
                SaveManager.AddCrystals(owed);
                _endlessCrystalsPaid += owed;
            }
            AchievementNotifier.CheckAndToast(overlayRoot != null ? overlayRoot : transform);

            string summary = Localization.Format("endless.result_fmt", wave, score);
            string payout = Localization.Format("endless.reward_fmt", _endlessCrystalsPaid);
            if (_endlessNewBest) payout = Localization.Get("endless.new_best") + "   " + payout;
            return summary + "\n" + payout;
        }

        private void OnKillCountChangedForAchievements(int _)
        {
            AchievementNotifier.CheckAndToast(overlayRoot != null ? overlayRoot : transform);
        }

        private void OnIntroComplete()
        {
            // The clock for the speed bonus starts when the fight does, not during the doors
            // sequence. Time.time freezes while paused, so pausing never costs the player time.
            _fightStartTime = Time.time;
            player.BeginFight();
            spawner.BeginLevel(_level);
            // Right as the fight actually starts, not during the doors sequence - a brand-new
            // player needs to be able to try the drag immediately, and nothing before this point
            // requires it anyway.
            if (moveTutorialHint != null) moveTutorialHint.Show();
        }

        private void HandleDefeat()
        {
            HandleDefeatWithReason(_endless ? BuildEndlessResult() : Localization.Get("gameover.reason_defeated"), true);
        }

        private void HandleDefeatWithReason(string reason, bool allowRevive = false)
        {
            if (_levelEnded) return;
            _levelEnded = true;
            GameObject panel = ShowPanel("GameOverPanel");
            _gameOverPanelObject = panel;
            GameOverPanel gameOverPanel = panel != null ? panel.GetComponent<GameOverPanel>() : null;
            if (gameOverPanel != null)
            {
                gameOverPanel.SetReason(reason);
                gameOverPanel.SetReviveOption(allowRevive && !_reviveUsed, OnReviveRequested);
            }
            if (_musicSource != null) _musicSource.Stop();
            AudioManager.Play(GameAudio.GameOver, 0.6f);
            // Freeze combat behind the panel (enemies/meteors/player) - same as Pause - instead
            // of letting it keep running while the player reads the Game Over screen.
            Time.timeScale = 0f;
        }

        private void OnReviveRequested()
        {
            _reviveUsed = true;
            _levelEnded = false;
            if (_gameOverPanelObject != null) Destroy(_gameOverPanelObject);
            _gameOverPanelObject = null;

            Time.timeScale = 1f;
            player.Revive(ReviveHpFraction);
            if (_musicSource != null) _musicSource.Play();
        }

        // Backgrounding the app (a call, the home button) must not leave the fight running unseen.
        // Focus loss on its own is mobile-only: in the Editor it fires whenever another editor
        // window is clicked, which would pause the game constantly while developing.
        private void OnApplicationPause(bool paused)
        {
            if (paused) AutoPause();
        }

#if !UNITY_EDITOR
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) AutoPause();
        }
#endif

        private void AutoPause()
        {
            if (_level == null || _levelEnded || Time.timeScale == 0f) return;
            ShowPanel("PauseMenu");
        }

        private void HandleVictory()
        {
            if (_levelEnded) return;

            // Letting most of a level's enemies simply fly past used to still count as clearing
            // it - dodging was a complete substitute for fighting. Now the level only ends in
            // Victory once enough of what it spawned was actually destroyed; falling short ends
            // it in defeat instead; once nothing more is going to spawn or die. Boss levels are
            // exempt: a Boss-kind enemy can only ever be removed by being killed (it holds
            // position instead of despawning), so beating it already proves far more than the
            // ordinary ratio does - without this, a player who beat the boss but let earlier
            // regular waves fly past could still see "Game Over" right after the boss died.
            if (!_level.HasBossWave() && spawner.KillRatio < _level.requiredKillRatio)
            {
                HandleDefeatWithReason(Localization.Get("gameover.reason_not_enough_kills"));
                return;
            }

            _levelEnded = true;

            Health playerHealth = player.GetComponent<Health>();
            float pct = playerHealth.maxHp > 0 ? (float)playerHealth.CurrentHp / playerHealth.maxHp : 0f;
            int stars = pct >= 0.8f ? 3 : pct >= 0.4f ? 2 : 1;

            // Skill score: kill points, plus bonuses for the damage avoided and the time taken, scaled
            // by the level's difficulty. Only the best run on a level counts toward the rating.
            Health playerHealthForScore = player.GetComponent<Health>();
            LevelScoreBreakdown score = SkillScore.ForLevel(_level, spawner.TotalPoints,
                playerHealthForScore.TotalDamageTaken, playerHealthForScore.maxHp, Time.time - _fightStartTime);
            int previousBest = SaveManager.GetLevelBestScore(_level.levelId);
            long ratingBefore = SaveManager.GetSkillRating();
            SaveManager.SetLevelBestScore(_level.levelId, score.total);
            long ratingAfter = SaveManager.GetSkillRating();

            SaveManager.SetStars(_level.levelId, stars);
            SaveManager.AddCrystals(_level.crystalReward);
            DailyMissions.Report(MissionKind.ClearLevels);
            if (stars >= 3) DailyMissions.Report(MissionKind.ThreeStarWins);
            AchievementNotifier.CheckAndToast(overlayRoot != null ? overlayRoot : transform);
            // Fire-and-forget: a clearing win still shows the Victory panel even if the player has
            // no connection right now - LeaderboardManager swallows its own network failures.
            _ = LeaderboardManager.SubmitLocalScore();

            LevelDatabase db = Resources.Load<LevelDatabase>("Data/LevelDatabase");
            LevelData nextLevel = null;
            if (db != null)
            {
                int idx = db.IndexOf(_level);
                if (idx >= 0)
                {
                    SaveManager.UnlockLevel(idx + 1);
                    nextLevel = db.GetByIndex(idx + 1);
                }
            }

            GameObject panel = ShowPanel("VictoryPanel");
            VictoryPanel victoryPanel = panel != null ? panel.GetComponent<VictoryPanel>() : null;
            if (victoryPanel != null)
            {
                victoryPanel.SetStars(stars);
                victoryPanel.SetNextLevel(nextLevel);
                victoryPanel.SetLevelNumber(_level.displayNumber);
                victoryPanel.SetScore(score, previousBest, ratingBefore, ratingAfter);
            }

            if (_musicSource != null) _musicSource.Stop();
            AudioManager.Play(GameAudio.Victory, 0.6f);
            Time.timeScale = 0f;
        }

        private GameObject ShowPanel(string resourceName)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/" + resourceName);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameplayController] Missing overlay prefab: {resourceName}");
                return null;
            }
            Transform parent = overlayRoot != null ? overlayRoot : transform;
            return Instantiate(prefab, parent);
        }
    }
}
