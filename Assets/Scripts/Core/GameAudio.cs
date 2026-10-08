using UnityEngine;

namespace SpaceHawk.Core
{
    /// <summary>The 6 real audio clips under Resources/Audio, lazily loaded and cached. Covers
    /// shooting/explosion/pickup/victory/defeat SFX plus the gameplay background music; anything
    /// not covered by these 6 (button clicks, the player-hit thud) still uses ProceduralAudio.</summary>
    public static class GameAudio
    {
        private const string Folder = "Audio/";

        private static AudioClip _background, _shoot, _explosion, _pickup, _victory, _gameOver;

        public static AudioClip Background => _background != null ? _background : (_background = Load("1_audio_background"));
        public static AudioClip Shoot => _shoot != null ? _shoot : (_shoot = Load("2_audio_shooting"));
        public static AudioClip Explosion => _explosion != null ? _explosion : (_explosion = Load("3_audio_effect_explosion"));
        public static AudioClip Pickup => _pickup != null ? _pickup : (_pickup = Load("4_audio_touching_item"));
        public static AudioClip Victory => _victory != null ? _victory : (_victory = Load("5_audio_victory"));
        public static AudioClip GameOver => _gameOver != null ? _gameOver : (_gameOver = Load("6_audio_gameover"));

        private static AudioClip Load(string name)
        {
            AudioClip clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning($"[GameAudio] Missing audio clip: {Folder}{name}");
            return clip;
        }
    }
}
