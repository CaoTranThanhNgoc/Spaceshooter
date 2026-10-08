using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    public enum RowState { InProgress, Claimable, Claimed }

    public class AchievementRowView : MonoBehaviour
    {
        public TMP_Text titleLabel;
        public TMP_Text subtitleLabel;
        public TMP_Text progressLabel;
        public Image progressFill;
        public TMP_Text rewardLabel;

        public Button statusButton;
        public Image statusButtonImage;
        public TMP_Text statusButtonLabel;
        public Sprite statusInProgressSprite;
        public Sprite statusClaimSprite;
        public Sprite statusCompleteSprite;

        private AchievementData _data;
        private System.Action<AchievementData> _onClaim;

        // A Filled-type Image only honours fillAmount when it has a sprite - with none it draws the
        // whole rectangle, so every progress bar read as 100% whatever the real progress was.
        private void Awake()
        {
            if (progressFill != null && progressFill.sprite == null)
                progressFill.sprite = SpaceHawk.Gameplay.ProceduralSprites.White;
        }

        /// <summary>Fills the row from plain values instead of an AchievementData - the Missions
        /// panel's daily-login, mission and bonus rows use the same row layout. `inProgressText`
        /// replaces the status button's default "IN PROGRESS" caption (e.g. with a countdown).</summary>
        public void SetupCustom(string title, string subtitle, int current, int target, int reward,
                                RowState state, System.Action onClaim, string inProgressText = null)
        {
            _data = null;
            int clamped = Mathf.Min(current, target);

            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;
            if (progressLabel != null) progressLabel.text = $"{clamped} / {target}";
            if (progressFill != null) progressFill.fillAmount = target > 0 ? (float)clamped / target : 0f;
            if (rewardLabel != null) rewardLabel.text = reward.ToString();

            if (statusButton == null) return;
            statusButton.onClick.RemoveAllListeners();
            switch (state)
            {
                case RowState.Claimed:
                    SetStatus(statusCompleteSprite, Localization.Get("achievements.complete"), false);
                    break;
                case RowState.Claimable:
                    SetStatus(statusClaimSprite, Localization.Get("achievements.claim"), true);
                    statusButton.onClick.AddListener(() => onClaim?.Invoke());
                    break;
                default:
                    SetStatus(statusInProgressSprite, inProgressText ?? Localization.Get("achievements.in_progress"), false);
                    break;
            }
        }

        public void Setup(AchievementData data, int currentCount, bool claimed, System.Action<AchievementData> onClaim)
        {
            _data = data;
            _onClaim = onClaim;

            int clamped = Mathf.Min(currentCount, data.targetCount);
            bool complete = currentCount >= data.targetCount;

            (string title, string subtitle) = AchievementTracker.DescribeGoal(data);
            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;
            if (progressLabel != null) progressLabel.text = $"{clamped} / {data.targetCount}";
            if (progressFill != null) progressFill.fillAmount = data.targetCount > 0 ? (float)clamped / data.targetCount : 0f;
            if (rewardLabel != null) rewardLabel.text = data.crystalReward.ToString();

            if (statusButton != null)
            {
                statusButton.onClick.RemoveAllListeners();
                if (claimed)
                {
                    SetStatus(statusCompleteSprite, Localization.Get("achievements.complete"), false);
                }
                else if (complete)
                {
                    SetStatus(statusClaimSprite, Localization.Get("achievements.claim"), true);
                    statusButton.onClick.AddListener(() => _onClaim?.Invoke(_data));
                }
                else
                {
                    SetStatus(statusInProgressSprite, Localization.Get("achievements.in_progress"), false);
                }
            }
        }

        private void SetStatus(Sprite sprite, string label, bool interactable)
        {
            if (statusButtonImage != null) statusButtonImage.sprite = sprite;
            if (statusButtonLabel != null) statusButtonLabel.text = label;
            statusButton.interactable = interactable;
        }
    }
}
