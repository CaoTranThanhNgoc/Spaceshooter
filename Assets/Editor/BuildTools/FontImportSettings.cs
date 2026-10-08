using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using TMPro;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds a TextMeshPro SDF Font Asset from Roboto (Apache-2.0, safe to bundle in a
    /// shipped game - unlike Windows system fonts such as Segoe UI/Arial, which are not freely
    /// redistributable) and makes it the project's default TMP font. Roboto has full Vietnamese
    /// Unicode coverage, unlike TMP's built-in LiberationSans SDF default. Uses a DYNAMIC atlas
    /// (same as Unity's own "Create > Font Asset > SDF" menu command), so glyphs - Vietnamese
    /// diacritics included - are added to the atlas the first time each one is actually displayed,
    /// instead of needing every character pre-baked up front.</summary>
    public static class FontImportSettings
    {
        private const string FontFolder = "Assets/Fonts";

        [MenuItem("Tools/Space Hawk/1c. Apply Font Import Settings")]
        public static void ApplyAll()
        {
            TMP_FontAsset regular = EnsureFontAsset("Roboto-Regular");
            EnsureFontAsset("Roboto-Bold");

            if (regular != null && TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != regular)
            {
                TMP_Settings.defaultFontAsset = regular;
                EditorUtility.SetDirty(TMP_Settings.instance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FontImportSettings] Font import settings applied.");
        }

        private static TMP_FontAsset EnsureFontAsset(string fontName)
        {
            string sourcePath = $"{FontFolder}/{fontName}.ttf";
            string assetPath = $"{FontFolder}/{fontName} SDF.asset";

            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[FontImportSettings] Missing font file: {sourcePath}");
                return null;
            }

            System.IO.Directory.CreateDirectory(FontFolder);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[FontImportSettings] Failed to create font asset for {fontName}.");
                return null;
            }

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            if (fontAsset.atlasTexture != null) AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            if (fontAsset.material != null) AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            EditorUtility.SetDirty(fontAsset);

            return fontAsset;
        }
    }
}
