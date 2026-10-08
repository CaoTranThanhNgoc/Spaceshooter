using System.Collections.Generic;
using UnityEngine;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    /// <summary>Call after any player stat that feeds an achievement changes (kills, ship level,
    /// stars) - shows one toast per achievement that just crossed its target for the first time.
    /// Without this, achievements only ever surfaced if the player happened to open the
    /// Achievements panel and noticed a row had filled up.</summary>
    public static class AchievementNotifier
    {
        private static AchievementDatabase _database;

        private static AchievementDatabase Database
        {
            get
            {
                if (_database == null) _database = Resources.Load<AchievementDatabase>("Data/AchievementDatabase");
                return _database;
            }
        }

        public static void CheckAndToast(Transform toastRoot)
        {
            List<AchievementData> newlyCompleted = AchievementTracker.CheckNewlyCompleted(Database);
            if (newlyCompleted == null) return;

            foreach (AchievementData data in newlyCompleted)
            {
                (string title, string _) = AchievementTracker.DescribeGoal(data);
                ToastUI.ShowToast(toastRoot, Localization.Format("achievements.unlocked_fmt", title));
                AudioManager.Play(GameAudio.Pickup, 0.6f);
            }
        }
    }
}
