using UnityEngine;

namespace SpaceHawk.Core
{
    /// <summary>Thin wrapper around Handheld.Vibrate() - a no-op on platforms without a vibration
    /// motor (Editor, desktop), so call sites never need their own platform checks. Reuses the
    /// existing SFX toggle as a stand-in for "wants extra feedback" rather than adding a whole
    /// separate Settings option just for this.</summary>
    public static class HapticFeedback
    {
        public static void Play()
        {
            if (!SaveManager.Data.sfxEnabled) return;
            Handheld.Vibrate();
        }
    }
}
