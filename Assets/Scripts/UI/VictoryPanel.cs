using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    public class VictoryPanel : MonoBehaviour
    {
        [Tooltip("One badge per star count (0..3) - each already has its own 0-3 stars drawn on it, same art used on the Level Select map, so index = stars earned.")]
        public Image starBadge;
        public Sprite[] starBadgeSprites;
        public Button continueButton;
        public TMP_Text continueButtonLabel;
        public Button menuButton;
        [Tooltip("Level number written on the medal's plate, as on the Level Select map. Hidden for modes with no level number.")]
        public TMP_Text medalNumberLabel;
        [Header("Score card")]
        [Tooltip("Four stacked lines: kills, health kept, speed, difficulty. Labels left-aligned, values right-aligned, same line spacing, so the rows line up.")]
        public TMP_Text breakdownLabels;
        public TMP_Text breakdownValues;
        public TMP_Text totalLabel;
        public TMP_Text totalValue;
        [Tooltip("Highlighted tag between the total's label and value - blank unless this run beat the level's record.")]
        public TMP_Text newBestLabel;
        [Tooltip("Card footer: the level's previous record (left) and the Leaderboard skill rating with what this run added (right).")]
        public TMP_Text bestBeforeLabel;
        public TMP_Text ratingLabel;

        // Written without a newline escape on purpose: built from a char code.
        private static readonly string Nl = ((char)10).ToString();

        private LevelData _nextLevel;
        private LevelScoreBreakdown _score;
        private int _previousBest;
        private long _ratingBefore;
        private long _ratingAfter;
        private bool _hasScore;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (menuButton != null) menuButton.onClick.AddListener(ToLevelSelect);
        }

        private void OnEnable()
        {
            Localization.LanguageChanged += RefreshContinueLabel;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshContinueLabel;
        }

        public void SetStars(int count)
        {
            if (starBadge == null || starBadgeSprites == null || starBadgeSprites.Length == 0) return;
            int index = Mathf.Clamp(count, 0, starBadgeSprites.Length - 1);
            starBadge.sprite = starBadgeSprites[index];
        }

        /// <summary>Pass the level right after the one just cleared - null if this was the last
        /// one, in which case Continue just falls back to Level Select.</summary>
        public void SetNextLevel(LevelData nextLevel)
        {
            _nextLevel = nextLevel;
            RefreshContinueLabel();
        }

        /// <summary>Shows how this clear was scored (kill points + health + speed, times the level's
        /// difficulty) and what it did to the Leaderboard's skill rating - so the number is never a
        /// mystery to the player.</summary>
        public void SetScore(LevelScoreBreakdown score, int previousBest, long ratingBefore, long ratingAfter)
        {
            _score = score;
            _previousBest = previousBest;
            _ratingBefore = ratingBefore;
            _ratingAfter = ratingAfter;
            _hasScore = true;
            RefreshScore();
        }

        public void SetLevelNumber(int displayNumber)
        {
            if (medalNumberLabel == null) return;
            medalNumberLabel.gameObject.SetActive(displayNumber > 0);
            medalNumberLabel.text = displayNumber.ToString();
        }

        private void RefreshScore()
        {
            if (!_hasScore) return;

            bool newBest = _score.total > _previousBest;
            if (breakdownLabels != null)
            {
                breakdownLabels.text = string.Join(Nl,
                    Localization.Get("score.kills"), Localization.Get("score.health"),
                    Localization.Get("score.speed"), Localization.Get("score.difficulty"));
            }
            if (breakdownValues != null)
            {
                breakdownValues.text = string.Join(Nl,
                    "+" + _score.killPoints, "+" + _score.healthBonus, "+" + _score.speedBonus,
                    "x" + _score.difficulty.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
            }

            if (totalLabel != null) totalLabel.text = Localization.Get("score.total");
            if (totalValue != null) totalValue.text = _score.total.ToString();
            if (newBestLabel != null) newBestLabel.text = newBest ? Localization.Get("endless.new_best") : "";

            if (bestBeforeLabel != null)
                bestBeforeLabel.text = _previousBest > 0 ? Localization.Get("score.best_before") + "  " + _previousBest : "";
            if (ratingLabel != null)
            {
                long gained = _ratingAfter - _ratingBefore;
                string gain = gained > 0 ? "  <color=#FFD633>(+" + gained + ")</color>" : "";
                ratingLabel.text = Localization.Get("score.rating") + "  " + _ratingAfter + gain;
            }
        }

        private void RefreshContinueLabel()
        {
            RefreshScore();
            if (continueButtonLabel != null)
                continueButtonLabel.text = Localization.Get(_nextLevel != null ? "victory.next_level" : "victory.level_select");
        }

        private void OnContinue()
        {
            if (_nextLevel == null)
            {
                GameManager.ReturnToMenu(true);
                return;
            }

            // Chaining Continue straight into the next level used to skip LevelSelectUI entirely -
            // the only other place that ever spent energy - letting a player clear the whole
            // campaign on the energy a single level normally costs.
            if (!SaveManager.TrySpendEnergy(_nextLevel.energyCost))
            {
                EnergyRefillDialog.Show(transform);
                return;
            }

            GameManager.StartLevel(_nextLevel);
        }

        private void ToLevelSelect()
        {
            GameManager.ReturnToMenu(true);
        }
    }
}
