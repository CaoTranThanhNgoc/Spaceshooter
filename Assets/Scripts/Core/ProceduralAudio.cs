using UnityEngine;

namespace SpaceHawk.Core
{
    /// <summary>Generates the couple of short SFX clips that aren't covered by the 6 real audio
    /// files under Resources/Audio (button click, player-hit thud), cached after first use.</summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static AudioClip _hit, _click;

        public static AudioClip Hit => _hit != null ? _hit : (_hit = BuildHit());
        public static AudioClip Click => _click != null ? _click : (_click = BuildClick());

        private static AudioClip BuildHit()
        {
            int samples = (int)(SampleRate * 0.15f);
            float[] data = new float[samples];
            System.Random rng = new System.Random(2);
            float smoothed = 0f;
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)samples;
                float envelope = Mathf.Pow(1f - progress, 2.5f);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                smoothed = smoothed * 0.75f + noise * 0.25f;
                phase += 160f / SampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * phase);
                data[i] = (smoothed * 0.5f + thud * 0.5f) * envelope * 0.55f;
            }
            return MakeClip("Hit", data);
        }

        private static AudioClip BuildClick()
        {
            int samples = (int)(SampleRate * 0.045f);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)samples;
                float envelope = Mathf.Pow(1f - progress, 3f);
                phase += 1000f / SampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * phase) * envelope * 0.4f;
            }
            return MakeClip("Click", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
