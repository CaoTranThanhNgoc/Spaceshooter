using UnityEngine;
using UnityEditor;

namespace SpaceHawk.EditorTools
{
    /// <summary>Configures the 6 real audio clips under Resources/Audio: the long looping
    /// background track streams from disk instead of decompressing ~6.5MB into memory up front,
    /// while the short SFX decompress on load for snappy, low-latency playback.</summary>
    public static class AudioImportSettings
    {
        private const string AudioFolder = "Assets/Resources/Audio";

        [MenuItem("Tools/Space Hawk/1b. Apply Audio Import Settings")]
        public static void ApplyAll()
        {
            Apply("1_audio_background", AudioClipLoadType.Streaming);
            Apply("2_audio_shooting", AudioClipLoadType.DecompressOnLoad);
            Apply("3_audio_effect_explosion", AudioClipLoadType.DecompressOnLoad);
            Apply("4_audio_touching_item", AudioClipLoadType.DecompressOnLoad);
            Apply("5_audio_victory", AudioClipLoadType.DecompressOnLoad);
            Apply("6_audio_gameover", AudioClipLoadType.DecompressOnLoad);
            AssetDatabase.SaveAssets();
            Debug.Log("[AudioImportSettings] Audio import settings applied.");
        }

        private static void Apply(string fileName, AudioClipLoadType loadType)
        {
            string path = $"{AudioFolder}/{fileName}.mp3";
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[AudioImportSettings] Missing audio file: {path}");
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == loadType) return; // already correct - skip the reimport

            settings.loadType = loadType;
            importer.defaultSampleSettings = settings;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
