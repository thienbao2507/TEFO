#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TEFOTileAssetSetupTool
{
    public const int MAP_TILE_PPU = 32;
    public const int LEGACY_TILE_PPU = 16;

    private const string ReportPath = "Assets/Docs/MAP_IMPORT_REPORT.md";
    private const string TileAssetRoot = "Assets/Tiles";
    private const string PropPrefabRoot = "Assets/Prefabs/Props";
    private const string BuildingPrefabRoot = "Assets/Prefabs/Buildings";

    private static readonly string[] ScanRoots =
    {
        "Assets/Art/Map/Tiles",
        "Assets/Art/Map/Props",
        "Assets/Art/Map/Buildings",
        "Assets/Map/Tiles"
    };

    private static readonly string[] TileCategories =
    {
        "Grass", "Forest", "Dirt", "Road", "Road_Marking", "Sidewalk", "Curb", "Sand", "Water", "Decals"
    };

    private static readonly string[] PropCategories =
    {
        "Nature", "Street", "Beach", "Parking", "Farm"
    };

    private static readonly string[] BuildingCategories =
    {
        "Houses", "Shops", "Garages", "Landmarks"
    };

    [MenuItem("TEFO/Map/Assets/Create Tiles From Map PNGs")]
    public static void CreateTilesFromMapPngs()
    {
        SetupReport report = CreateReport("Create Tiles From Map PNGs");
        List<MapPngAsset> assets = ScanMapPngAssets(report);
        ConfigureImportSettings(assets.Where(asset => asset.Kind == MapAssetKind.Tile), report);
        CreateTileAssets(assets.Where(asset => asset.Kind == MapAssetKind.Tile), report);
        FinishSetup(report, false);
    }

    [MenuItem("TEFO/Map/Assets/Create Prop Prefabs From Map PNGs")]
    public static void CreatePropPrefabsFromMapPngs()
    {
        SetupReport report = CreateReport("Create Prop Prefabs From Map PNGs");
        List<MapPngAsset> assets = ScanMapPngAssets(report);
        ConfigureImportSettings(assets.Where(asset => asset.Kind == MapAssetKind.Prop), report);
        CreatePropPrefabs(assets.Where(asset => asset.Kind == MapAssetKind.Prop), report);
        FinishSetup(report, false);
    }

    [MenuItem("TEFO/Map/Assets/Create Building Prefabs From Map PNGs")]
    public static void CreateBuildingPrefabsFromMapPngs()
    {
        SetupReport report = CreateReport("Create Building Prefabs From Map PNGs");
        List<MapPngAsset> assets = ScanMapPngAssets(report);
        ConfigureImportSettings(assets.Where(asset => asset.Kind == MapAssetKind.Building), report);
        CreateBuildingPrefabs(assets.Where(asset => asset.Kind == MapAssetKind.Building), report);
        FinishSetup(report, false);
    }

    [MenuItem("TEFO/Map/Assets/Setup All Map Assets")]
    public static void SetupAllMapAssets()
    {
        SetupReport report = CreateReport("Setup All Map Assets");
        EnsureKnownFolders();

        List<MapPngAsset> assets = ScanMapPngAssets(report);
        ConfigureImportSettings(assets, report);
        CreateTileAssets(assets.Where(asset => asset.Kind == MapAssetKind.Tile), report);
        CreatePropPrefabs(assets.Where(asset => asset.Kind == MapAssetKind.Prop), report);
        CreateBuildingPrefabs(assets.Where(asset => asset.Kind == MapAssetKind.Building), report);
        TEFOMapLayerSetupTool.SetupLayerStack(false);

        FinishSetup(report, true);
    }

    public static void ConfigureAllMapPngImportsOnly()
    {
        SetupReport report = CreateReport("Configure All Map PNG Imports");
        List<MapPngAsset> assets = ScanMapPngAssets(report);
        ConfigureImportSettings(assets, report);
        FinishSetup(report, false);
    }

    private static void EnsureKnownFolders()
    {
        foreach (string category in TileCategories)
        {
            EnsureFolder($"{TileAssetRoot}/{category}");
        }

        foreach (string category in PropCategories)
        {
            EnsureFolder($"{PropPrefabRoot}/{category}");
        }

        foreach (string category in BuildingCategories)
        {
            EnsureFolder($"{BuildingPrefabRoot}/{category}");
        }

        EnsureFolder("Assets/Docs");
        AssetDatabase.Refresh();
    }

    private static List<MapPngAsset> ScanMapPngAssets(SetupReport report)
    {
        AssetDatabase.Refresh();

        List<MapPngAsset> assets = new List<MapPngAsset>();

        foreach (string root in ScanRoots)
        {
            if (!Directory.Exists(root))
            {
                report.Warnings.Add($"Scan root missing: {root}");
                continue;
            }

            foreach (string rawPath in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
            {
                string path = NormalizePath(rawPath);
                MapAssetKind kind = GetAssetKind(path);
                if (kind == MapAssetKind.Unknown)
                {
                    report.Skipped.Add($"{path}: not in a supported map source folder.");
                    continue;
                }

                PngInfo info = ReadPngInfo(path, report);
                string category = GetCategory(path, kind);
                if (string.IsNullOrEmpty(category))
                {
                    report.Skipped.Add($"{path}: could not determine category.");
                    continue;
                }

                MapPngAsset asset = new MapPngAsset(path, kind, category, info.Width, info.Height);
                assets.Add(asset);
                report.FoundPngs.Add(asset);

                AddSizeWarnings(asset, report);
                if (info.LooksLikeCheckerboard)
                {
                    report.Warnings.Add($"{path}: possible checkerboard background baked into pixels.");
                }
            }
        }

        return assets.OrderBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ConfigureImportSettings(IEnumerable<MapPngAsset> assets, SetupReport report)
    {
        foreach (MapPngAsset asset in assets)
        {
            TextureImporter importer = AssetImporter.GetAtPath(asset.Path) as TextureImporter;
            if (importer == null)
            {
                report.Skipped.Add($"{asset.Path}: TextureImporter not found.");
                continue;
            }

            int ppu = GetPixelsPerUnit(asset, importer);
            bool legacyKept = ppu == LEGACY_TILE_PPU && asset.Path.StartsWith("Assets/Map/Tiles/", StringComparison.OrdinalIgnoreCase);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
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
            importer.SaveAndReimport();

            report.ImportSettingsUpdated++;
            if (legacyKept)
            {
                report.LegacyPpuKept.Add($"{asset.Path}: kept {LEGACY_TILE_PPU} PPU because it is an existing legacy top-level map tile.");
            }
        }
    }

    private static int GetPixelsPerUnit(MapPngAsset asset, TextureImporter importer)
    {
        bool isLegacyTopLevelTile = asset.Kind == MapAssetKind.Tile
            && asset.Path.StartsWith("Assets/Map/Tiles/", StringComparison.OrdinalIgnoreCase)
            && asset.Path.Substring("Assets/Map/Tiles/".Length).IndexOf('/') < 0
            && Mathf.Approximately(importer.spritePixelsPerUnit, LEGACY_TILE_PPU);

        return isLegacyTopLevelTile ? LEGACY_TILE_PPU : MAP_TILE_PPU;
    }

    private static void CreateTileAssets(IEnumerable<MapPngAsset> tileAssets, SetupReport report)
    {
        Dictionary<string, string> existingTileBySpritePath = FindExistingTileAssetsBySpritePath();

        foreach (MapPngAsset asset in tileAssets)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.Path);
            if (sprite == null)
            {
                report.Skipped.Add($"{asset.Path}: sprite not available after import.");
                continue;
            }

            string targetFolder = $"{TileAssetRoot}/{asset.Category}";
            EnsureFolder(targetFolder);

            string targetPath = existingTileBySpritePath.TryGetValue(asset.Path, out string existingPath)
                ? existingPath
                : $"{targetFolder}/{SanitizeFileName(Path.GetFileNameWithoutExtension(asset.Path))}.asset";

            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(targetPath);
            if (tile == null && File.Exists(targetPath))
            {
                report.Skipped.Add($"{asset.Path}: target exists but is not a Tile asset: {targetPath}");
                continue;
            }

            if (tile != null)
            {
                string currentSpritePath = tile.sprite == null ? string.Empty : NormalizePath(AssetDatabase.GetAssetPath(tile.sprite));
                if (!string.IsNullOrEmpty(currentSpritePath) && !PathsEqual(currentSpritePath, asset.Path))
                {
                    report.Skipped.Add($"{asset.Path}: existing Tile uses another sprite, skipped: {targetPath}");
                    continue;
                }

                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
                report.TileAssetsUpdated++;
                continue;
            }

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = Path.GetFileNameWithoutExtension(asset.Path);
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, targetPath);
            report.TileAssetsCreated++;
        }
    }

    private static void CreatePropPrefabs(IEnumerable<MapPngAsset> propAssets, SetupReport report)
    {
        foreach (MapPngAsset asset in propAssets)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.Path);
            if (sprite == null)
            {
                report.Skipped.Add($"{asset.Path}: sprite not available after import.");
                continue;
            }

            string targetFolder = $"{PropPrefabRoot}/{asset.Category}";
            EnsureFolder(targetFolder);
            string targetPath = $"{targetFolder}/{SanitizeFileName(Path.GetFileNameWithoutExtension(asset.Path))}.prefab";

            if (!CreateOrUpdateSpritePrefab(asset, sprite, targetPath, GetPropSortingOrder(asset), ShouldAddPropCollider(asset), report))
            {
                continue;
            }

            if (report.LastPrefabWasCreated)
            {
                report.PropPrefabsCreated++;
            }
            else
            {
                report.PropPrefabsUpdated++;
            }
        }
    }

    private static void CreateBuildingPrefabs(IEnumerable<MapPngAsset> buildingAssets, SetupReport report)
    {
        foreach (MapPngAsset asset in buildingAssets)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.Path);
            if (sprite == null)
            {
                report.Skipped.Add($"{asset.Path}: sprite not available after import.");
                continue;
            }

            string targetFolder = $"{BuildingPrefabRoot}/{asset.Category}";
            EnsureFolder(targetFolder);
            string targetPath = $"{targetFolder}/{SanitizeFileName(Path.GetFileNameWithoutExtension(asset.Path))}.prefab";

            if (!CreateOrUpdateSpritePrefab(asset, sprite, targetPath, 10, true, report))
            {
                continue;
            }

            if (report.LastPrefabWasCreated)
            {
                report.BuildingPrefabsCreated++;
            }
            else
            {
                report.BuildingPrefabsUpdated++;
            }
        }
    }

    private static bool CreateOrUpdateSpritePrefab(MapPngAsset asset, Sprite sprite, string targetPath, int sortingOrder, bool addCollider, SetupReport report)
    {
        report.LastPrefabWasCreated = !File.Exists(targetPath);

        GameObject root;
        bool loadedPrefab = !report.LastPrefabWasCreated;
        if (loadedPrefab)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (prefabAsset == null)
            {
                report.Skipped.Add($"{asset.Path}: target exists but is not a prefab: {targetPath}");
                return false;
            }

            SpriteRenderer existingRenderer = prefabAsset.GetComponent<SpriteRenderer>();
            string existingSpritePath = existingRenderer == null || existingRenderer.sprite == null
                ? string.Empty
                : NormalizePath(AssetDatabase.GetAssetPath(existingRenderer.sprite));
            if (!string.IsNullOrEmpty(existingSpritePath) && !PathsEqual(existingSpritePath, asset.Path))
            {
                report.Skipped.Add($"{asset.Path}: existing prefab uses another sprite, skipped: {targetPath}");
                return false;
            }

            root = PrefabUtility.LoadPrefabContents(targetPath);
        }
        else
        {
            root = new GameObject(Path.GetFileNameWithoutExtension(asset.Path));
        }

        try
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;

            if (addCollider && root.GetComponent<BoxCollider2D>() == null)
            {
                root.AddComponent<BoxCollider2D>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            return true;
        }
        finally
        {
            if (loadedPrefab)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static Dictionary<string, string> FindExistingTileAssetsBySpritePath()
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(TileAssetRoot))
        {
            return result;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { TileAssetRoot }))
        {
            string tilePath = AssetDatabase.GUIDToAssetPath(guid);
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null || tile.sprite == null)
            {
                continue;
            }

            string spritePath = NormalizePath(AssetDatabase.GetAssetPath(tile.sprite));
            if (!string.IsNullOrEmpty(spritePath) && !result.ContainsKey(spritePath))
            {
                result.Add(spritePath, tilePath);
            }
        }

        return result;
    }

    private static PngInfo ReadPngInfo(string assetPath, SetupReport report)
    {
        string fullPath = Path.GetFullPath(assetPath);
        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                report.Warnings.Add($"{assetPath}: could not decode PNG size.");
                return new PngInfo(0, 0, false);
            }

            return new PngInfo(texture.width, texture.height, LooksLikeCheckerboard(texture));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static bool LooksLikeCheckerboard(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int sampled = 0;
        int grayOpaque = 0;
        int transparent = 0;

        int stepX = Mathf.Max(1, texture.width / 16);
        int stepY = Mathf.Max(1, texture.height / 16);

        for (int y = 0; y < texture.height; y += stepY)
        {
            for (int x = 0; x < texture.width; x += stepX)
            {
                Color32 color = pixels[y * texture.width + x];
                sampled++;

                if (color.a < 250)
                {
                    transparent++;
                    continue;
                }

                int rg = Mathf.Abs(color.r - color.g);
                int gb = Mathf.Abs(color.g - color.b);
                bool isGray = rg <= 8 && gb <= 8;
                bool isCheckerValue = color.r >= 180 && color.r <= 255;
                if (isGray && isCheckerValue)
                {
                    grayOpaque++;
                }
            }
        }

        return transparent == 0 && sampled > 0 && grayOpaque >= sampled * 0.45f;
    }

    private static void AddSizeWarnings(MapPngAsset asset, SetupReport report)
    {
        if (asset.Kind != MapAssetKind.Tile)
        {
            return;
        }

        if (asset.Width == MAP_TILE_PPU && asset.Height == MAP_TILE_PPU)
        {
            return;
        }

        if (asset.Width >= 64 || asset.Height >= 64)
        {
            report.Warnings.Add($"{asset.Path}: tile PNG is {asset.Width}x{asset.Height}; it may be a spritesheet or large tile.");
            return;
        }

        report.Warnings.Add($"{asset.Path}: tile PNG is {asset.Width}x{asset.Height}; expected 32x32 for new map tiles.");
    }

    private static MapAssetKind GetAssetKind(string path)
    {
        if (path.StartsWith("Assets/Art/Map/Tiles/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Assets/Map/Tiles/", StringComparison.OrdinalIgnoreCase))
        {
            return MapAssetKind.Tile;
        }

        if (path.StartsWith("Assets/Art/Map/Props/", StringComparison.OrdinalIgnoreCase))
        {
            return MapAssetKind.Prop;
        }

        if (path.StartsWith("Assets/Art/Map/Buildings/", StringComparison.OrdinalIgnoreCase))
        {
            return MapAssetKind.Building;
        }

        return MapAssetKind.Unknown;
    }

    private static string GetCategory(string path, MapAssetKind kind)
    {
        string folderCategory = GetCategoryFromPath(path, kind);
        string nameCategory = GetCategoryFromName(Path.GetFileNameWithoutExtension(path), kind);
        return string.IsNullOrEmpty(folderCategory) ? nameCategory : folderCategory;
    }

    private static string GetCategoryFromPath(string path, MapAssetKind kind)
    {
        string marker = kind == MapAssetKind.Tile
            ? (path.StartsWith("Assets/Map/Tiles/", StringComparison.OrdinalIgnoreCase) ? "Assets/Map/Tiles/" : "Assets/Art/Map/Tiles/")
            : kind == MapAssetKind.Prop ? "Assets/Art/Map/Props/" : "Assets/Art/Map/Buildings/";

        if (!path.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string remainder = path.Substring(marker.Length);
        int slash = remainder.IndexOf('/');
        if (slash < 0)
        {
            return string.Empty;
        }

        return NormalizeCategory(remainder.Substring(0, slash), kind, Path.GetFileNameWithoutExtension(path));
    }

    private static string GetCategoryFromName(string name, MapAssetKind kind)
    {
        return NormalizeCategory(name, kind, name);
    }

    private static string NormalizeCategory(string value, MapAssetKind kind, string fileName)
    {
        string text = $"{value}_{fileName}".ToLowerInvariant();

        if (kind == MapAssetKind.Tile)
        {
            if (text.Contains("road_marking") || text.Contains("roadmarking") || text.Contains("crosswalk") || text.Contains("line_") || text.Contains("road_line"))
            {
                return "Road_Marking";
            }

            if (text.Contains("forest"))
            {
                return "Forest";
            }

            if (text.Contains("grass"))
            {
                return "Grass";
            }

            if (text.Contains("dirt") || text.Contains("dirtpath") || text.Contains("path"))
            {
                return "Dirt";
            }

            if (text.Contains("road"))
            {
                return "Road";
            }

            if (text.Contains("villageplaza") || text.Contains("village_plaza") || text.Contains("plaza") || text.Contains("sidewalk"))
            {
                return text.Contains("decal") ? "Decals" : "Sidewalk";
            }

            if (text.Contains("curb"))
            {
                return "Curb";
            }

            if (text.Contains("sand") || text.Contains("beach"))
            {
                return "Sand";
            }

            if (text.Contains("water") || text.Contains("ocean") || text.Contains("shore"))
            {
                return "Water";
            }

            if (text.Contains("decal") || text.Contains("crack") || text.Contains("oil") || text.Contains("pothole") || text.Contains("moss") || text.Contains("leaf"))
            {
                return "Decals";
            }
        }

        if (kind == MapAssetKind.Prop)
        {
            if (text.Contains("farm"))
            {
                return "Farm";
            }

            if (text.Contains("beach") || text.Contains("palm") || text.Contains("dock") || text.Contains("umbrella"))
            {
                return "Beach";
            }

            if (text.Contains("parking"))
            {
                return "Parking";
            }

            if (text.Contains("street") || text.Contains("lamp") || text.Contains("sign") || text.Contains("bench") || text.Contains("fence") || text.Contains("trash") || text.Contains("barrel") || text.Contains("crate"))
            {
                return "Street";
            }

            return "Nature";
        }

        if (kind == MapAssetKind.Building)
        {
            if (text.Contains("shop") || text.Contains("store"))
            {
                return "Shops";
            }

            if (text.Contains("garage") || text.Contains("mechanic"))
            {
                return "Garages";
            }

            if (text.Contains("landmark") || text.Contains("lighthouse") || text.Contains("tower"))
            {
                return "Landmarks";
            }

            return "Houses";
        }

        return string.Empty;
    }

    private static int GetPropSortingOrder(MapPngAsset asset)
    {
        string text = $"{asset.Category}_{Path.GetFileNameWithoutExtension(asset.Path)}".ToLowerInvariant();
        if (text.Contains("front") || text.Contains("foreground") || text.Contains("tree") || text.Contains("lamp") || text.Contains("sign") || text.Contains("fence") || text.Contains("barrel") || text.Contains("crate") || text.Contains("lighthouse"))
        {
            return 30;
        }

        return 0;
    }

    private static bool ShouldAddPropCollider(MapPngAsset asset)
    {
        string text = Path.GetFileNameWithoutExtension(asset.Path).ToLowerInvariant();
        return text.Contains("tree")
            || text.Contains("rock_large")
            || text.Contains("lamp")
            || text.Contains("fence")
            || text.Contains("barrel")
            || text.Contains("crate")
            || text.Contains("lighthouse")
            || text.Contains("building");
    }

    private static void FinishSetup(SetupReport report, bool includedLayerSetup)
    {
        report.IncludedLayerSetup = includedLayerSetup;
        AddMissingCategoryWarnings(report);
        WriteReport(report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"TEFO map asset setup finished. Report: {ReportPath}");
    }

    private static void AddMissingCategoryWarnings(SetupReport report)
    {
        AddMissingCategories(report, MapAssetKind.Tile, TileCategories);
        AddMissingCategories(report, MapAssetKind.Prop, PropCategories);
        AddMissingCategories(report, MapAssetKind.Building, BuildingCategories);
    }

    private static void AddMissingCategories(SetupReport report, MapAssetKind kind, IEnumerable<string> expectedCategories)
    {
        HashSet<string> found = new HashSet<string>(
            report.FoundPngs.Where(asset => asset.Kind == kind).Select(asset => asset.Category),
            StringComparer.OrdinalIgnoreCase);

        foreach (string category in expectedCategories)
        {
            if (!found.Contains(category))
            {
                report.MissingCategories.Add($"{kind}: {category}");
            }
        }
    }

    private static void WriteReport(SetupReport report)
    {
        EnsureFolder("Assets/Docs");

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# TEFO Map Import Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Run mode: {report.RunMode}");
        builder.AppendLine();
        builder.AppendLine("## PPU Convention");
        builder.AppendLine();
        builder.AppendLine($"- MAP_TILE_PPU = {MAP_TILE_PPU} for generated 32x32 map PNGs, props, and buildings.");
        builder.AppendLine($"- LEGACY_TILE_PPU = {LEGACY_TILE_PPU} for existing top-level `Assets/Map/Tiles/*.png` assets already imported at 16 PPU.");
        builder.AppendLine("- Existing gameplay objects, player, vehicles, weapons, animation controllers, and car prefabs are not modified by this tool.");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- PNG files found: {report.FoundPngs.Count}");
        builder.AppendLine($"- Import settings updated: {report.ImportSettingsUpdated}");
        builder.AppendLine($"- Tile assets created: {report.TileAssetsCreated}");
        builder.AppendLine($"- Tile assets updated: {report.TileAssetsUpdated}");
        builder.AppendLine($"- Prop prefabs created: {report.PropPrefabsCreated}");
        builder.AppendLine($"- Prop prefabs updated: {report.PropPrefabsUpdated}");
        builder.AppendLine($"- Building prefabs created: {report.BuildingPrefabsCreated}");
        builder.AppendLine($"- Building prefabs updated: {report.BuildingPrefabsUpdated}");
        builder.AppendLine($"- Map layer setup included: {(report.IncludedLayerSetup ? "yes" : "no")}");
        builder.AppendLine();
        AppendAssetList(builder, "PNGs Found", report.FoundPngs.Select(asset => $"{asset.Path} - {asset.Kind}/{asset.Category} - {asset.Width}x{asset.Height}"));
        AppendAssetList(builder, "Legacy PPU Kept", report.LegacyPpuKept);
        AppendAssetList(builder, "Missing Expected Categories", report.MissingCategories);
        AppendAssetList(builder, "Warnings", report.Warnings);
        AppendAssetList(builder, "Skipped Files", report.Skipped);

        File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void AppendAssetList(StringBuilder builder, string title, IEnumerable<string> lines)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();

        List<string> items = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct().OrderBy(line => line).ToList();
        if (items.Count == 0)
        {
            builder.AppendLine("- None");
            builder.AppendLine();
            return;
        }

        foreach (string item in items)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
    }

    private static SetupReport CreateReport(string runMode)
    {
        return new SetupReport { RunMode = runMode };
    }

    private static void EnsureFolder(string folder)
    {
        folder = NormalizePath(folder);
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = NormalizePath(Path.GetDirectoryName(folder));
        string name = Path.GetFileName(folder);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static string SanitizeFileName(string name)
    {
        StringBuilder builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.ToString();
    }

    private static bool PathsEqual(string a, string b)
    {
        return string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private enum MapAssetKind
    {
        Unknown,
        Tile,
        Prop,
        Building
    }

    private readonly struct MapPngAsset
    {
        public readonly string Path;
        public readonly MapAssetKind Kind;
        public readonly string Category;
        public readonly int Width;
        public readonly int Height;

        public MapPngAsset(string path, MapAssetKind kind, string category, int width, int height)
        {
            Path = path;
            Kind = kind;
            Category = category;
            Width = width;
            Height = height;
        }
    }

    private readonly struct PngInfo
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool LooksLikeCheckerboard;

        public PngInfo(int width, int height, bool looksLikeCheckerboard)
        {
            Width = width;
            Height = height;
            LooksLikeCheckerboard = looksLikeCheckerboard;
        }
    }

    private sealed class SetupReport
    {
        public string RunMode;
        public int ImportSettingsUpdated;
        public int TileAssetsCreated;
        public int TileAssetsUpdated;
        public int PropPrefabsCreated;
        public int PropPrefabsUpdated;
        public int BuildingPrefabsCreated;
        public int BuildingPrefabsUpdated;
        public bool IncludedLayerSetup;
        public bool LastPrefabWasCreated;

        public readonly List<MapPngAsset> FoundPngs = new List<MapPngAsset>();
        public readonly List<string> LegacyPpuKept = new List<string>();
        public readonly List<string> MissingCategories = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Skipped = new List<string>();
    }
}
#endif
