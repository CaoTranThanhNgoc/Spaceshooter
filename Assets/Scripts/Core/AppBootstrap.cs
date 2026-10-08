using UnityEngine;

namespace SpaceHawk.Core
{
    /// <summary>One-time startup settings that must apply before either scene runs. Unity's
    /// Android default is 30 FPS, which makes a fast shooter feel noticeably choppy - this asks
    /// for 60 (phones that can't sustain it just drop lower on their own).</summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyPlatformSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            // A level is played with a single finger on the glass - the screen must not dim and
            // lock halfway through a fight just because nothing else was touched.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
