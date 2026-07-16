using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AE86FinalWorkspaceImportSetup
{
    private const string FinalRoot = "Assets/Art/Vehicles/AE86_final";
    private const string CurrentRoot = FinalRoot + "/Current17";
    private const string ManualRoot = FinalRoot + "/ManualSlot07";
    private const string ReportsRoot = FinalRoot + "/Reports";
    private const string ActiveRoot = "Assets/Art/Vehicles/AE86/Body/Extracted/Production32";

    private static readonly string[] CurrentNames =
    {
        "00_090.00_up.png", "01_078.75.png", "02_067.50.png", "03_056.25.png",
        "04_045.00_upright.png", "05_033.75.png", "06_022.50.png",
        "07_011.25_WORKING.png", "08_000.00_right.png", "09_348.75.png",
        "10_337.50.png", "11_326.25.png", "12_315.00_downright.png",
        "13_303.75.png", "14_292.50.png", "15_281.25.png", "16_270.00_down.png"
    };

    private static readonly string[] ActiveNames =
    {
        "ae86_090_00_up.png", "ae86_078_75.png", "ae86_067_50.png", "ae86_056_25.png",
        "ae86_045_00_upright.png", "ae86_033_75.png", "ae86_022_50.png",
        "ae86_011_25.png", "ae86_000_00_right.png", "ae86_348_75.png",
        "ae86_337_50.png", "ae86_326_25.png", "ae86_315_00_downright.png",
        "ae86_303_75.png", "ae86_292_50.png", "ae86_281_25.png", "ae86_270_00_down.png"
    };

    private static readonly string[] ManualNames =
    {
        "01_prev_slot06_22.50.png", "02_working_slot07_11.25.png", "03_next_slot08_right.png"
    };

    private static readonly int[] ManualActiveSlots = { 6, 7, 8 };
    private static readonly string[] PlatformNames =
    {
        "DefaultTexturePlatform", "Standalone", "Android", "iPhone"
    };

    private sealed class Row
    {
        public string Kind;
        public string AssetPath;
        public string ActiveReference;
        public string Guid;
        public int Width;
        public int Height;
        public int PngColorType;
        public bool TextureExists;
        public bool SpriteExists;
        public bool SettingsMatch;
        public bool GuidUnique;
        public bool GuidNotActive;
        public string Settings;
        public string Status;
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            ApplyAndValidate();
            Debug.Log("AE86_FINAL_IMPORT_READY");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ApplyAndValidate()
    {
        if (CurrentNames.Length != 17 || ActiveNames.Length != 17)
            throw new InvalidOperationException("Current17 importer mapping must contain exactly 17 entries.");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        List<Tuple<string, string, string>> spriteMappings = new List<Tuple<string, string, string>>();
        for (int slot = 0; slot < 17; slot++)
        {
            spriteMappings.Add(Tuple.Create(
                "CURRENT17",
                CurrentRoot + "/" + CurrentNames[slot],
                ActiveRoot + "/" + ActiveNames[slot]));
        }

        for (int index = 0; index < ManualNames.Length; index++)
        {
            int activeSlot = ManualActiveSlots[index];
            spriteMappings.Add(Tuple.Create(
                "MANUAL_SLOT07",
                ManualRoot + "/" + ManualNames[index],
                ActiveRoot + "/" + ActiveNames[activeSlot]));
        }

        foreach (Tuple<string, string, string> mapping in spriteMappings)
        {
            TextureImporter active = AssetImporter.GetAtPath(mapping.Item3) as TextureImporter;
            TextureImporter workspace = AssetImporter.GetAtPath(mapping.Item2) as TextureImporter;
            if (active == null || workspace == null)
                throw new InvalidOperationException("Missing TextureImporter: " + mapping.Item2 + " or " + mapping.Item3);

            CopyImporter(active, workspace);
            workspace.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        HashSet<string> activePngGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string activeName in ActiveNames)
            activePngGuids.Add(AssetDatabase.AssetPathToGUID(ActiveRoot + "/" + activeName));

        HashSet<string> workspaceGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<Row> rows = new List<Row>();
        foreach (Tuple<string, string, string> mapping in spriteMappings)
        {
            TextureImporter active = AssetImporter.GetAtPath(mapping.Item3) as TextureImporter;
            TextureImporter workspace = AssetImporter.GetAtPath(mapping.Item2) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(mapping.Item2);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(mapping.Item2);
            string guid = AssetDatabase.AssetPathToGUID(mapping.Item2);
            ReadPngHeader(mapping.Item2, out int width, out int height, out int colorType);

            Row row = new Row
            {
                Kind = mapping.Item1,
                AssetPath = mapping.Item2,
                ActiveReference = mapping.Item3,
                Guid = guid,
                Width = width,
                Height = height,
                PngColorType = colorType,
                TextureExists = texture != null && texture.width == 186 && texture.height == 186,
                SpriteExists = sprite != null,
                SettingsMatch = ImporterSettingsMatch(active, workspace),
                GuidUnique = !string.IsNullOrEmpty(guid) && workspaceGuids.Add(guid),
                GuidNotActive = !activePngGuids.Contains(guid),
                Settings = DescribeImporter(workspace)
            };

            row.Status = row.Width == 186 && row.Height == 186 && row.PngColorType == 6 &&
                         row.TextureExists && row.SpriteExists && row.SettingsMatch &&
                         row.GuidUnique && row.GuidNotActive
                ? "PASS"
                : "FAIL";
            rows.Add(row);
        }

        string[] allWorkspaceGuids = AssetDatabase.FindAssets(string.Empty, new[] { FinalRoot });
        bool allAssetGuidsUnique = allWorkspaceGuids.Length == allWorkspaceGuids.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        bool noWorkspaceGuidMatchesActive = allWorkspaceGuids.All(guid => !activePngGuids.Contains(guid));

        WriteAudit(rows, allWorkspaceGuids.Length, allAssetGuidsUnique, noWorkspaceGuidMatchesActive);

        if (rows.Count != 20 || rows.Any(row => row.Status != "PASS") ||
            !allAssetGuidsUnique || !noWorkspaceGuidMatchesActive || EditorUtility.scriptCompilationFailed)
        {
            throw new InvalidOperationException("AE86_final Unity import validation failed. Read unity_import_audit.csv.");
        }
    }

    private static void CopyImporter(TextureImporter source, TextureImporter destination)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        source.ReadTextureSettings(settings);
        destination.SetTextureSettings(settings);
        destination.textureType = source.textureType;
        destination.spriteImportMode = source.spriteImportMode;
        destination.spritePixelsPerUnit = source.spritePixelsPerUnit;
        destination.spritePivot = source.spritePivot;
        destination.filterMode = source.filterMode;
        destination.textureCompression = source.textureCompression;
        destination.compressionQuality = source.compressionQuality;
        destination.crunchedCompression = source.crunchedCompression;
        destination.maxTextureSize = source.maxTextureSize;
        destination.mipmapEnabled = source.mipmapEnabled;
        destination.alphaIsTransparency = source.alphaIsTransparency;
        destination.alphaSource = source.alphaSource;
        destination.wrapMode = source.wrapMode;
        destination.sRGBTexture = source.sRGBTexture;
        destination.npotScale = source.npotScale;
        destination.isReadable = source.isReadable;

        foreach (string platform in PlatformNames)
            destination.SetPlatformTextureSettings(source.GetPlatformTextureSettings(platform));
    }

    private static bool ImporterSettingsMatch(TextureImporter active, TextureImporter workspace)
    {
        if (active == null || workspace == null)
            return false;

        TextureImporterSettings activeSettings = new TextureImporterSettings();
        TextureImporterSettings workspaceSettings = new TextureImporterSettings();
        active.ReadTextureSettings(activeSettings);
        workspace.ReadTextureSettings(workspaceSettings);

        return active.textureType == workspace.textureType &&
               active.spriteImportMode == workspace.spriteImportMode &&
               Mathf.Approximately(active.spritePixelsPerUnit, workspace.spritePixelsPerUnit) &&
               activeSettings.spriteMeshType == workspaceSettings.spriteMeshType &&
               Vector2.Distance(active.spritePivot, workspace.spritePivot) < 0.0001f &&
               active.filterMode == workspace.filterMode &&
               active.textureCompression == workspace.textureCompression &&
               active.compressionQuality == workspace.compressionQuality &&
               active.crunchedCompression == workspace.crunchedCompression &&
               active.maxTextureSize == workspace.maxTextureSize &&
               active.mipmapEnabled == workspace.mipmapEnabled &&
               active.alphaIsTransparency == workspace.alphaIsTransparency &&
               active.alphaSource == workspace.alphaSource &&
               active.wrapMode == workspace.wrapMode &&
               active.sRGBTexture == workspace.sRGBTexture &&
               active.npotScale == workspace.npotScale &&
               active.isReadable == workspace.isReadable &&
               PlatformNames.All(platform => PlatformSignature(active, platform) == PlatformSignature(workspace, platform));
    }

    private static string PlatformSignature(TextureImporter importer, string platform)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        return string.Join("|", settings.name, settings.overridden, settings.maxTextureSize,
            settings.resizeAlgorithm, settings.format, settings.textureCompression,
            settings.compressionQuality, settings.crunchedCompression, settings.allowsAlphaSplitting);
    }

    private static string DescribeImporter(TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        return string.Join(";",
            "type=" + importer.textureType,
            "mode=" + importer.spriteImportMode,
            "ppu=" + importer.spritePixelsPerUnit.ToString("0.###", CultureInfo.InvariantCulture),
            "mesh=" + settings.spriteMeshType,
            $"pivot={importer.spritePivot.x:0.###}/{importer.spritePivot.y:0.###}",
            "filter=" + importer.filterMode,
            "compression=" + importer.textureCompression,
            "max=" + importer.maxTextureSize,
            "mips=" + importer.mipmapEnabled,
            "alpha=" + importer.alphaIsTransparency,
            "wrap=" + importer.wrapMode,
            "srgb=" + importer.sRGBTexture,
            "npot=" + importer.npotScale,
            "readable=" + importer.isReadable);
    }

    private static void ReadPngHeader(string assetPath, out int width, out int height, out int colorType)
    {
        byte[] bytes = File.ReadAllBytes(ToAbsolutePath(assetPath));
        if (bytes.Length < 26 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
            throw new InvalidDataException("Invalid PNG: " + assetPath);

        width = ReadBigEndianInt32(bytes, 16);
        height = ReadBigEndianInt32(bytes, 20);
        colorType = bytes[25];
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) |
               (bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    private static void WriteAudit(List<Row> rows, int allGuidCount, bool allGuidsUnique, bool noActiveGuidMatch)
    {
        List<string> lines = new List<string>
        {
            "kind,asset_path,active_import_reference,guid,width,height,png_color_type_rgba6,texture_186x186,sprite_non_null,settings_match,guid_unique,guid_not_active,settings,status"
        };

        foreach (Row row in rows)
        {
            lines.Add(Csv(row.Kind, row.AssetPath, row.ActiveReference, row.Guid,
                row.Width.ToString(CultureInfo.InvariantCulture), row.Height.ToString(CultureInfo.InvariantCulture),
                row.PngColorType == 6 ? "PASS" : "FAIL", row.TextureExists ? "PASS" : "FAIL",
                row.SpriteExists ? "PASS" : "FAIL", row.SettingsMatch ? "PASS" : "FAIL",
                row.GuidUnique ? "PASS" : "FAIL", row.GuidNotActive ? "PASS" : "FAIL",
                row.Settings, row.Status));
        }

        lines.Add(Csv("SUMMARY", FinalRoot, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty,
            $"ALL_WORKSPACE_GUIDS_UNIQUE={allGuidsUnique}",
            $"NO_GUID_MATCHES_ACTIVE={noActiveGuidMatch}",
            $"WORKSPACE_ASSET_GUID_COUNT={allGuidCount}",
            rows.All(row => row.Status == "PASS") && allGuidsUnique && noActiveGuidMatch ? "PASS" : "FAIL"));

        File.WriteAllLines(ToAbsolutePath(ReportsRoot + "/unity_import_audit.csv"), lines, new UTF8Encoding(false));
    }

    private static string Csv(params string[] values)
    {
        return string.Join(",", values.Select(value => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));
    }

    private static string ToAbsolutePath(string projectPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        return Path.GetFullPath(Path.Combine(projectRoot, projectPath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
