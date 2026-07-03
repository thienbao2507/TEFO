#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TEFOTileImportTool
{
    public const int MAP_TILE_PPU = TEFOTileAssetSetupTool.MAP_TILE_PPU;
    public const int LEGACY_TILE_PPU = TEFOTileAssetSetupTool.LEGACY_TILE_PPU;

    [MenuItem("TEFO/Map/Import/Configure Selected Pixel PNGs")]
    public static void ConfigureSelectedPixelPngs()
    {
        int configuredCount = ConfigureSelectedPngs(GetSelectedPngPaths());
        Debug.Log($"TEFO tile import settings applied to {configuredCount} selected PNG(s).");
    }

    [MenuItem("TEFO/Map/Import/Configure Selected Pixel PNGs", true)]
    public static bool ValidateConfigureSelectedPixelPngs()
    {
        return GetSelectedPngPaths().Any();
    }

    [MenuItem("TEFO/Map/Import/Configure All Map PNGs")]
    public static void ConfigureAllMapPngs()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Configure TEFO map PNGs",
            "Apply TEFO pixel import settings to all map PNG files and update the import report?",
            "Configure",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        TEFOTileAssetSetupTool.ConfigureAllMapPngImportsOnly();
    }

    private static IEnumerable<string> GetSelectedPngPaths()
    {
        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (Directory.Exists(path))
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsPngPath(assetPath))
                    {
                        yield return assetPath;
                    }
                }

                continue;
            }

            if (IsPngPath(path))
            {
                yield return path;
            }
        }
    }

    private static int ConfigureSelectedPngs(IEnumerable<string> paths)
    {
        int configuredCount = 0;

        foreach (string path in paths.Distinct().OrderBy(path => path))
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            ApplyPixelSettings(importer, MAP_TILE_PPU);
            importer.SaveAndReimport();
            configuredCount++;
        }

        AssetDatabase.Refresh();
        return configuredCount;
    }

    private static void ApplyPixelSettings(TextureImporter importer, int pixelsPerUnit)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }

    private static bool IsPngPath(string path)
    {
        return path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
