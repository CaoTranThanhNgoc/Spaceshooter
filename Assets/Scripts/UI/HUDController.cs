using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.Gameplay;

namespace SpaceHawk.UI
{
    [System.Serializable]
    public struct BuffIconSlot
    {
        public PowerUpType type;
        public GameObject root;
        public TMP_Text label;
    }

    public class HUDController : MonoBehaviour
    {
        public Image healthFill;
        public TMP_Text levelLabel;
        public TMP_Text scoreLabel;
        public TMP_Text killsLabel;
        public Image killsFill;
        public Button pauseButton;
        public Image lowHealthOverlay;
        [Tooltip("Endless mode: big caption flashed in the middle of the screen as each wave begins.")]
        public TMP_Text waveBannerLabel;

        private static readonly Color KillsFillNormal = new Color(1f, 0.55f, 0.25f, 1f);
        private static readonly Color KillsFillComplete = new Color(0.4f, 0.9f, 0.5f, 1f);
        public BuffIconSlot[] buffIcons;
        public GameObject bossBarRoot;
        public Image bossBarFill;

        // Shared with UIBuilder_GameplayScene.BuildBuffIcons so the baked starting layout and the
        // runtime reflow below always agree on where slot 0, 1, 2... sit.
        public const float BuffIconBaseX = 50f;
        public const float BuffIconSize = 56f;
        public const float BuffIconSpacing = 10f;

        private const float LowHealthThreshold = 0.25f;

        private PlayerShip _player;
        private int _points;
        private int _levelDisplayNumber;
        private Health _bossHealth;
        private EnemySpawner _spawner;
        private int _requiredKills;
        private int _currentKills;
        private float _healthPct = 1f;
        private float _lowHealthPulseTime;
        private bool _endless;
        private int _wave;
        private Coroutine _bannerRoutine;

        public event System.Action PauseRequested;

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(() => PauseRequested?.Invoke());
        }

        private void OnEnable()
        {
            Localization.LanguageChanged += RefreshLabels;
            Enemy.BossSpawned += OnBossSpawned;
            if (bossBarRoot != null) bossBarRoot.SetActive(false);
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshLabels;
            Enemy.BossSpawned -= OnBossSpawned;
            UnsubscribeBoss();
        }

        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.KillCountChanged -= OnLevelKillCountChanged;
                _spawner.PointsChanged -= OnPointsChanged;
                _spawner.WaveStarted -= OnWaveStarted;
            }
        }

        private void OnWaveStarted(int wave)
        {
            _wave = wave;
            RefreshLabels();

            if (waveBannerLabel == null) return;
            bool boss = EndlessWaves.IsBossWave(wave);
            waveBannerLabel.text = Localization.Format(boss ? "endless.boss_wave_fmt" : "endless.wave_fmt", wave);
            waveBannerLabel.color = boss ? new Color(1f, 0.35f, 0.3f, 1f) : Color.white;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(FlashWaveBanner());
        }

        // Unscaled time: the banner must play out even if the player pauses right as it appears.
        private System.Collections.IEnumerator FlashWaveBanner()
        {
            const float fadeIn = 0.25f;
            const float hold = 1.1f;
            const float fadeOut = 0.45f;

            waveBannerLabel.gameObject.SetActive(true);
            float t = 0f;
            while (t < fadeIn + hold + fadeOut)
            {
                t += Time.unscaledDeltaTime;
                float alpha = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
                float scale = 1f + (1f - Mathf.Clamp01(t / (fadeIn + 0.2f))) * 0.4f;
                Color c = waveBannerLabel.color;
                c.a = Mathf.Clamp01(alpha);
                waveBannerLabel.color = c;
                waveBannerLabel.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            waveBannerLabel.gameObject.SetActive(false);
            _bannerRoutine = null;
        }

        /// <summary>Boss health bar - hidden until a Boss-kind enemy actually spawns (only the
        /// last wave of the final level, right now), then tracks that one Enemy's Health directly
        /// so it needs no per-frame polling.</summary>
        private void OnBossSpawned(Enemy boss)
        {
            UnsubscribeBoss();
            _bossHealth = boss.GetComponent<Health>();
            if (_bossHealth == null) return;

            _bossHealth.DamagedPercent += SetBossFill;
            _bossHealth.Died += HideBossBar;
            SetBossFill(1f);
            if (bossBarRoot != null) bossBarRoot.SetActive(true);
        }

        private void SetBossFill(float pct)
        {
            if (bossBarFill != null) bossBarFill.fillAmount = pct;
        }

        private void HideBossBar()
        {
            if (bossBarRoot != null) bossBarRoot.SetActive(false);
            UnsubscribeBoss();
        }

        private void UnsubscribeBoss()
        {
            if (_bossHealth == null) return;
            _bossHealth.DamagedPercent -= SetBossFill;
            _bossHealth.Died -= HideBossBar;
            _bossHealth = null;
        }

        public void Bind(PlayerShip player, LevelData level, EnemySpawner spawner)
        {
            _player = player;
            Health health = player.GetComponent<Health>();
            health.DamagedPercent += SetHealthFill;
            SetHealthFill(1f);
            _levelDisplayNumber = level.displayNumber;

            _points = 0;

            _spawner = spawner;
            _endless = level != null && level.isEndless;
            _requiredKills = level != null ? level.GetRequiredKillCount() : 0;
            _currentKills = 0;
            if (_spawner != null)
            {
                _spawner.KillCountChanged += OnLevelKillCountChanged;
                _spawner.PointsChanged += OnPointsChanged;
            }
            if (_spawner != null && _endless) _spawner.WaveStarted += OnWaveStarted;

            RefreshLabels();
        }

        private void OnLevelKillCountChanged(int killedCount)
        {
            _currentKills = killedCount;
            UpdateKillsDisplay();
        }

        private void UpdateKillsDisplay()
        {
            // Endless has no kill quota to measure progress against - just a running count.
            if (_endless)
            {
                if (killsLabel != null) killsLabel.text = _currentKills.ToString();
                if (killsFill != null)
                {
                    killsFill.fillAmount = 1f;
                    killsFill.color = KillsFillNormal;
                }
                return;
            }

            if (killsLabel != null) killsLabel.text = $"{_currentKills}/{_requiredKills}";

            if (killsFill != null)
            {
                bool met = _requiredKills <= 0 || _currentKills >= _requiredKills;
                killsFill.fillAmount = _requiredKills > 0 ? Mathf.Clamp01((float)_currentKills / _requiredKills) : 1f;
                killsFill.color = met ? KillsFillComplete : KillsFillNormal;
            }
        }

        private void SetHealthFill(float pct)
        {
            _healthPct = pct;
            if (healthFill != null) healthFill.fillAmount = pct;
            if (lowHealthOverlay != null && pct > LowHealthThreshold) lowHealthOverlay.color = new Color(1f, 0.15f, 0.15f, 0f);
        }

        private void OnPointsChanged(int totalPoints)
        {
            _points = totalPoints;
            UpdateScore();
        }

        private void RefreshLabels()
        {
            if (levelLabel != null)
            {
                if (_endless)
                    levelLabel.text = _wave > 0 ? Localization.Format("endless.wave_fmt", _wave) : Localization.Get("endless.title");
                else
                    levelLabel.text = Localization.Get("hud.level") + " " + _levelDisplayNumber;
            }
            UpdateScore();
            UpdateKillsDisplay();
        }

        /// <summary>The running total of what every enemy destroyed this level was worth - the
        /// "kill points" line of the final score (see SkillScore).</summary>
        private void UpdateScore()
        {
            if (scoreLabel == null) return;
            scoreLabel.text = Localization.Get("hud.score") + " " + _points;
        }

        // Each buff type used to own a fixed slot (Damage always at x=50, Rockets always at
        // x=116...), so whichever ones happened to be active left gaps between them - or, with
        // only a late-index buff active, sat off past the health bar's own width entirely. Packing
        // only the currently-active icons left-to-right (in the same stable array order every
        // time, so a given buff doesn't jump around while others toggle) keeps them always flush
        // under the health bar instead.
        private void Update()
        {
            UpdateLowHealthPulse();

            if (_player == null || buffIcons == null) return;

            int visibleIndex = 0;
            for (int i = 0; i < buffIcons.Length; i++)
            {
                BuffIconSlot slot = buffIcons[i];
                if (slot.root == null) continue;

                float remaining = _player.GetBuffRemaining(slot.type);
                bool active = remaining > 0.05f;
                if (slot.root.activeSelf != active) slot.root.SetActive(active);
                if (!active) continue;

                if (slot.label != null) slot.label.text = Mathf.CeilToInt(remaining).ToString();

                float x = BuffIconBaseX + visibleIndex * (BuffIconSize + BuffIconSpacing);
                RectTransform rt = (RectTransform)slot.root.transform;
                Vector2 pos = rt.anchoredPosition;
                if (!Mathf.Approximately(pos.x, x)) rt.anchoredPosition = new Vector2(x, pos.y);
                visibleIndex++;
            }
        }

        /// <summary>Full-screen red pulse once HP drops critical - the small corner health bar is
        /// easy to stop watching mid-fight, this is not.</summary>
        private void UpdateLowHealthPulse()
        {
            if (lowHealthOverlay == null) return;

            if (_healthPct > LowHealthThreshold)
            {
                _lowHealthPulseTime = 0f;
                return;
            }

            _lowHealthPulseTime += Time.deltaTime * 3.5f;
            float alpha = 0.12f + (Mathf.Sin(_lowHealthPulseTime) * 0.5f + 0.5f) * 0.22f;
            lowHealthOverlay.color = new Color(1f, 0.15f, 0.15f, alpha);
        }
    }
}
