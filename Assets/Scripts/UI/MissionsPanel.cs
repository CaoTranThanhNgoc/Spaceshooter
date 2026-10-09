using System;
using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Core;

namespace SpaceHawk.UI
{
    /// <summary>The daily hub: the login-streak reward, today's three missions, and a bonus for
    /// finishing all three - five rows of the same layout the Achievements list uses.</summary>
    public class MissionsPanel : MonoBehaviour
    {
        public AchievementRowView dailyRow;
        public AchievementRowView[] missionRows;
        public AchievementRowView bonusRow;
        public Button closeButton;

        private float _refreshTimer;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            Populate();
            DailyMissions.Changed += Populate;
            SaveManager.ProfileChanged += Populate;   // another account / the guest took over while this is open
            Localization.LanguageChanged += Populate;
        }

        private void OnDisable()
        {
            DailyMissions.Changed -= Populate;
            SaveManager.ProfileChanged -= Populate;
            Localization.LanguageChanged -= Populate;
        }

        // Countdowns (next login reward, mission reset) tick once a second.
        private void Update()
        {
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < 1f) return;
            _refreshTimer = 0f;
            Populate();
        }

        private void Populate()
        {
            PopulateDaily();
            PopulateMissions();
            PopulateBonus();
        }

        private void PopulateDaily()
        {
            if (dailyRow == null) return;

            bool canClaim = SaveManager.CanClaimDailyReward();
            int day = SaveManager.GetNextDailyRewardDay();
            int amount = SaveManager.GetDailyRewardAmount(day);
            // Days already collected in the current streak - the claimable day isn't counted yet.
            int collected = canClaim ? day - 1 : Mathf.Clamp(SaveManager.Data.dailyStreak, 0, SaveManager.DailyRewardDays);

            dailyRow.SetupCustom(
                Localization.Get("missions.daily_login"),
                Localization.Format("missions.daily_login_sub_fmt", Mathf.Min(day, SaveManager.DailyRewardDays), SaveManager.DailyRewardDays),
                collected, SaveManager.DailyRewardDays, amount,
                canClaim ? RowState.Claimable : RowState.InProgress,
                ClaimDaily,
                FormatTime(SaveManager.SecondsUntilNextDailyReward()));
        }

        private void PopulateMissions()
        {
            if (missionRows == null) return;
            for (int i = 0; i < missionRows.Length && i < DailyMissions.Count; i++)
            {
                if (missionRows[i] == null) continue;
                int index = i;
                MissionInfo info = DailyMissions.Get(i);
                RowState state = DailyMissions.IsClaimed(i) ? RowState.Claimed
                    : DailyMissions.IsComplete(i) ? RowState.Claimable : RowState.InProgress;

                missionRows[i].SetupCustom(
                    DailyMissions.GetTitle(info),
                    DailyMissions.Describe(info),
                    DailyMissions.GetProgress(i), info.target, info.reward,
                    state, () => ClaimMission(index));
            }
        }

        private void PopulateBonus()
        {
            if (bonusRow == null) return;

            RowState state = DailyMissions.IsBonusClaimed ? RowState.Claimed
                : DailyMissions.CanClaimBonus ? RowState.Claimable : RowState.InProgress;

            bonusRow.SetupCustom(
                Localization.Get("missions.all_done"),
                Localization.Format("missions.resets_fmt", FormatTime((long)DailyMissions.TimeUntilReset.TotalSeconds)),
                DailyMissions.CompletedCount, DailyMissions.Count, DailyMissions.BonusReward,
                state, ClaimBonus);
        }

        private static string FormatTime(long totalSeconds)
        {
            long h = totalSeconds / 3600;
            long m = (totalSeconds % 3600) / 60;
            long s = totalSeconds % 60;
            return $"{h:00}:{m:00}:{s:00}";
        }

        private void ClaimDaily()
        {
            int amount = SaveManager.TryClaimDailyReward();
            if (amount <= 0) return;
            Celebrate(amount);
            Populate();
        }

        private void ClaimMission(int index)
        {
            int reward = DailyMissions.Get(index).reward;
            if (DailyMissions.TryClaim(index)) Celebrate(reward);
        }

        private void ClaimBonus()
        {
            if (DailyMissions.TryClaimBonus()) Celebrate(DailyMissions.BonusReward);
        }

        private void Celebrate(int crystals)
        {
            AudioManager.Play(GameAudio.Pickup, 0.55f);
            ToastUI.ShowToast(transform, Localization.Format("common.crystals_fmt", crystals));
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
