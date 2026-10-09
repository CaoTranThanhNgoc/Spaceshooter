using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpaceHawk.Data;

namespace SpaceHawk.Core
{
    [Serializable]
    public class LevelStarEntry
    {
        public string levelId;
        public int stars;
    }

    [Serializable]
    public class LevelScoreEntry
    {
        public string levelId;
        public int score;
    }

    [Serializable]
    public class SaveData
    {
        public int crystals = 100;
        public int totalCrystalsEarned = 0;
        public float energyCurrent = SaveManager.MaxEnergy;
        public long lastEnergyUnixSeconds;
        public int highestUnlockedLevelIndex = 0;
        public List<LevelStarEntry> levelStars = new List<LevelStarEntry>();

        public int enemiesDestroyed = 0;
        public List<string> claimedAchievements = new List<string>();
        public List<string> notifiedAchievements = new List<string>();

        public long lastDailyRewardUnixSeconds = 0;
        public int dailyStreak = 0;

        public int missionDayKey = 0;
        public int[] missionProgress = new int[DailyMissions.Count];
        public bool[] missionClaimed = new bool[DailyMissions.Count];
        public bool missionBonusClaimed = false;

        public List<LevelScoreEntry> levelBestScores = new List<LevelScoreEntry>();
        public bool skillScoresMigrated = false;

        public int endlessBestWave = 0;
        public int endlessBestScore = 0;

        public int shipLevel = 1;
        // The ship roster (see ShipCatalog): index = family * 5 + tier. `unlockedShips` is the old
        // 3-hull format, kept only so saves from before the roster can be converted once.
        public int selectedShip = 0;
        public bool[] ownedShips = NewOwnedShips();
        public bool[] unlockedShips = new bool[] { true, false, false };
        public int shipRosterVersion = 0;

        private static bool[] NewOwnedShips()
        {
            bool[] owned = new bool[ShipCatalog.Count];
            owned[0] = true;
            return owned;
        }

        public float masterVolume = 1f;
        public bool sfxEnabled = true;
        public bool fullscreen = true;
        public int resolutionIndex = -1;
        public int language = 0; // Language enum: 0 = English, 1 = Vietnamese

        public bool hasSeenMoveTutorial = false;

        public string playerName = "";
        // Display-name bookkeeping (see NameService). The name was picked by the game, not by hand; the online identity
        // (PlayerId) whose server record holds the name - another identity registers it again; and the Unix time from
        // which the name may be changed by hand again (it can be changed once a week).
        public bool nameIsAuto = false;
        public string nameRegisteredFor = "";
        public long nameChangeUnlockUnix = 0;
        public bool accountLinked = false;
        public string accountUsername = "";
        // The e-mail that lets this account get a forgotten password reset (canonical form, see
        // RecoveryContact). Belongs to the account: it travels with the cloud snapshot, not the guest profile.
        public string recoveryContact = "";
        // True once the server confirmed the contact belongs to the player (a code was sent to it and typed back).
        public bool recoveryContactVerified = false;
    }

    /// <summary>
    /// Local persistence + economy. Plain static class (no scene object needed) so it
    /// works identically from MainMenu and Gameplay scenes without a DontDestroyOnLoad singleton.
    /// </summary>
    public static class SaveManager
    {
        public const int MaxEnergy = 10;
        public const int EnergyRegenSeconds = 300; // 1 point every 5 minutes
        public const long DailyRewardIntervalSeconds = 24 * 60 * 60;
        // Skipping more than one full extra day breaks the streak and starts over from day 1.
        public const long DailyStreakGraceSeconds = 2 * DailyRewardIntervalSeconds;
        private static readonly int[] DailyRewardByDay = { 50, 60, 75, 90, 110, 140, 200 };

        public const int ReviveCrystalCost = 40;
        public const int EnergyRefillCrystalCost = 30;

        public const int MaxShipLevel = 5;
        public const int ShipBaseHp = 100;
        public const int ShipHpPerLevel = 25;
        public const int ShipBaseDamage = 12;
        public const int ShipDamagePerLevel = 3;
        private static readonly int[] ShipUpgradeCost = { 100, 250, 500, 900 }; // cost from level i to i+1

        public const int ShipCount = ShipCatalog.Count;

        private static SaveData _data;
        private static bool _skipDiskWrites;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

        // The guest profile, put aside while an account is signed in so that logging out brings it
        // back untouched (see StashGuestProfile / RestoreGuestProfile).
        private static string GuestStashPath => Path.Combine(Application.persistentDataPath, "guest_savegame.json");
        private static string _guestStashJson;
        private static bool _guestStashLoaded;

        public static event Action<int> CrystalsChanged;
        public static event Action<int> EnergyChanged;
        public static event Action<int> EnemiesDestroyedChanged;
        public static event Action<int> SelectedShipChanged;
        /// <summary>The whole local profile was swapped (signing in to an account, back to the guest
        /// profile on log out) - screens built from save data (the level map) must rebuild.</summary>
        public static event Action ProfileChanged;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    _data = JsonUtility.FromJson<SaveData>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not read save file, starting fresh: {e.Message}");
            }

            if (_data == null) _data = new SaveData();
            NormalizeShips(_data);
            RegenEnergy();
            if (EnsureName(_data)) Save();
        }

        public static void Save()
        {
            if (_skipDiskWrites) return;

            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not write save file: {e.Message}");
            }
        }

        /// <summary>Grants whole energy points for every full interval elapsed since last check-in.</summary>
        public static void RegenEnergy()
        {
            SaveData d = Data;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (d.energyCurrent >= MaxEnergy)
            {
                d.lastEnergyUnixSeconds = now;
                return;
            }

            if (d.lastEnergyUnixSeconds <= 0)
            {
                d.lastEnergyUnixSeconds = now;
                return;
            }

            long elapsed = now - d.lastEnergyUnixSeconds;
            if (elapsed < EnergyRegenSeconds) return;

            int pointsGained = (int)(elapsed / EnergyRegenSeconds);
            int currentWhole = Mathf.FloorToInt(d.energyCurrent);
            int newEnergy = Mathf.Min(MaxEnergy, currentWhole + pointsGained);
            int actualGained = newEnergy - currentWhole;

            d.energyCurrent = newEnergy;
            d.lastEnergyUnixSeconds += (long)actualGained * EnergyRegenSeconds;

            if (actualGained > 0) EnergyChanged?.Invoke(newEnergy);
        }

        public static int GetEnergy()
        {
            RegenEnergy();
            return Mathf.FloorToInt(Data.energyCurrent);
        }

        public static int GetCrystals() => Data.crystals;

        public static bool TrySpendEnergy(int amount)
        {
            RegenEnergy();
            if (Data.energyCurrent < amount) return false;
            Data.energyCurrent -= amount;
            Save();
            EnergyChanged?.Invoke(GetEnergy());
            return true;
        }

        public static void AddCrystals(int amount)
        {
            if (amount == 0) return;
            Data.crystals += amount;
            // Lifetime total, not the spendable balance above - stays intact when crystals get
            // spent, so a "earn N crystals total" achievement isn't undone by upgrading a ship.
            if (amount > 0) Data.totalCrystalsEarned += amount;
            Save();
            CrystalsChanged?.Invoke(Data.crystals);
        }

        public static int GetTotalCrystalsEarned() => Data.totalCrystalsEarned;

        public static int GetLevelsCleared() => Data.highestUnlockedLevelIndex;

        public static bool TrySpendCrystals(int amount)
        {
            if (Data.crystals < amount) return false;
            Data.crystals -= amount;
            Save();
            CrystalsChanged?.Invoke(Data.crystals);
            return true;
        }

        public static int GetStars(string levelId)
        {
            List<LevelStarEntry> list = Data.levelStars;
            for (int i = 0; i < list.Count; i++)
                if (list[i].levelId == levelId) return list[i].stars;
            return 0;
        }

        public static void SetStars(string levelId, int stars)
        {
            List<LevelStarEntry> list = Data.levelStars;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].levelId == levelId)
                {
                    if (stars > list[i].stars)
                    {
                        list[i].stars = stars;
                        Save();
                    }
                    return;
                }
            }
            list.Add(new LevelStarEntry { levelId = levelId, stars = stars });
            Save();
        }

        /// <summary>Highest star rating earned on any single level - used by the "earn N stars on
        /// a level" achievement (as opposed to summing stars across all levels).</summary>
        public static int GetBestStars()
        {
            int best = 0;
            foreach (LevelStarEntry entry in Data.levelStars)
                if (entry.stars > best) best = entry.stars;
            return best;
        }

        public static bool IsLevelUnlocked(int levelIndex) => levelIndex <= Data.highestUnlockedLevelIndex;

        public static void UnlockLevel(int levelIndex)
        {
            if (levelIndex > Data.highestUnlockedLevelIndex)
            {
                Data.highestUnlockedLevelIndex = levelIndex;
                Save();
            }
        }

        public static int GetEnemiesDestroyed() => Data.enemiesDestroyed;

        public static int GetLevelBestScore(string levelId)
        {
            foreach (LevelScoreEntry entry in Data.levelBestScores)
                if (entry.levelId == levelId) return entry.score;
            return 0;
        }

        /// <summary>Keeps only the best score a level has ever produced. Returns true when this run
        /// beat the previous best (or was the first).</summary>
        public static bool SetLevelBestScore(string levelId, int score)
        {
            foreach (LevelScoreEntry entry in Data.levelBestScores)
            {
                if (entry.levelId != levelId) continue;
                if (score <= entry.score) return false;
                entry.score = score;
                Save();
                return true;
            }

            if (score <= 0) return false;
            Data.levelBestScores.Add(new LevelScoreEntry { levelId = levelId, score = score });
            Save();
            return true;
        }

        /// <summary>The Leaderboard number: the sum of the best score on every level plus the best
        /// Endless run (see SkillScore for how each is earned). Only ever goes up.</summary>
        public static long GetSkillRating()
        {
            long total = Data.endlessBestScore;
            foreach (LevelScoreEntry entry in Data.levelBestScores) total += entry.score;
            return total;
        }

        public static int GetEndlessBestWave() => Data.endlessBestWave;
        public static int GetEndlessBestScore() => Data.endlessBestScore;

        /// <summary>Records a finished Endless run. Returns true when it beat the best wave reached
        /// (a better score on an equal or lower wave still updates the score record, silently).</summary>
        public static bool RecordEndlessRun(int wave, int score)
        {
            bool newBestWave = wave > Data.endlessBestWave;
            if (newBestWave) Data.endlessBestWave = wave;
            if (score > Data.endlessBestScore) Data.endlessBestScore = score;
            Save();
            return newBestWave;
        }

        public static void AddEnemyKill()
        {
            Data.enemiesDestroyed++;
            DailyMissions.Report(MissionKind.DestroyEnemies, 1, persist: false);
            Save();
            EnemiesDestroyedChanged?.Invoke(Data.enemiesDestroyed);
        }

        public static bool IsAchievementClaimed(string achievementId) => Data.claimedAchievements.Contains(achievementId);

        public static bool TryClaimAchievement(string achievementId, int crystalReward)
        {
            if (Data.claimedAchievements.Contains(achievementId)) return false;
            Data.claimedAchievements.Add(achievementId);
            Save();
            AddCrystals(crystalReward);
            return true;
        }

        public static bool HasNotifiedAchievement(string achievementId) => Data.notifiedAchievements.Contains(achievementId);

        public static void MarkAchievementNotified(string achievementId)
        {
            if (Data.notifiedAchievements.Contains(achievementId)) return;
            Data.notifiedAchievements.Add(achievementId);
            Save();
        }

        /// <summary>Seconds remaining before the daily reward can be claimed again (0 = claimable now).</summary>
        public static long SecondsUntilNextDailyReward()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long elapsed = now - Data.lastDailyRewardUnixSeconds;
            long remaining = DailyRewardIntervalSeconds - elapsed;
            return remaining > 0 ? remaining : 0;
        }

        public static bool CanClaimDailyReward() => SecondsUntilNextDailyReward() <= 0;

        public static int DailyRewardDays => DailyRewardByDay.Length;

        public static int GetDailyRewardAmount(int day)
        {
            return DailyRewardByDay[Mathf.Clamp(day, 1, DailyRewardByDay.Length) - 1];
        }

        /// <summary>The day (1..7) the next claim will count as - back to 1 when the streak was
        /// broken by missing a day, or after completing day 7. This is what the Shop tile shows.</summary>
        public static int GetNextDailyRewardDay()
        {
            SaveData d = Data;
            if (d.lastDailyRewardUnixSeconds <= 0) return 1;

            long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - d.lastDailyRewardUnixSeconds;
            if (elapsed > DailyStreakGraceSeconds) return 1;

            int next = d.dailyStreak + 1;
            return next > DailyRewardByDay.Length ? 1 : Mathf.Max(1, next);
        }

        /// <summary>Returns the crystals granted, or 0 if it can't be claimed yet.</summary>
        public static int TryClaimDailyReward()
        {
            if (!CanClaimDailyReward()) return 0;

            int day = GetNextDailyRewardDay();
            int amount = GetDailyRewardAmount(day);
            Data.dailyStreak = day;
            Data.lastDailyRewardUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Save();
            AddCrystals(amount);
            return amount;
        }

        /// <summary>Adds energy, capped at MaxEnergy (never wastes a refill by overflowing).</summary>
        public static void AddEnergy(int amount)
        {
            if (amount <= 0) return;
            RegenEnergy();
            SaveData d = Data;
            d.energyCurrent = Mathf.Min(MaxEnergy, Mathf.Floor(d.energyCurrent) + amount);
            // Regen timestamp restarts from now so a partially-elapsed interval isn't paid out
            // on top of the refill.
            d.lastEnergyUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Save();
            EnergyChanged?.Invoke(Mathf.FloorToInt(d.energyCurrent));
        }

        public static bool TryRefillEnergyWithCrystals()
        {
            if (GetEnergy() >= MaxEnergy) return false;
            if (!TrySpendCrystals(EnergyRefillCrystalCost)) return false;
            AddEnergy(MaxEnergy);
            return true;
        }

        public static int GetShipLevel() => Mathf.Clamp(Data.shipLevel, 1, MaxShipLevel);

        /// <summary>Max HP of the worn ship: the shared upgrade level's HP times the ship's own multiplier.</summary>
        public static int GetShipMaxHp() => GetShipMaxHp(GetSelectedShip());

        public static int GetShipMaxHp(int shipIndex) =>
            Mathf.RoundToInt((ShipBaseHp + (GetShipLevel() - 1) * ShipHpPerLevel) * ShipCatalog.Get(shipIndex).hp);

        public static int GetShipDamage() => GetShipDamage(GetSelectedShip());

        public static int GetShipDamage(int shipIndex) =>
            Mathf.RoundToInt((ShipBaseDamage + (GetShipLevel() - 1) * ShipDamagePerLevel) * ShipCatalog.Get(shipIndex).dmg);

        /// <summary>-1 once the ship is already at MaxShipLevel.</summary>
        public static int GetShipUpgradeCost()
        {
            int level = GetShipLevel();
            if (level >= MaxShipLevel) return -1;
            return ShipUpgradeCost[level - 1];
        }

        public static bool TryUpgradeShip()
        {
            int cost = GetShipUpgradeCost();
            if (cost < 0) return false;
            if (!TrySpendCrystals(cost)) return false;
            Data.shipLevel = GetShipLevel() + 1;
            Save();
            return true;
        }

        public static int GetSelectedShip() => Mathf.Clamp(Data.selectedShip, 0, ShipCount - 1);

        public static bool IsShipUnlocked(int shipIndex)
        {
            if (shipIndex <= 0) return true;
            bool[] owned = Data.ownedShips;
            return owned != null && shipIndex < owned.Length && owned[shipIndex];
        }

        /// <summary>-1 for the always-owned starter.</summary>
        public static int GetShipUnlockCost(int shipIndex)
        {
            if (shipIndex <= 0 || shipIndex >= ShipCount) return -1;
            return ShipCatalog.Get(shipIndex).price;
        }

        public enum ShipRequirement { Met, PreviousShip, ClearedLevels }

        /// <summary>What still stands between the player and buying this ship, apart from the
        /// Crystals: owning the previous tier of its family first, then reaching its place in the
        /// campaign. `value` is the ship index to own or the number of levels to clear.</summary>
        public static ShipRequirement GetShipRequirement(int shipIndex, out int value)
        {
            value = 0;
            ShipSpec spec = ShipCatalog.Get(shipIndex);
            int previous = ShipCatalog.PreviousTier(spec.index);
            if (previous >= 0 && !IsShipUnlocked(previous))
            {
                value = previous;
                return ShipRequirement.PreviousShip;
            }
            if (GetLevelsCleared() < spec.requiresClearedLevels)
            {
                value = spec.requiresClearedLevels;
                return ShipRequirement.ClearedLevels;
            }
            return ShipRequirement.Met;
        }

        public static bool TryUnlockShip(int shipIndex)
        {
            if (IsShipUnlocked(shipIndex)) return false;
            if (GetShipRequirement(shipIndex, out _) != ShipRequirement.Met) return false;
            int cost = GetShipUnlockCost(shipIndex);
            if (cost < 0 || !TrySpendCrystals(cost)) return false;
            Data.ownedShips[shipIndex] = true;
            Save();
            return true;
        }

        /// <summary>One-off conversion of a save from before the roster (3 hulls whose look followed
        /// the upgrade level): the ships the player has actually flown - each owned hull's sprites up
        /// to their current upgrade level - become owned ships, and the one on screen stays worn.</summary>
        private static void NormalizeShips(SaveData d)
        {
            if (d == null) return;

            if (d.ownedShips == null || d.ownedShips.Length != ShipCatalog.Count)
            {
                bool[] owned = new bool[ShipCatalog.Count];
                if (d.ownedShips != null) Array.Copy(d.ownedShips, owned, Mathf.Min(d.ownedShips.Length, owned.Length));
                d.ownedShips = owned;
            }

            if (d.shipRosterVersion < 1)
            {
                int tech = Mathf.Clamp(d.shipLevel, 1, MaxShipLevel);
                bool[] legacy = d.unlockedShips;
                for (int family = 0; family < ShipCatalog.FamilyCount; family++)
                {
                    bool had = family == 0 || (legacy != null && family < legacy.Length && legacy[family]);
                    if (!had) continue;
                    for (int tier = 0; tier < tech; tier++) d.ownedShips[ShipCatalog.IndexOf(family, tier)] = true;
                }
                int legacyHull = Mathf.Clamp(d.selectedShip, 0, ShipCatalog.FamilyCount - 1);
                d.selectedShip = ShipCatalog.IndexOf(legacyHull, tech - 1);
                d.shipRosterVersion = 1;
            }

            d.ownedShips[0] = true;
            if (d.selectedShip < 0 || d.selectedShip >= ShipCatalog.Count || !d.ownedShips[d.selectedShip]) d.selectedShip = 0;
        }

        public static void SelectShip(int shipIndex)
        {
            if (!IsShipUnlocked(shipIndex) || Data.selectedShip == shipIndex) return;
            Data.selectedShip = shipIndex;
            Save();
            SelectedShipChanged?.Invoke(shipIndex);
        }

        public static bool HasSeenMoveTutorial() => Data.hasSeenMoveTutorial;

        public static void MarkMoveTutorialSeen()
        {
            if (Data.hasSeenMoveTutorial) return;
            Data.hasSeenMoveTutorial = true;
            Save();
        }

        public static string GetPlayerName() => Data.playerName;

        /// <summary>True while the name is one the game picked ("Pilot7k2q") rather than one the player chose.</summary>
        public static bool IsPlayerNameAuto() => Data.nameIsAuto;

        /// <summary>Remembers the name locally. A changed name is unknown to the server until NameService registers it.</summary>
        public static void SetPlayerName(string name, bool auto = false)
        {
            if (Data.playerName == name && Data.nameIsAuto == auto) return;
            if (Data.playerName != name) Data.nameRegisteredFor = "";
            Data.playerName = name;
            Data.nameIsAuto = auto;
            Save();
        }

        /// <summary>The profile has no name of its own (an account that never had one): it gets a fresh game-picked one.</summary>
        public static void ClearPlayerName()
        {
            Data.playerName = "";
            EnsureDefaultName();
        }

        /// <summary>Every profile always has a display name: one the game picked when the player has not chosen yet,
        /// so a guest shows up on the Leaderboard (and in the profile) under the same name from the start.</summary>
        public static string EnsureDefaultName()
        {
            if (EnsureName(Data)) Save();
            return Data.playerName;
        }

        // A profile always has a name; a signed-in account that has not picked one shows up under its account name
        // (not the game's "PilotXXXX") - on this device right away, whatever the server is doing.
        private static bool EnsureName(SaveData d)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(d.playerName))
            {
                d.playerName = PlayerNameRules.RandomDefaultName();
                d.nameIsAuto = true;
                d.nameRegisteredFor = "";
                d.nameChangeUnlockUnix = 0;
                changed = true;
            }

            if (d.accountLinked && d.nameIsAuto && PlayerNameRules.IsGameDefault(d.playerName))
            {
                string account = PlayerNameRules.AccountDefault(d.accountUsername);
                if (account != null)
                {
                    d.playerName = account;
                    d.nameRegisteredFor = "";
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>The server holds `name` for this online identity; `nextChangeAt` is when it may be changed by hand again.</summary>
        public static void MarkNameRegistered(string playerId, string name, long nextChangeAt)
        {
            Data.playerName = name;
            Data.nameRegisteredFor = playerId ?? "";
            Data.nameChangeUnlockUnix = nextChangeAt;
            Save();
        }

        public static bool IsNameRegisteredFor(string playerId) => !string.IsNullOrEmpty(playerId) && Data.nameRegisteredFor == playerId;

        /// <summary>The name was registered for ANOTHER online identity than `playerId` (the profile moved to a new one).</summary>
        public static bool IsNameRegisteredForAnother(string playerId) =>
            !string.IsNullOrEmpty(Data.nameRegisteredFor) && Data.nameRegisteredFor != playerId;

        /// <summary>The name belongs to another online identity than the one signed in now (the profile moved to a
        /// new identity): what the server knew about it - registration and weekly lock - does not apply.</summary>
        public static void ForgetNameRegistration()
        {
            Data.nameRegisteredFor = "";
            Data.nameChangeUnlockUnix = 0;
            Save();
        }

        public static void SetNameChangeUnlock(long unixSeconds)
        {
            if (Data.nameChangeUnlockUnix == unixSeconds) return;
            Data.nameChangeUnlockUnix = unixSeconds;
            Save();
        }

        /// <summary>Seconds until the name may be changed by hand again; 0 when it may be changed now.</summary>
        public static long GetNameChangeWaitSeconds()
        {
            long wait = Data.nameChangeUnlockUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return wait > 0 ? wait : 0;
        }

        // ---- who the player is, on this device (not part of any profile: it must survive logging out)

        private const string IdentityChosenKey = "SpaceHawk.IdentityChosen";
        private static bool _identityChosenForTests;

        /// <summary>True once the "who are you" gate was answered on this device: Continue as Guest, Create Account
        /// or Sign In (or a name chosen by hand / an account that predates the gate). The gate is for a brand-new
        /// install only - logging out or returning from a level never brings it back.</summary>
        public static bool HasChosenIdentity()
        {
            if (Data.accountLinked)
            {
                MarkIdentityChosen();
                return true;
            }
            if (!string.IsNullOrEmpty(Data.playerName) && !Data.nameIsAuto) return true;
            return _skipDiskWrites ? _identityChosenForTests : PlayerPrefs.GetInt(IdentityChosenKey, 0) == 1;
        }

        public static void MarkIdentityChosen()
        {
            if (_skipDiskWrites)
            {
                _identityChosenForTests = true;
                return;
            }
            if (PlayerPrefs.GetInt(IdentityChosenKey, 0) == 1) return;
            PlayerPrefs.SetInt(IdentityChosenKey, 1);
            PlayerPrefs.Save();
        }

        public static string GetRecoveryContact() => Data.recoveryContact ?? "";

        /// <summary>Only a VERIFIED contact is ever stored: the server refuses to attach any other.</summary>
        public static void SetRecoveryContact(string canonicalContact)
        {
            Data.recoveryContact = canonicalContact ?? "";
            Data.recoveryContactVerified = !string.IsNullOrEmpty(Data.recoveryContact);
            Save();
        }

        /// <summary>True when the account has a contact the server has verified (one from before
        /// verification existed counts as none - the player is asked to add and verify it again).</summary>
        public static bool HasVerifiedRecoveryContact() => Data.recoveryContactVerified && !string.IsNullOrEmpty(Data.recoveryContact);

        public static bool IsAccountLinked() => Data.accountLinked;
        public static string GetAccountUsername() => Data.accountUsername;

        public static void SetAccountLinked(bool linked, string username)
        {
            Data.accountLinked = linked;
            Data.accountUsername = linked ? username : "";
            if (linked) MarkIdentityChosen();
            EnsureName(Data);   // the account name becomes the display name unless one was chosen
            Save();
        }

        /// <summary>Replaces local progress with a cloud snapshot pulled down after signing in to
        /// an account on this device - see CloudSaveManager.FetchCloudSave. Identity fields
        /// (account link state, username, display name) are kept as THIS device's AccountManager
        /// just set them, not overwritten by whatever the snapshot happened to hold for them.</summary>
        public static void ApplyCloudData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            SaveData incoming;
            try
            {
                incoming = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not parse cloud save data: {e.Message}");
                return;
            }
            if (incoming == null) return;

            incoming.accountLinked = Data.accountLinked;
            incoming.accountUsername = Data.accountUsername;
            incoming.playerName = Data.playerName;
            CarryDeviceSettings(Data, incoming);

            EnsureName(incoming);
            _data = incoming;
            NormalizeShips(_data);
            RegenEnergy();
            Save();
            RaiseProfileEvents();
        }

        /// <summary>Volume, language, display and "seen the tutorial" belong to the device, not to
        /// whichever profile happens to be loaded - they must survive a profile swap.</summary>
        private static void CarryDeviceSettings(SaveData from, SaveData to)
        {
            to.masterVolume = from.masterVolume;
            to.sfxEnabled = from.sfxEnabled;
            to.fullscreen = from.fullscreen;
            to.resolutionIndex = from.resolutionIndex;
            to.language = from.language;
            to.hasSeenMoveTutorial |= from.hasSeenMoveTutorial;
        }

        private static void RaiseProfileEvents()
        {
            CrystalsChanged?.Invoke(_data.crystals);
            EnergyChanged?.Invoke(GetEnergy());
            EnemiesDestroyedChanged?.Invoke(_data.enemiesDestroyed);
            SelectedShipChanged?.Invoke(_data.selectedShip);
            ProfileChanged?.Invoke();
        }

        // ------------------------------------------------------------------ guest / account profiles

        /// <summary>True while a guest profile is waiting to be restored on log out.</summary>
        public static bool HasGuestStash
        {
            get
            {
                LoadGuestStash();
                return !string.IsNullOrEmpty(_guestStashJson);
            }
        }

        private static void LoadGuestStash()
        {
            if (_guestStashLoaded) return;
            _guestStashLoaded = true;
            if (_skipDiskWrites) return;
            try
            {
                if (File.Exists(GuestStashPath)) _guestStashJson = File.ReadAllText(GuestStashPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not read the guest profile: {e.Message}");
            }
        }

        private static void WriteGuestStash()
        {
            if (_skipDiskWrites) return;
            try
            {
                if (string.IsNullOrEmpty(_guestStashJson)) File.Delete(GuestStashPath);
                else File.WriteAllText(GuestStashPath, _guestStashJson);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not write the guest profile: {e.Message}");
            }
        }

        /// <summary>Puts the current progress aside as the guest profile. Called when a guest signs in to
        /// (or creates) an account, just before the progress on screen becomes that account's - so
        /// logging out can bring the guest progress back. Does nothing while an account is already
        /// signed in: that progress is not the guest's.</summary>
        public static void StashGuestProfile()
        {
            if (Data.accountLinked) return;
            LoadGuestStash();
            _guestStashJson = JsonUtility.ToJson(Data, true);
            WriteGuestStash();
        }

        /// <summary>The progress put aside is not needed any more - it moved into the account (a brand-new account
        /// has nothing of its own in the cloud, so the guest progress becomes its starting point).</summary>
        public static void DiscardGuestStash()
        {
            LoadGuestStash();
            _guestStashJson = null;
            WriteGuestStash();
        }

        /// <summary>Back to guest play after logging out (or deleting the account): the guest profile that
        /// was put aside comes back exactly as it was - or a fresh one if there never was any guest
        /// progress. The account's progress is not left behind on the device (it lives in the
        /// cloud). Device settings (volume, language...) stay as they are.</summary>
        public static void RestoreGuestProfile()
        {
            LoadGuestStash();

            SaveData guest = null;
            if (!string.IsNullOrEmpty(_guestStashJson))
            {
                try
                {
                    guest = JsonUtility.FromJson<SaveData>(_guestStashJson);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveManager] Guest profile unreadable, starting a fresh one: {e.Message}");
                }
            }
            if (guest == null) guest = new SaveData();

            CarryDeviceSettings(Data, guest);
            guest.accountLinked = false;
            guest.accountUsername = "";
            EnsureName(guest);   // a guest that starts fresh gets a name of its own, not the account's

            _data = guest;
            NormalizeShips(_data);
            RegenEnergy();

            _guestStashJson = null;
            WriteGuestStash();
            Save();
            RaiseProfileEvents();
        }

        public static void SetAudioSettings(float volume, bool sfxEnabled)
        {
            Data.masterVolume = volume;
            Data.sfxEnabled = sfxEnabled;
            Save();
        }

        public static void SetLanguage(int language)
        {
            Data.language = language;
            Save();
        }

        public static void SetDisplaySettings(bool fullscreen, int resolutionIndex)
        {
            Data.fullscreen = fullscreen;
            Data.resolutionIndex = resolutionIndex;
            Save();
        }

        /// <summary>Test-only hook so EditMode tests don't touch the real save file. Public because
        /// the EditMode tests live in a separate assembly (SpaceHawk.Tests.EditMode).</summary>
        public static void ResetForTests()
        {
            _data = new SaveData();
            NormalizeShips(_data);
            _guestStashJson = null;
            _guestStashLoaded = true;
            _identityChosenForTests = false;
            // Tests spend crystals, burn energy and so on - none of that may ever land in the
            // developer's real save file (the static flag resets itself on the next domain reload).
            _skipDiskWrites = true;
        }
    }
}
