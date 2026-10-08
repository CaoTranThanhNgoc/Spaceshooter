using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

namespace SpaceHawk.EditorTools
{
    /// <summary>Generates the Android launcher icons from the game's own ship art and assigns
    /// them in Player Settings - without this the app ships with Unity's default icon. Produces
    /// all three kinds Android uses: Adaptive (separate background + foreground layers that the
    /// launcher crops to a circle/squircle/etc), Round and Legacy (a flat composed square).</summary>
    public static class AppIconBuilder
    {
        private const string Folder = "Assets/AppIcon";
        private const string ShipPath = "Assets/Material/Ship_01/Ship_LVL_3.png";

        [MenuItem("Tools/Space Hawk/16. Build App Icons")]
        public static void Build()
        {
            if (!File.Exists(ShipPath))
            {
                Debug.LogWarning($"[AppIconBuilder] Missing {ShipPath} - icons not generated.");
                return;
            }

            Directory.CreateDirectory(Folder);
            Texture2D ship = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ship.LoadImage(File.ReadAllBytes(ShipPath));

            NamedBuildTarget android = NamedBuildTarget.Android;
            int assigned = 0;

            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                string kindName = kind.ToString();
                bool adaptive = kindName.Contains("Adaptive");
                bool round = kindName.Contains("Round");
                bool legacy = kindName.Contains("Legacy");
                if (!adaptive && !round && !legacy) continue;

                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(android, kind);
                for (int i = 0; i < icons.Length; i++)
                {
                    int size = icons[i].width;
                    string stem = $"{Folder}/icon_{(adaptive ? "adaptive" : round ? "round" : "legacy")}_{size}";

                    if (adaptive)
                    {
                        // Layers are 108dp canvases of which only the centre 66dp is guaranteed to
                        // survive the launcher's mask, so the ship stays well inside that.
                        Texture2D bg = SaveTexture(Compose(ship, size, 0f, false, false), stem + "_bg");
                        Texture2D fg = SaveTexture(Compose(ship, size, 0.56f, true, false), stem + "_fg");
                        icons[i].SetTextures(bg, fg);
                    }
                    else
                    {
                        Texture2D tex = SaveTexture(Compose(ship, size, round ? 0.66f : 0.76f, false, round), stem);
                        icons[i].SetTextures(tex);
                    }
                    assigned++;
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }

            Object.DestroyImmediate(ship);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AppIconBuilder] Generated and assigned {assigned} Android icons.");
        }

        /// <summary>shipFill = ship height as a fraction of the canvas (0 = none). foregroundOnly
        /// leaves the background transparent; circleMask clips to a circle (Round icons).</summary>
        private static Color32[] Compose(Texture2D ship, int size, float shipFill, bool foregroundOnly, bool circleMask)
        {
            Color32[] px = new Color32[size * size];
            float shipSize = size * shipFill;
            float shipLeft = (size - shipSize) * 0.5f;
            float shipBottom = (size - shipSize) * 0.5f;
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = foregroundOnly ? Color.clear : Background(x, y, size);

                    if (shipFill > 0f)
                    {
                        float u = (x - shipLeft) / shipSize;
                        float v = (y - shipBottom) / shipSize;
                        if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                        {
                            Color s = ship.GetPixelBilinear(u, v);
                            float a = s.a + c.a * (1f - s.a);
                            Color rgb = a > 0f ? (s * s.a + c * c.a * (1f - s.a)) / a : Color.clear;
                            c = new Color(rgb.r, rgb.g, rgb.b, a);
                        }
                    }

                    if (circleMask)
                    {
                        float dx = x + 0.5f - radius;
                        float dy = y + 0.5f - radius;
                        if (dx * dx + dy * dy > radius * radius) c = Color.clear;
                    }

                    px[y * size + x] = c;
                }
            }
            return px;
        }

        // Deep-space look: dark navy edges glowing to a purple-blue centre, plus a few fixed stars
        // (hashed from the pixel position so every icon size gets the same sky).
        private static Color Background(int x, int y, int size)
        {
            float nx = (x + 0.5f) / size - 0.5f;
            float ny = (y + 0.5f) / size - 0.5f;
            float d = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny) * 1.6f);
            Color center = new Color(0.20f, 0.18f, 0.48f, 1f);
            Color edge = new Color(0.02f, 0.03f, 0.12f, 1f);
            Color c = Color.Lerp(center, edge, d);

            uint h = (uint)(Mathf.FloorToInt(nx * 110f + 200f) * 73856093 ^ Mathf.FloorToInt(ny * 110f + 200f) * 19349663);
            if (h % 83 == 0) c = Color.Lerp(c, Color.white, 0.6f);
            return c;
        }

        private static Texture2D SaveTexture(Color32[] pixels, string pathWithoutExtension)
        {
            int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            string path = pathWithoutExtension + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
