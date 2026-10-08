using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace SpaceHawk.EditorTools
{
    /// <summary>
    /// One-time pass over the two imported asset packs: makes sure every PNG is set up as a
    /// UI-ready Sprite, and gives the resizable "box" graphics (panels, buttons, bars) a
    /// 9-slice border so they can stretch to different container sizes without distorting
    /// their rounded corners.
    /// </summary>
    public static class UISpriteImportSettings
    {
        // filename (no extension) -> border in pixels, Unity order (left, bottom, right, top)
        private static readonly Dictionary<string, Vector4> SlicedBorders = new Dictionary<string, Vector4>
        {
            { "BoxMenu", new Vector4(40, 40, 40, 40) },
            { "DarkBackground", new Vector4(40, 40, 40, 40) },
            { "Normal_LongBtn", new Vector4(20, 20, 20, 20) },
            { "Hover_LongBtn", new Vector4(20, 20, 20, 20) },
            { "Disable_LongBtn", new Vector4(20, 20, 20, 20) },
            { "Normal_Btn", new Vector4(20, 20, 20, 20) },
            { "Hover_Btn", new Vector4(20, 20, 20, 20) },
            { "Disable_Btn", new Vector4(20, 20, 20, 20) },
            { "TitleBox", new Vector4(30, 10, 30, 10) },
            { "BgHudBar", new Vector4(18, 10, 18, 10) },
            { "HealthBar", new Vector4(10, 8, 10, 8) },
            { "HealthBar_Line", new Vector4(10, 8, 10, 8) },
            { "CrystalBarHUD", new Vector4(24, 20, 24, 20) },
            { "Achievement_ProgressBar", new Vector4(16, 14, 16, 14) },
            { "Achievement_Box", new Vector4(20, 20, 20, 20) },
            { "Achievement_statusBox", new Vector4(14, 12, 14, 12) },
            { "Achievement_CompleteBOx", new Vector4(14, 12, 14, 12) },
            { "Leaderboard_Box", new Vector4(20, 20, 20, 20) },
            { "Shop_Box", new Vector4(24, 24, 24, 24) },
            { "Shop_WeeklySaleBox", new Vector4(24, 24, 24, 24) },
            { "BgItemList", new Vector4(20, 16, 20, 16) },
            { "BgDropdownContent", new Vector4(16, 14, 16, 14) },
            { "BgSlidebar", new Vector4(6, 6, 6, 6) },
            { "YellowSlidebar", new Vector4(6, 6, 6, 6) },
            { "ListBtn_normal", new Vector4(16, 10, 16, 10) },
            { "ListBtn_hover", new Vector4(16, 10, 16, 10) },
            { "ListBtn_Disable", new Vector4(16, 10, 16, 10) },
            { "SmallitemBox", new Vector4(14, 14, 14, 14) },
            { "InventorySlot", new Vector4(14, 14, 14, 14) },
            { "Btn_Empty_Normal", new Vector4(16, 16, 16, 16) },
            { "Btn_Empty_Hover", new Vector4(16, 16, 16, 16) },
            { "Btn_Empty_Disable", new Vector4(16, 16, 16, 16) },
            { "Btn_Acess_Normal", new Vector4(16, 16, 16, 16) },
            { "Btn_Acess_Hover", new Vector4(16, 16, 16, 16) },
            { "Btn_Acess_Disable", new Vector4(16, 16, 16, 16) },
            { "SmallBtnforONOFF", new Vector4(20, 20, 20, 20) },
            { "Shop_Btn", new Vector4(16, 16, 16, 16) },
            { "Btn_RemoveAds_Normal", new Vector4(16, 16, 16, 16) },
            { "Btn_RemoveAds_Hover", new Vector4(16, 16, 16, 16) },
            { "Btn_RemoveAds_Disable", new Vector4(16, 16, 16, 16) },
        };

        // Ship/effect art is authored much larger (800-1200px) than the UI icons, so it gets its
        // own pixels-per-unit so a ship ends up a sensible ~1.3-1.6 world units tall in Gameplay.
        private const float ShipPixelsPerUnit = 650f;

        // Background layers are full-screen scrolling art (1080x1920, portrait) - much lower PPU
        // so one layer comfortably covers the landscape camera view after being rotated 90 degrees.
        private const float BackgroundPixelsPerUnit = 85f;

        [MenuItem("Tools/Space Hawk/1. Apply Sprite Import Settings")]
        public static void ApplyAll()
        {
            ApplyFolder("Assets/Material/UI", 100f);
            ApplyFolder("Assets/Material/Ship_01", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Ship_02", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Ship_03", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Ship_Effects", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Boss", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Meteors", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Props", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Sprites", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Bonus_Items", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Background", BackgroundPixelsPerUnit);
            // User-added pack (ships/weapons/explosions/asteroids/GUI) - same effect-art PPU
            // convention as the existing Ship_Effects/Boss/Meteors folders above.
            ApplyFolder("Assets/Material/Weapons", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Explosions", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Asteroids", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/Spaceships", ShipPixelsPerUnit);
            ApplyFolder("Assets/Material/GUI", 100f);
            AssetDatabase.SaveAssets();
            Debug.Log("[UISpriteImportSettings] Sprite import settings applied.");
        }

        private static void ApplyFolder(string folder, float pixelsPerUnit)
        {
            if (!Directory.Exists(folder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
                {
                    importer.spritePixelsPerUnit = pixelsPerUnit;
                    changed = true;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (SlicedBorders.TryGetValue(fileName, out Vector4 border) && importer.spriteBorder != border)
                {
                    importer.spriteBorder = border;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
