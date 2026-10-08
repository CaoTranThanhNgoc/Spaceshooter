using UnityEngine;

namespace SpaceHawk.Core
{
    /// <summary>Plays one-shot SFX through a small pooled set of AudioSources on a persistent
    /// GameObject, so it survives scene loads without needing to be wired into every scene.
    /// Respects the Settings SFX toggle; the master volume slider already affects it for free via
    /// AudioListener.volume.</summary>
    public static class AudioManager
    {
        private const int PoolSize = 6;

        private static GameObject _root;
        private static AudioSource[] _pool;
        private static int _nextIndex;

        /// <summary>Creates the persistent AudioListener + SFX pool if they don't exist yet.
        /// Neither scene in this project has its own camera-attached AudioListener (MainMenu's
        /// Canvas is Screen Space Overlay and needs no camera at all), so Unity would otherwise
        /// stay completely silent - call this before playing anything through your own
        /// AudioSource too (e.g. background music), not just through AudioManager.Play.</summary>
        public static void EnsureInitialized()
        {
            if (_root != null) return;

            _root = new GameObject("AudioManager");
            Object.DontDestroyOnLoad(_root);
            _root.AddComponent<AudioListener>();

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource src = _root.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // plain 2D SFX, not attenuated/panned by distance from the camera
                _pool[i] = src;
            }
        }

        private static AudioSource NextSource()
        {
            EnsureInitialized();
            AudioSource chosen = _pool[_nextIndex];
            _nextIndex = (_nextIndex + 1) % PoolSize;
            return chosen;
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || !SaveManager.Data.sfxEnabled) return;
            AudioSource source = NextSource();
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
