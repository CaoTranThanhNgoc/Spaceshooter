using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    /// <summary>Drives a fixed set of pre-built row slots (baked into the prefab at build time,
    /// in the same order as the database) rather than instantiating rows at runtime.</summary>
    public class AchievementsPanel : MonoBehaviour
    {
        public AchievementDatabase database;
        public AchievementRowView[] rows;
        public Button closeButton;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            Populate();
            SaveManager.EnemiesDestroyedChanged += OnProgressChanged;
            Localization.LanguageChanged += Populate;
        }

        private void OnDisable()
        {
            SaveManager.EnemiesDestroyedChanged -= OnProgressChanged;
            Localization.LanguageChanged -= Populate;
        }

        private void Populate()
        {
            if (database == null || database.achievements == null || rows == null) return;

            int count = Mathf.Min(rows.Length, database.achievements.Length);
            for (int i = 0; i < count; i++)
            {
                AchievementData data = database.achievements[i];
                if (data == null || rows[i] == null) continue;
                bool claimed = SaveManager.IsAchievementClaimed(data.achievementId);
                rows[i].Setup(data, AchievementTracker.GetProgress(data), claimed, OnClaim);
            }
        }

        private void OnClaim(AchievementData data)
        {
            if (SaveManager.TryClaimAchievement(data.achievementId, data.crystalReward))
            {
                AudioManager.Play(GameAudio.Pickup, 0.55f);
                Populate();
            }
        }

        private void OnProgressChanged(int current)
        {
            Populate();
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
