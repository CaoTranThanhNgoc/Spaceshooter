using System;
using UnityEngine;

namespace SpaceHawk.Core
{
    public enum MissionKind
    {
        DestroyEnemies,   // kill N enemies (any mode)
        ClearLevels,      // win N campaign levels
        CollectPowerUps,  // pick up N power-ups
        ThreeStarWins,    // win N levels with 3 stars
        EndlessWave,      // reach wave N in one Endless run
    }

    public struct MissionInfo
    {
        public MissionKind kind;
        public int target;
        public int reward;
    }

    /// <summary>Three small goals a day, picked from a pool by the calendar date (so every launch
    /// that day sees the same three), tracked from real gameplay events and paid out in Crystals
    /// when claimed - plus a bonus for finishing all three. Progress lives in SaveData and resets by
    /// itself the first time it is read on a new local day.</summary>
    public static class DailyMissions
    {
        public const int Count = 3;
        public const int BonusReward = 60;

        private struct Template
        {
            public MissionKind kind;
            public int[] targets;
            public int[] rewards;
        }

        // Same index in targets/rewards = the same difficulty tier; the date picks the tier.
        private static readonly Template[] Pool =
        {
            new Template { kind = MissionKind.DestroyEnemies,  targets = new[] { 40, 70, 100 }, rewards = new[] { 25, 35, 50 } },
            new Template { kind = MissionKind.ClearLevels,     targets = new[] { 1, 2, 3 },     rewards = new[] { 30, 45, 60 } },
            new Template { kind = MissionKind.CollectPowerUps, targets = new[] { 4, 6, 9 },     rewards = new[] { 25, 35, 50 } },
            new Template { kind = MissionKind.ThreeStarWins,   targets = new[] { 1, 1, 2 },     rewards = new[] { 40, 40, 70 } },
            new Template { kind = MissionKind.EndlessWave,     targets = new[] { 5, 8, 12 },    rewards = new[] { 40, 55, 80 } },
        };

        /// <summary>Raised whenever progress or a claim changes - the Missions panel refreshes on it.</summary>
        public static event Action Changed;
        /// <summary>Raised once, with the mission index, the moment a mission reaches its target.</summary>
        public static event Action<int> MissionCompleted;

        /// <summary>Test hook: lets a test pretend it is another day.</summary>
        public static Func<DateTime> Clock = () => DateTime.Now;

        public static int TodayKey
        {
            get
            {
                DateTime now = Clock();
                return now.Year * 10000 + now.Month * 100 + now.Day;
            }
        }

        /// <summary>Time left until the next set of missions (local midnight).</summary>
        public static TimeSpan TimeUntilReset
        {
            get
            {
                DateTime now = Clock();
                return now.Date.AddDays(1) - now;
            }
        }

        public static MissionInfo Get(int index)
        {
            EnsureToday();
            return Generate(SaveManager.Data.missionDayKey, index);
        }

        /// <summary>Short heading, e.g. "DESTROY ENEMIES".</summary>
        public static string GetTitle(MissionInfo info)
        {
            return Localization.Get("missions.kind_" + KeyOf(info.kind));
        }

        /// <summary>The full goal with its number, e.g. "Destroy 70 enemies".</summary>
        public static string Describe(MissionInfo info)
        {
            return Localization.Format("missions.kind_" + KeyOf(info.kind) + "_fmt", info.target);
        }

        private static string KeyOf(MissionKind kind)
        {
            switch (kind)
            {
                case MissionKind.ClearLevels: return "levels";
                case MissionKind.CollectPowerUps: return "powerups";
                case MissionKind.ThreeStarWins: return "stars";
                case MissionKind.EndlessWave: return "endless";
                default: return "kills";
            }
        }

        public static int GetProgress(int index)
        {
            EnsureToday();
            return Mathf.Min(SaveManager.Data.missionProgress[index], Get(index).target);
        }

        public static bool IsComplete(int index) => GetProgress(index) >= Get(index).target;

        public static bool IsClaimed(int index)
        {
            EnsureToday();
            return SaveManager.Data.missionClaimed[index];
        }

        public static int CompletedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (IsComplete(i)) n++;
                return n;
            }
        }

        public static bool IsBonusClaimed
        {
            get
            {
                EnsureToday();
                return SaveManager.Data.missionBonusClaimed;
            }
        }

        public static bool CanClaimBonus => !IsBonusClaimed && CompletedCount >= Count;

        /// <summary>Adds `amount` to every today-mission of this kind. Pass persist:false from a
        /// caller that saves right afterwards anyway (the per-kill path).</summary>
        public static void Report(MissionKind kind, int amount = 1, bool persist = true)
        {
            if (amount <= 0) return;
            Apply(kind, (current, target) => current + amount, persist);
        }

        /// <summary>For "reach wave N"-style goals: progress becomes the best value seen today
        /// rather than a running total.</summary>
        public static void ReportMax(MissionKind kind, int value)
        {
            Apply(kind, (current, target) => Mathf.Max(current, value), true);
        }

        public static bool TryClaim(int index)
        {
            if (index < 0 || index >= Count || IsClaimed(index) || !IsComplete(index)) return false;
            SaveManager.Data.missionClaimed[index] = true;
            SaveManager.Save();
            SaveManager.AddCrystals(Get(index).reward);
            Changed?.Invoke();
            return true;
        }

        public static bool TryClaimBonus()
        {
            if (!CanClaimBonus) return false;
            SaveManager.Data.missionBonusClaimed = true;
            SaveManager.Save();
            SaveManager.AddCrystals(BonusReward);
            Changed?.Invoke();
            return true;
        }

        private static void Apply(MissionKind kind, Func<int, int, int> update, bool persist)
        {
            EnsureToday();
            SaveData data = SaveManager.Data;
            bool changed = false;

            for (int i = 0; i < Count; i++)
            {
                MissionInfo info = Generate(data.missionDayKey, i);
                if (info.kind != kind) continue;

                int before = Mathf.Min(data.missionProgress[i], info.target);
                int after = Mathf.Min(update(data.missionProgress[i], info.target), info.target);
                if (after == before) continue;

                data.missionProgress[i] = after;
                changed = true;
                if (before < info.target && after >= info.target) MissionCompleted?.Invoke(i);
            }

            if (!changed) return;
            if (persist) SaveManager.Save();
            Changed?.Invoke();
        }

        /// <summary>First read on a new day wipes yesterday's progress and claims.</summary>
        private static void EnsureToday()
        {
            SaveData data = SaveManager.Data;
            int today = TodayKey;
            if (data.missionDayKey == today && data.missionProgress != null && data.missionProgress.Length == Count
                && data.missionClaimed != null && data.missionClaimed.Length == Count) return;

            data.missionDayKey = today;
            data.missionProgress = new int[Count];
            data.missionClaimed = new bool[Count];
            data.missionBonusClaimed = false;
            SaveManager.Save();
        }

        /// <summary>The same date always yields the same three missions: kinds are a seeded shuffle
        /// of the pool (so all three differ), tiers are seeded too.</summary>
        private static MissionInfo Generate(int dayKey, int index)
        {
            System.Random rng = new System.Random(dayKey);

            int[] order = new int[Pool.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            int[] tiers = new int[Count];
            for (int i = 0; i < Count; i++) tiers[i] = rng.Next(3);

            Template template = Pool[order[index]];
            return new MissionInfo { kind = template.kind, target = template.targets[tiers[index]], reward = template.rewards[tiers[index]] };
        }
    }
}
