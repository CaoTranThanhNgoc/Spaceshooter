using System.Collections.Generic;
using SpaceHawk.Core;

namespace SpaceHawk.Data
{
    /// <summary>Shared achievement-progress logic - what AchievementRowView and AchievementsPanel
    /// each computed on their own, plus detecting when one crosses its target for the first time
    /// so gameplay can show a real-time toast instead of the player only finding out by opening
    /// the Achievements panel later.</summary>
    public static class AchievementTracker
    {
        public static int GetProgress(AchievementData data)
        {
            switch (data.kind)
            {
                case AchievementKind.ShipLevel: return SaveManager.GetShipLevel();
                case AchievementKind.StarsOnAnyLevel: return SaveManager.GetBestStars();
                case AchievementKind.LevelsCleared: return SaveManager.GetLevelsCleared();
                case AchievementKind.CrystalsEarned: return SaveManager.GetTotalCrystalsEarned();
                case AchievementKind.EndlessWave: return SaveManager.GetEndlessBestWave();
                default: return SaveManager.GetEnemiesDestroyed();
            }
        }

        public static (string title, string subtitle) DescribeGoal(AchievementData data)
        {
            switch (data.kind)
            {
                case AchievementKind.ShipLevel:
                    return data.targetCount >= SaveManager.MaxShipLevel
                        ? (Localization.Get("ach.ship_level.max_title"), Localization.Get("ach.ship_level.max_subtitle"))
                        : (Localization.Get("ach.ship_level.title"), Localization.Format("ach.ship_level.subtitle_fmt", data.targetCount));
                case AchievementKind.StarsOnAnyLevel:
                    return (Localization.Get("ach.stars.title"), Localization.Format("ach.stars.subtitle_fmt", data.targetCount));
                case AchievementKind.LevelsCleared:
                    return (Localization.Get("ach.levels.title"), Localization.Format("ach.levels.subtitle_fmt", data.targetCount));
                case AchievementKind.CrystalsEarned:
                    return (Localization.Get("ach.crystals.title"), Localization.Format("ach.crystals.subtitle_fmt", data.targetCount));
                case AchievementKind.EndlessWave:
                    return (Localization.Get("ach.endless.title"), Localization.Format("ach.endless.subtitle_fmt", data.targetCount));
                default:
                    return (Localization.Get("ach.kills.title"), Localization.Format("ach.kills.subtitle_fmt", data.targetCount));
            }
        }

        /// <summary>Achievements that just crossed their target for the first time - not yet
        /// claimed, not yet notified about. Marks each as notified (persisted) so this never
        /// fires twice for the same achievement, however many more kills/upgrades/stars follow.
        /// Null when nothing new completed.</summary>
        public static List<AchievementData> CheckNewlyCompleted(AchievementDatabase db)
        {
            if (db == null || db.achievements == null) return null;

            List<AchievementData> result = null;
            foreach (AchievementData data in db.achievements)
            {
                if (data == null) continue;
                if (SaveManager.IsAchievementClaimed(data.achievementId)) continue;
                if (SaveManager.HasNotifiedAchievement(data.achievementId)) continue;
                if (GetProgress(data) < data.targetCount) continue;

                SaveManager.MarkAchievementNotified(data.achievementId);
                result ??= new List<AchievementData>();
                result.Add(data);
            }
            return result;
        }
    }
}
