using System;
using UnityEngine;
using Unity.Services.Authentication;
using SpaceHawk.Core;

namespace SpaceHawk.Online
{
    /// <summary>Where each online identity keeps its cached session. Unity Authentication keeps one cached
    /// session per "profile": the guest identity lives in the default slot and an account the player signs
    /// in to lives in its own slot, so signing in never throws the guest identity away (its Leaderboard
    /// entry and name would be orphaned), a failed sign-in leaves the guest untouched, and logging out
    /// returns to exactly the guest identity that was there before.
    ///
    /// An account that was created FROM a guest (Register) lives where that guest did - the guest identity
    /// became the account. Logging out clears that slot and the next guest identity starts there.</summary>
    public static class AuthProfiles
    {
        public const string GuestSlot = "default";
        public const string SignedInSlot = "account";
        private const string AccountSlotKey = "SpaceHawk.AccountAuthSlot";

        /// <summary>The slot holding the signed-in account's session (the guest slot for an account made from a guest).</summary>
        public static string AccountSlot
        {
            get => PlayerPrefs.GetString(AccountSlotKey, GuestSlot);
            set
            {
                PlayerPrefs.SetString(AccountSlotKey, value);
                PlayerPrefs.Save();
            }
        }

        public static void ForgetAccountSlot()
        {
            PlayerPrefs.DeleteKey(AccountSlotKey);
            PlayerPrefs.Save();
        }

        /// <summary>The slot the next sign-in has to use for whoever is playing right now.</summary>
        public static string SlotToUse => SaveManager.IsAccountLinked() ? AccountSlot : GuestSlot;

        /// <summary>Points the service at `slot`. Only possible while signed out; a slot that is already current is left alone.</summary>
        public static void Switch(string slot)
        {
            try
            {
                if (AuthenticationService.Instance.Profile != slot) AuthenticationService.Instance.SwitchProfile(slot);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthProfiles] Could not switch to '{slot}': {e.Message}");
            }
        }
    }
}
