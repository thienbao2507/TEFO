#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TEFOMapAssetIngestTool
{
    private const int GeneratedMapPpu = 32;
    private const string ReportPath = "Assets/Docs/MAP_ASSET_INGEST_REPORT.md";
    private const string ImportReportPath = "Assets/Docs/MAP_IMPORT_REPORT.md";
    private const string SpritesheetReviewFolder = "Assets/Art/Map/Spritesheets/NeedsManualSlice";

    private static readonly string[] PreferredAssetPackRoots =
    {
        "Assets/TEFO_Map_AssetPack",
        "TEFO_Map_AssetPack"
    };

    private static readonly string[] FallbackSourceRoots =
    {
        "Assets/Map",
        "Assets/Map/Tiles",
        "Assets/Art/Map",
        "Assets/Generated",
        "Assets/Downloads"
    };

    private static readonly string[] IgnoredPathParts =
    {
        "/Library/",
        "/Temp/",
        "/Packages/",
        "/Editor Default Resources/"
    };

    private static readonly string[] TileCategories =
    {
        "Grass", "Forest", "Dirt", "VillagePlaza", "Sand", "Water",
        "Road", "Road_Marking", "Sidewalk", "Curb", "Decals", "Collision"
    };

    private static readonly string[] PropCategories =
    {
        "Nature", "Beach", "Town", "Farm", "Street", "Parking"
    };

    private static readonly string[] BuildingCategories =
    {
        "Village", "Town", "Shops", "Garages", "Landmarks"
    };

    [MenuItem("TEFO/Map/Assets/Ingest Generated PNG Asset Pack")]
    public static void IngestGeneratedPngAssetPack()
    {
        Run(new RunOptions(copyToCanonical: true, rebuildAssets: false, setupLayers: false, mode: "Ingest Generated PNG Asset Pack"));
    }

    [MenuItem("TEFO/Map/Assets/Rebuild Tiles And Prefabs")]
    public static void RebuildTilesAndPrefabs()
    {
        Run(new RunOptions(copyToCanonical: false, rebuildAssets: true, setupLayers: false, mode: "Rebuild Tiles And Prefabs"));
    }

    [MenuItem("TEFO/Map/Assets/Full Fix And Setup Map Assets")]
    public static void FullFixAndSetupMapAssets()
    {
        Run(new RunOptions(copyToCanonical: true, rebuildAssets: true, setupLayers: true, mode: "Full Fix And Setup Map Assets"));
    }

    private static void Run(RunOptions options)
    {
        IngestReport report = new IngestReport(options.Mode);
        EnsureCanonicalFolders();
        AssetDatabase.Refresh();

        if (options.CopyToCanonical)
        {
            List<SourceRoot> sourceRoots = ResolveSourceRoots(report);
            List<ManifestEntry> manifestEntries = LoadManifestEntries(sourceRoots, report);
            List<ScannedPng> scannedPngs = ScanSourcePngs(sourceRoots, manifestEntries, report);
            CopyClassifiedPngsToCanonical(scannedPngs, report);
            AssetDatabase.Refresh();
        }

        List<ScannedPng> canonicalPngs = ScanCanonicalPngs(report);
        ApplyImportSettings(canonicalPngs, report);

        if (options.RebuildAssets)
        {
            AssetDatabase.Refresh();
            CreateTileAssets(canonicalPngs.Where(png => png.Classification.Kind == MapAssetKind.Tile), report);
            CreatePropPrefabs(canonicalPngs.Where(png => png.Classification.Kind == MapAssetKind.Prop), report);
            CreateBuildingPrefabs(canonicalPngs.Where(png => png.Classification.Kind == MapAssetKind.Building), report);
        }

        if (options.SetupLayers)
        {
            TEFOMapLayerSetupTool.SetupLayerStack(false);
            report.LayerSetupEnsured = true;
        }

        AddMissingCategoryWarnings(report);
        WriteReports(report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"TEFO map asset ingest finished. Report: {ReportPath}");
    }

    private static void EnsureCanonicalFolders()
    {
        foreach (string category in TileCategories)
        {
            EnsureFolder($"Assets/Art/Map/Tiles/{category}");
            EnsureFolder($"Assets/Tiles/{category}");
        }

        foreach (string category in PropCategories)
        {
            EnsureFolder($"Assets/Art/Map/Props/{category}");
            EnsureFolder($"Assets/Prefabs/Props/{category}");
        }

        foreach (string category in BuildingCategories)
        {
            EnsureFolder($"Assets/Art/Map/Buildings/{category}");
            EnsureFolder($"Assets/Prefabs/Buildings/{category}");
        }

        EnsureFolder(SpritesheetReviewFolder);
        EnsureFolder("Assets/Docs");
    }

    private static List<SourceRoot> ResolveSourceRoots(IngestReport report)
    {
        foreach (string root in PreferredAssetPackRoots)
        {
            if (RootHasPngs(root))
            {
                SourceRoot sourceRoot = new SourceRoot(NormalizePath(root), isAssetPack: true, isFallback: false);
                report.SourceRootsScanned.Add($"{sourceRoot.Path} (primary)");
                return new List<SourceRoot> { sourceRoot };
            }
        }

        report.Warnings.Add("No PNG files found in Assets/TEFO_Map_AssetPack or TEFO_Map_AssetPack. Scanning fallback map folders.");

        List<SourceRoot> fallbackRoots = new List<SourceRoot>();
        foreach (string root in FallbackSourceRoots)
        {
            if (!RootHasPngs(root))
            {
                continue;
            }

            SourceRoot sourceRoot = new SourceRoot(NormalizePath(root), isAssetPack: false, isFallback: true);
            fallbackRoots.Add(sourceRoot);
            report.SourceRootsScanned.Add($"{sourceRoot.Path} (fallback)");
        }

        if (fallbackRoots.Count == 0)
        {
            report.Warnings.Add("No fallback PNG source folders were found.");
        }

        return fallbackRoots;
    }

    private static bool RootHasPngs(string root)
    {
        return Directory.Exists(root) && Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).Length > 0;
    }

    private static List<ScannedPng> ScanSourcePngs(List<SourceRoot> sourceRoots, List<ManifestEntry> manifestEntries, IngestReport report)
    {
        List<ScannedPng> result = new List<ScannedPng>();
        HashSet<string> visitedFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> scannedPaths = new List<string>();

        foreach (SourceRoot sourceRoot in sourceRoots)
        {
            if (!Directory.Exists(sourceRoot.Path))
            {
                continue;
            }

            foreach (string rawPath in Directory.GetFiles(sourceRoot.Path, "*.png", SearchOption.AllDirectories))
            {
                string path = NormalizePath(rawPath);
                string fullPath = NormalizePath(Path.GetFullPath(path));
                if (!visitedFullPaths.Add(fullPath) || ShouldIgnorePath(path))
                {
                    continue;
                }

                scannedPaths.Add(path);
                report.TotalPngScanned++;
                if (sourceRoot.IsAssetPack)
                {
                    report.TotalPngScannedFromAssetPack++;
                }

                ManifestEntry manifestEntry = FindManifestEntry(path, manifestEntries);
                Classification classification = manifestEntry.IsValid
                    ? manifestEntry.Classification
                    : ClassifyPath(path);

                PngInfo info = ReadPngInfo(path, report);
                if (classification.Kind == MapAssetKind.Unknown)
                {
                    string message = $"{path}: unknown category; skipped.";
                    report.UnknownCategoryFiles.Add(message);
                    report.SkippedFiles.Add(message);
                    continue;
                }

                if (LooksLikeSpritesheet(path, info, classification))
                {
                    classification = Classification.Spritesheet("NeedsManualSlice");
                    report.SuspiciousSpritesheets++;
                    string message = $"{path}: suspected spritesheet or multi-asset PNG; copied to manual slice review instead of creating one Tile/Prefab.";
                    report.SuspiciousFiles.Add(message);
                    report.Warnings.Add(message);
                }

                report.TotalPngClassified++;
                ValidatePng(path, info, classification, report);
                result.Add(new ScannedPng(path, classification, info));
            }
        }

        RecordDuplicateFileNames(scannedPaths, report);
        return result;
    }

    private static List<ScannedPng> ScanCanonicalPngs(IngestReport report)
    {
        List<ScannedPng> result = new List<ScannedPng>();
        string[] roots =
        {
            "Assets/Art/Map/Tiles",
            "Assets/Art/Map/Props",
            "Assets/Art/Map/Buildings"
        };

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string rawPath in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
            {
                string path = NormalizePath(rawPath);
                Classification classification = ClassifyCanonicalPath(path);
                if (classification.Kind == MapAssetKind.Unknown)
                {
                    continue;
                }

                PngInfo info = ReadPngInfo(path, report);
                ValidatePng(path, info, classification, report);
                result.Add(new ScannedPng(path, classification, info));
            }
        }

        return result;
    }

    private static void RecordDuplicateFileNames(IEnumerable<string> paths, IngestReport report)
    {
        foreach (IGrouping<string, string> group in paths.GroupBy(path => SanitizeFileName(Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase))
        {
            List<string> duplicates = group.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToList();
            if (duplicates.Count <= 1)
            {
                continue;
            }

            string message = $"{group.Key}: {string.Join(", ", duplicates)}";
            report.DuplicateFiles.Add(message);
            report.Warnings.Add($"Duplicate filename detected: {message}");
        }
    }

    private static void CopyClassifiedPngsToCanonical(IEnumerable<ScannedPng> scannedPngs, IngestReport report)
    {
        foreach (ScannedPng png in scannedPngs)
        {
            string destinationFolder = GetCanonicalFolder(png.Classification);
            if (string.IsNullOrEmpty(destinationFolder))
            {
                report.SkippedFiles.Add($"{png.AssetPath}: no canonical folder for classification {png.Classification.Kind}/{png.Classification.Category}.");
                continue;
            }

            EnsureFolder(destinationFolder);
            string destinationName = SanitizeFileName(Path.GetFileName(png.AssetPath));
            string destinationPath = NormalizePath($"{destinationFolder}/{destinationName}");

            if (PathsEqual(png.AssetPath, destinationPath))
            {
                report.CanonicalFiles.Add(destinationPath);
                report.SourceToDestinationFiles.Add($"{png.AssetPath} -> {destinationPath} (already canonical)");
                continue;
            }

            string finalDestinationPath = destinationPath;
            if (File.Exists(finalDestinationPath))
            {
                if (FilesHaveSameBytes(png.AssetPath, finalDestinationPath))
                {
                    report.CanonicalFiles.Add(finalDestinationPath);
                    report.SourceToDestinationFiles.Add($"{png.AssetPath} -> {finalDestinationPath} (identical existing file reused)");
                    continue;
                }

                finalDestinationPath = GenerateUniqueAssetFilePath(destinationPath);
                report.Warnings.Add($"{png.AssetPath}: destination filename already exists with different contents. Copied to unique path {finalDestinationPath}.");
            }

            string fullDestinationFolder = Path.GetDirectoryName(finalDestinationPath);
            if (!string.IsNullOrEmpty(fullDestinationFolder))
            {
                Directory.CreateDirectory(fullDestinationFolder);
            }

            try
            {
                File.Copy(png.AssetPath, finalDestinationPath, overwrite: false);
                AssetDatabase.ImportAsset(finalDestinationPath);
                report.TotalPngCopied++;
                report.CopiedFiles.Add($"{png.AssetPath} -> {finalDestinationPath}");
                report.SourceToDestinationFiles.Add($"{png.AssetPath} -> {finalDestinationPath}");
                report.CanonicalFiles.Add(finalDestinationPath);
            }
            catch (Exception exception)
            {
                report.SkippedFiles.Add($"{png.AssetPath}: copy failed for {finalDestinationPath}: {exception.Message}");
            }
        }
    }

    private static string GenerateUniqueAssetFilePath(string destinationPath)
    {
        destinationPath = NormalizePath(destinationPath);
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        string folder = NormalizePath(Path.GetDirectoryName(destinationPath));
        string name = Path.GetFileNameWithoutExtension(destinationPath);
        string extension = Path.GetExtension(destinationPath);
        for (int index = 1; index < 10000; index++)
        {
            string candidate = NormalizePath($"{folder}/{name}_{index:00}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find a unique destination path for {destinationPath}.");
    }

    private static bool FilesHaveSameBytes(string leftPath, string rightPath)
    {
        FileInfo left = new FileInfo(leftPath);
        FileInfo right = new FileInfo(rightPath);
        if (!left.Exists || !right.Exists || left.Length != right.Length)
        {
            return false;
        }

        byte[] leftBytes = File.ReadAllBytes(leftPath);
        byte[] rightBytes = File.ReadAllBytes(rightPath);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        for (int i = 0; i < leftBytes.Length; i++)
        {
            if (leftBytes[i] != rightBytes[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyImportSettings(IEnumerable<ScannedPng> canonicalPngs, IngestReport report)
    {
        foreach (ScannedPng png in canonicalPngs)
        {
            TextureImporter importer = AssetImporter.GetAtPath(png.AssetPath) as TextureImporter;
            if (importer == null)
            {
                report.SkippedFiles.Add($"{png.AssetPath}: TextureImporter not found.");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = GeneratedMapPpu;
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
        }
    }

    private static void CreateTileAssets(IEnumerable<ScannedPng> tilePngs, IngestReport report)
    {
        foreach (ScannedPng png in tilePngs)
        {
            if (png.Classification.Kind != MapAssetKind.Tile)
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png.AssetPath);
            if (sprite == null)
            {
                report.SkippedFiles.Add($"{png.AssetPath}: sprite not available after import.");
                continue;
            }

            string targetFolder = $"Assets/Tiles/{png.Classification.Category}";
            EnsureFolder(targetFolder);
            string targetPath = NormalizePath($"{targetFolder}/{Path.GetFileNameWithoutExtension(png.AssetPath)}.asset");
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(targetPath);

            if (tile == null && File.Exists(targetPath))
            {
                report.SkippedFiles.Add($"{png.AssetPath}: target exists but is not a Tile asset: {targetPath}");
                continue;
            }

            Tile.ColliderType desiredCollider = png.Classification.Category == "Collision" ? Tile.ColliderType.Grid : Tile.ColliderType.None;

            if (tile != null)
            {
                bool changed = false;
                string currentSpritePath = tile.sprite == null ? string.Empty : NormalizePath(AssetDatabase.GetAssetPath(tile.sprite));
                if (!PathsEqual(currentSpritePath, png.AssetPath))
                {
                    report.Warnings.Add($"{targetPath}: existing Tile sprite updated from {currentSpritePath} to {png.AssetPath}.");
                    tile.sprite = sprite;
                    changed = true;
                }

                if (tile.colliderType != desiredCollider)
                {
                    tile.colliderType = desiredCollider;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(tile);
                    report.TileAssetsUpdated++;
                }

                continue;
            }

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = Path.GetFileNameWithoutExtension(png.AssetPath);
            tile.sprite = sprite;
            tile.colliderType = desiredCollider;
            AssetDatabase.CreateAsset(tile, targetPath);
            report.TileAssetsCreated++;
        }
    }

    private static void CreatePropPrefabs(IEnumerable<ScannedPng> propPngs, IngestReport report)
    {
        foreach (ScannedPng png in propPngs)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png.AssetPath);
            if (sprite == null)
            {
                report.SkippedFiles.Add($"{png.AssetPath}: sprite not available after import.");
                continue;
            }

            string targetFolder = $"Assets/Prefabs/Props/{png.Classification.Category}";
            EnsureFolder(targetFolder);
            string targetPath = NormalizePath($"{targetFolder}/{Path.GetFileNameWithoutExtension(png.AssetPath)}.prefab");
            if (CreateOrUpdateSpritePrefab(png, sprite, targetPath, GetPropSortingOrder(png), ShouldPropBlock(png), false, report))
            {
                report.PropPrefabsCreated++;
            }
        }
    }

    private static void CreateBuildingPrefabs(IEnumerable<ScannedPng> buildingPngs, IngestReport report)
    {
        foreach (ScannedPng png in buildingPngs)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png.AssetPath);
            if (sprite == null)
            {
                report.SkippedFiles.Add($"{png.AssetPath}: sprite not available after import.");
                continue;
            }

            string targetFolder = $"Assets/Prefabs/Buildings/{png.Classification.Category}";
            EnsureFolder(targetFolder);
            string targetPath = NormalizePath($"{targetFolder}/{Path.GetFileNameWithoutExtension(png.AssetPath)}.prefab");
            if (CreateOrUpdateSpritePrefab(png, sprite, targetPath, 10, true, true, report))
            {
                report.BuildingPrefabsCreated++;
            }
        }
    }

    private static bool CreateOrUpdateSpritePrefab(ScannedPng png, Sprite sprite, string targetPath, int sortingOrder, bool addCollider, bool isBuilding, IngestReport report)
    {
        bool alreadyExists = File.Exists(targetPath);
        GameObject root;

        if (alreadyExists)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (existingPrefab == null)
            {
                report.SkippedFiles.Add($"{png.AssetPath}: target exists but is not a prefab: {targetPath}");
                return false;
            }

            SpriteRenderer existingRenderer = existingPrefab.GetComponent<SpriteRenderer>();
            string existingSpritePath = existingRenderer == null || existingRenderer.sprite == null
                ? string.Empty
                : NormalizePath(AssetDatabase.GetAssetPath(existingRenderer.sprite));
            if (!string.IsNullOrEmpty(existingSpritePath) && !PathsEqual(existingSpritePath, png.AssetPath))
            {
                report.Warnings.Add($"{targetPath}: existing prefab sprite updated from {existingSpritePath} to {png.AssetPath}.");
            }

            root = PrefabUtility.LoadPrefabContents(targetPath);
        }
        else
        {
            root = new GameObject(Path.GetFileNameWithoutExtension(png.AssetPath));
        }

        try
        {
            root.name = Path.GetFileNameWithoutExtension(png.AssetPath);
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

            BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
            if (addCollider && collider == null)
            {
                root.AddComponent<BoxCollider2D>();
            }
            else if (!addCollider && collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            if (alreadyExists)
            {
                if (isBuilding)
                {
                    report.BuildingPrefabsUpdated++;
                }
                else
                {
                    report.PropPrefabsUpdated++;
                }

                return false;
            }

            return true;
        }
        finally
        {
            if (alreadyExists)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static List<ManifestEntry> LoadManifestEntries(List<SourceRoot> sourceRoots, IngestReport report)
    {
        List<ManifestEntry> entries = new List<ManifestEntry>();
        HashSet<string> visitedManifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SourceRoot sourceRoot in sourceRoots)
        {
            if (!Directory.Exists(sourceRoot.Path))
            {
                continue;
            }

            foreach (string rawPath in Directory.GetFiles(sourceRoot.Path, "asset_manifest.*", SearchOption.AllDirectories))
            {
                string path = NormalizePath(rawPath);
                if (!visitedManifests.Add(NormalizePath(Path.GetFullPath(path))) || ShouldIgnorePath(path))
                {
                    continue;
                }

                if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    report.ManifestFiles.Add(path);
                    LoadCsvManifest(path, entries, report);
                }
                else if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    report.ManifestFiles.Add(path);
                    LoadJsonManifest(path, entries, report);
                }
            }
        }

        report.ManifestEntriesLoaded = entries.Count;
        return entries;
    }

    private static void LoadCsvManifest(string path, List<ManifestEntry> entries, IngestReport report)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0)
        {
            return;
        }

        List<string> headers = SplitCsvLine(lines[0]).Select(NormalizeHeader).ToList();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].IndexOf(".png", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            List<string> cells = SplitCsvLine(lines[i]);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column < headers.Count && column < cells.Count; column++)
            {
                values[headers[column]] = cells[column].Trim();
            }

            string filename = GetValue(values, "filename");
            string relativePath = GetValue(values, "relative_path");
            string kind = GetValue(values, "tile_or_prop");
            string category = GetValue(values, "category");
            string subcategory = GetValue(values, "subcategory");
            Classification classification = ClassifyManifestFields(kind, category, subcategory, lines[i]);
            AddManifestTokens(entries, classification, filename, relativePath);
        }
    }

    private static void LoadJsonManifest(string path, List<ManifestEntry> entries, IngestReport report)
    {
        string content = File.ReadAllText(path);
        foreach (Match objectMatch in Regex.Matches(content, "\\{(?<body>.*?)\\}", RegexOptions.Singleline))
        {
            string body = objectMatch.Groups["body"].Value;
            if (body.IndexOf(".png", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match fieldMatch in Regex.Matches(body, "\"(?<key>[^\"]+)\"\\s*:\\s*(\"(?<value>[^\"]*)\"|(?<number>-?[0-9.]+)|(?<bool>true|false))", RegexOptions.IgnoreCase))
            {
                string key = NormalizeHeader(fieldMatch.Groups["key"].Value);
                string value = fieldMatch.Groups["value"].Success
                    ? fieldMatch.Groups["value"].Value
                    : fieldMatch.Groups["number"].Success
                        ? fieldMatch.Groups["number"].Value
                        : fieldMatch.Groups["bool"].Value;
                values[key] = value;
            }

            string filename = GetValue(values, "filename");
            string relativePath = GetValue(values, "relative_path");
            string kind = GetValue(values, "tile_or_prop");
            string category = GetValue(values, "category");
            string subcategory = GetValue(values, "subcategory");
            Classification classification = ClassifyManifestFields(kind, category, subcategory, body);
            AddManifestTokens(entries, classification, filename, relativePath);
        }
    }

    private static void AddManifestTokens(List<ManifestEntry> entries, Classification classification, params string[] tokens)
    {
        if (classification.Kind == MapAssetKind.Unknown)
        {
            return;
        }

        foreach (string rawToken in tokens)
        {
            if (string.IsNullOrWhiteSpace(rawToken) || rawToken.IndexOf(".png", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            entries.Add(new ManifestEntry(NormalizePath(rawToken.Trim()), classification));
        }
    }

    private static List<string> SplitCsvLine(string line)
    {
        List<string> cells = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static string NormalizeHeader(string header)
    {
        return Normalize(header).Trim('_');
    }

    private static string GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string value) ? value : string.Empty;
    }

    private static ManifestEntry FindManifestEntry(string assetPath, List<ManifestEntry> manifestEntries)
    {
        string normalizedPath = Normalize(assetPath);
        string fileName = Normalize(Path.GetFileName(assetPath));

        foreach (ManifestEntry entry in manifestEntries)
        {
            string normalizedToken = Normalize(entry.Token);
            if (normalizedPath.EndsWith(normalizedToken, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        foreach (ManifestEntry entry in manifestEntries)
        {
            if (fileName == Normalize(Path.GetFileName(entry.Token)))
            {
                return entry;
            }
        }

        return ManifestEntry.Invalid;
    }

    private static Classification ClassifyCanonicalPath(string path)
    {
        string normalized = NormalizePath(path);
        if (normalized.StartsWith("Assets/Art/Map/Tiles/", StringComparison.OrdinalIgnoreCase))
        {
            return new Classification(MapAssetKind.Tile, MapTileCategory(GetCategoryAfter(normalized, "Assets/Art/Map/Tiles/"), normalized));
        }

        if (normalized.StartsWith("Assets/Art/Map/Props/", StringComparison.OrdinalIgnoreCase))
        {
            return new Classification(MapAssetKind.Prop, MapPropCategory(GetCategoryAfter(normalized, "Assets/Art/Map/Props/"), normalized));
        }

        if (normalized.StartsWith("Assets/Art/Map/Buildings/", StringComparison.OrdinalIgnoreCase))
        {
            return new Classification(MapAssetKind.Building, MapBuildingCategory(GetCategoryAfter(normalized, "Assets/Art/Map/Buildings/"), normalized));
        }

        return Classification.Unknown;
    }

    private static Classification ClassifyPath(string path)
    {
        Classification canonical = ClassifyCanonicalPath(path);
        if (canonical.Kind != MapAssetKind.Unknown)
        {
            return canonical;
        }

        Classification folderClassification = ClassifyKnownFolderPath(path);
        if (folderClassification.Kind != MapAssetKind.Unknown)
        {
            return folderClassification;
        }

        return ClassifyText(path);
    }

    private static Classification ClassifyKnownFolderPath(string path)
    {
        string[] parts = NormalizePath(path).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("Tiles", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length - 1)
            {
                return new Classification(MapAssetKind.Tile, MapTileCategory(parts[i + 1], path));
            }

            if (parts[i].Equals("Props", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length - 1)
            {
                return new Classification(MapAssetKind.Prop, MapPropCategory(parts[i + 1], path));
            }

            if (parts[i].Equals("Buildings", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length - 1)
            {
                return new Classification(MapAssetKind.Building, MapBuildingCategory(parts[i + 1], path));
            }
        }

        return Classification.Unknown;
    }

    private static Classification ClassifyManifestFields(string kind, string category, string subcategory, string context)
    {
        string combined = $"{kind} {category} {subcategory} {context}";
        string kindText = Normalize($"{kind} {category}");
        string categoryText = string.IsNullOrWhiteSpace(subcategory) ? category : subcategory;

        if (ContainsAny(kindText, "building", "buildings"))
        {
            return new Classification(MapAssetKind.Building, MapBuildingCategory(categoryText, combined));
        }

        if (ContainsAny(kindText, "prop", "props"))
        {
            return new Classification(MapAssetKind.Prop, MapPropCategory(categoryText, combined));
        }

        if (ContainsAny(kindText, "tile", "tiles", "terrain"))
        {
            return new Classification(MapAssetKind.Tile, MapTileCategory(categoryText, combined));
        }

        return ClassifyText(combined);
    }

    private static Classification ClassifyText(string source)
    {
        string text = Normalize(source);
        bool pathSaysTile = ContainsAny(text, "/tile", "/tiles", "tiles/", "terrain");
        bool pathSaysProp = ContainsAny(text, "/prop", "/props", "props/");
        bool pathSaysBuilding = ContainsAny(text, "/building", "/buildings", "buildings/");

        if (pathSaysBuilding)
        {
            return ClassifyBuildingText(text);
        }

        if (pathSaysProp)
        {
            return ClassifyPropText(text);
        }

        if (pathSaysTile)
        {
            return ClassifyTileText(text);
        }

        if (ContainsAny(text, "village_house", "house_small", "barn", "rural_house", "town_house", "city_house", "shop", "store", "market", "garage", "auto", "parking_building", "lighthouse", "monument", "fountain_area", "landmark", "plaza_monument", "bridge"))
        {
            return ClassifyBuildingText(text);
        }

        if (ContainsAny(text, "tree", "pine", "oak", "palm", "bush", "grass_tuft", "flower_patch", "rock", "stump", "log", "umbrella", "beach_chair", "towel", "dock", "hut", "beach_sign", "lamp", "sign", "bench", "mailbox", "trash", "cone", "barrel", "crate", "tire", "fence", "post", "crop", "field", "haystack", "well", "rural", "farm", "gate", "parking_meter", "parking_prop", "town_prop", "urban_prop"))
        {
            return ClassifyPropText(text);
        }

        if (ContainsAny(text, "grass", "flower", "meadow", "lawn", "forest", "dense", "jungle", "woods", "dirt", "path", "trail", "soil", "village_path", "plaza", "stone_plaza", "village_plaza", "road", "roadmarking", "asphalt", "street", "pavement_road", "line", "dash", "crosswalk", "parking_line", "stop_line", "marking", "sidewalk", "pavement", "stone_path", "walkway", "curb", "kerb", "road_edge", "sand", "beach", "water", "ocean", "sea", "shore", "foam", "pond", "lake", "decal", "crack", "pothole", "oil", "stain", "leaf", "moss", "dirt_patch", "collision", "block", "blocker"))
        {
            return ClassifyTileText(text);
        }

        return Classification.Unknown;
    }

    private static Classification ClassifyBuildingText(string text)
    {
        return new Classification(MapAssetKind.Building, MapBuildingCategory(string.Empty, text));
    }

    private static Classification ClassifyPropText(string text)
    {
        return new Classification(MapAssetKind.Prop, MapPropCategory(string.Empty, text));
    }

    private static Classification ClassifyTileText(string text)
    {
        return new Classification(MapAssetKind.Tile, MapTileCategory(string.Empty, text));
    }

    private static string MapTileCategory(string category, string context)
    {
        string compactCategory = Compact(category);
        string text = Normalize($"{category} {context}");

        if (compactCategory == "grass")
        {
            return "Grass";
        }

        if (compactCategory == "forest")
        {
            return "Forest";
        }

        if (compactCategory == "dirt" || compactCategory == "dirtpath")
        {
            return "Dirt";
        }

        if (compactCategory == "villageplaza")
        {
            return "VillagePlaza";
        }

        if (compactCategory == "sand")
        {
            return "Sand";
        }

        if (compactCategory == "water")
        {
            return "Water";
        }

        if (compactCategory == "roadmarking" || compactCategory == "roadline")
        {
            return "Road_Marking";
        }

        if (compactCategory == "road")
        {
            return "Road";
        }

        if (compactCategory == "sidewalk")
        {
            return "Sidewalk";
        }

        if (compactCategory == "curb" || compactCategory == "kerb")
        {
            return "Curb";
        }

        if (compactCategory == "decals" || compactCategory == "decal")
        {
            return "Decals";
        }

        if (compactCategory == "collision")
        {
            return "Collision";
        }

        if (ContainsAny(text, "roadmarking", "road_marking", "road_line", "line", "dash", "crosswalk", "parking_line", "stop_line", "marking"))
        {
            return "Road_Marking";
        }

        if (ContainsAny(text, "curb", "kerb", "road_edge"))
        {
            return "Curb";
        }

        if (ContainsAny(text, "sidewalk", "pavement", "stone_path", "walkway"))
        {
            return "Sidewalk";
        }

        if (ContainsAny(text, "collision", "block", "blocker"))
        {
            return "Collision";
        }

        if (ContainsAny(text, "decal", "crack", "pothole", "oil", "stain", "leaf", "moss", "dirt_patch"))
        {
            return "Decals";
        }

        if (ContainsAny(text, "water", "ocean", "sea", "shore", "foam", "pond", "lake"))
        {
            return "Water";
        }

        if (ContainsAny(text, "sand", "beach"))
        {
            return "Sand";
        }

        if (ContainsAny(text, "plaza", "stone_plaza", "village_plaza", "villageplaza"))
        {
            return "VillagePlaza";
        }

        if (ContainsAny(text, "road", "asphalt", "street", "pavement_road"))
        {
            return "Road";
        }

        if (ContainsAny(text, "dirt", "path", "trail", "soil", "village_path"))
        {
            return "Dirt";
        }

        if (ContainsAny(text, "forest", "dense", "jungle", "woods"))
        {
            return "Forest";
        }

        return "Grass";
    }

    private static string MapPropCategory(string category, string context)
    {
        string compactCategory = Compact(category);
        string text = Normalize($"{category} {context}");

        if (compactCategory == "nature")
        {
            return "Nature";
        }

        if (compactCategory == "beach")
        {
            return "Beach";
        }

        if (compactCategory == "town")
        {
            return "Town";
        }

        if (compactCategory == "farm")
        {
            return "Farm";
        }

        if (compactCategory == "street")
        {
            return "Street";
        }

        if (compactCategory == "parking")
        {
            return "Parking";
        }

        if (ContainsAny(text, "umbrella", "beach_chair", "towel", "dock", "hut", "palm", "beach_sign", "beach"))
        {
            return "Beach";
        }

        if (ContainsAny(text, "parking", "parking_meter", "parking_prop"))
        {
            return "Parking";
        }

        if (ContainsAny(text, "crop", "field", "haystack", "well", "rural", "farm", "gate"))
        {
            return "Farm";
        }

        if (ContainsAny(text, "lamp", "sign", "bench", "mailbox", "trash", "cone", "barrel", "crate", "tire", "fence", "post"))
        {
            return "Street";
        }

        if (ContainsAny(text, "town_prop", "urban_prop", "town"))
        {
            return "Town";
        }

        return "Nature";
    }

    private static string MapBuildingCategory(string category, string context)
    {
        string compactCategory = Compact(category);
        string text = Normalize($"{category} {context}");

        if (compactCategory == "village")
        {
            return "Village";
        }

        if (compactCategory == "town")
        {
            return "Town";
        }

        if (compactCategory == "shops" || compactCategory == "shop")
        {
            return "Shops";
        }

        if (compactCategory == "garages" || compactCategory == "garage")
        {
            return "Garages";
        }

        if (compactCategory == "landmarks" || compactCategory == "landmark")
        {
            return "Landmarks";
        }

        if (ContainsAny(text, "lighthouse", "monument", "fountain_area", "landmark", "plaza_monument", "bridge"))
        {
            return "Landmarks";
        }

        if (ContainsAny(text, "shop", "store", "market"))
        {
            return "Shops";
        }

        if (ContainsAny(text, "garage", "auto", "parking_building"))
        {
            return "Garages";
        }

        if (ContainsAny(text, "town_house", "city_house", "town"))
        {
            return "Town";
        }

        return "Village";
    }

    private static void ValidatePng(string path, PngInfo info, Classification classification, IngestReport report)
    {
        if (classification.Kind == MapAssetKind.Tile)
        {
            if (info.Width == 32 && info.Height == 32)
            {
                return;
            }

            if (info.Width == 16 && info.Height == 16)
            {
                report.Warnings.Add($"{path}: legacy tile size 16x16.");
                return;
            }

            if (info.Width > 64 || info.Height > 64)
            {
                string message = $"{path}: suspicious tile size {info.Width}x{info.Height}; possible spritesheet or large tile.";
                report.SuspiciousFiles.Add(message);
                report.Warnings.Add(message);
                return;
            }

            report.Warnings.Add($"{path}: tile size {info.Width}x{info.Height}; expected 32x32.");
        }

        if ((classification.Kind == MapAssetKind.Prop || classification.Kind == MapAssetKind.Building) && !info.HasAlpha)
        {
            report.Warnings.Add($"{path}: prop/building PNG has no alpha transparency.");
        }

        if (info.LooksLikeCheckerboard)
        {
            string message = $"{path}: possible checkerboard background baked into pixels.";
            report.SuspiciousFiles.Add(message);
            report.Warnings.Add(message);
        }
    }

    private static bool LooksLikeSpritesheet(string path, PngInfo info, Classification classification)
    {
        string text = Normalize(path);
        bool nameSaysSheet = ContainsAny(text, "spritesheet", "sprite_sheet", "atlas", "sheet");
        bool tileTooLarge = classification.Kind == MapAssetKind.Tile && (info.Width > 64 || info.Height > 64);
        bool gridLikeLarge = info.Width >= 128 && info.Height >= 128 && info.Width % 16 == 0 && info.Height % 16 == 0;
        return nameSaysSheet || tileTooLarge || (gridLikeLarge && classification.Kind != MapAssetKind.Unknown);
    }

    private static PngInfo ReadPngInfo(string assetPath, IngestReport report)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                report.Warnings.Add($"{assetPath}: could not decode PNG.");
                return new PngInfo(0, 0, false, false);
            }

            Color32[] pixels = texture.GetPixels32();
            bool hasAlpha = pixels.Any(pixel => pixel.a < 250);
            bool checkerboard = LooksLikeCheckerboard(texture, pixels);
            return new PngInfo(texture.width, texture.height, hasAlpha, checkerboard);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static bool LooksLikeCheckerboard(Texture2D texture, Color32[] pixels)
    {
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

                bool gray = Mathf.Abs(color.r - color.g) <= 8 && Mathf.Abs(color.g - color.b) <= 8;
                bool bright = color.r >= 180 && color.r <= 255;
                if (gray && bright)
                {
                    grayOpaque++;
                }
            }
        }

        return transparent == 0 && sampled > 0 && grayOpaque >= sampled * 0.45f;
    }

    private static void AddMissingCategoryWarnings(IngestReport report)
    {
        AddMissing(report, MapAssetKind.Tile, TileCategories);
        AddMissing(report, MapAssetKind.Prop, PropCategories);
        AddMissing(report, MapAssetKind.Building, BuildingCategories);
    }

    private static void AddMissing(IngestReport report, MapAssetKind kind, IEnumerable<string> categories)
    {
        foreach (string category in categories)
        {
            string folder = GetCanonicalFolder(new Classification(kind, category));
            bool hasPng = Directory.Exists(folder) && Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories).Length > 0;
            if (!hasPng)
            {
                report.MissingImportantCategories.Add($"{kind}: {category}");
            }
        }
    }

    private static void WriteReports(IngestReport report)
    {
        EnsureFolder("Assets/Docs");

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# TEFO Map Asset Ingest Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Run mode: {report.Mode}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Total PNG scanned from TEFO_Map_AssetPack: {report.TotalPngScannedFromAssetPack}");
        builder.AppendLine($"- Total PNG files scanned: {report.TotalPngScanned}");
        builder.AppendLine($"- Total PNG files classified: {report.TotalPngClassified}");
        builder.AppendLine($"- Total PNG files copied to canonical folders: {report.TotalPngCopied}");
        builder.AppendLine($"- Import settings updated: {report.ImportSettingsUpdated}");
        builder.AppendLine($"- Tile assets created: {report.TileAssetsCreated}");
        builder.AppendLine($"- Tile assets updated: {report.TileAssetsUpdated}");
        builder.AppendLine($"- Prop prefabs created: {report.PropPrefabsCreated}");
        builder.AppendLine($"- Prop prefabs updated: {report.PropPrefabsUpdated}");
        builder.AppendLine($"- Building prefabs created: {report.BuildingPrefabsCreated}");
        builder.AppendLine($"- Building prefabs updated: {report.BuildingPrefabsUpdated}");
        builder.AppendLine($"- Total suspicious files: {report.SuspiciousFiles.Distinct().Count()}");
        builder.AppendLine($"- Total duplicate filename warnings: {report.DuplicateFiles.Distinct().Count()}");
        builder.AppendLine($"- Total skipped files: {report.SkippedFiles.Count}");
        builder.AppendLine($"- Manifest entries loaded: {report.ManifestEntriesLoaded}");
        builder.AppendLine($"- Map layer stack ensured: {(report.LayerSetupEnsured ? "yes" : "no")}");
        builder.AppendLine();
        AppendList(builder, "Source Roots Scanned", report.SourceRootsScanned);
        AppendList(builder, "Manifest Files Used", report.ManifestFiles);
        AppendList(builder, "Source Path To Destination Path List", report.SourceToDestinationFiles);
        AppendList(builder, "Copied Files", report.CopiedFiles);
        AppendList(builder, "Missing Categories After Rebuild", report.MissingImportantCategories);
        AppendList(builder, "Suspicious Files", report.SuspiciousFiles);
        AppendList(builder, "Duplicate Files", report.DuplicateFiles);
        AppendList(builder, "Unknown Category Files", report.UnknownCategoryFiles);
        AppendList(builder, "Warnings", report.Warnings);
        AppendList(builder, "Skipped Files", report.SkippedFiles);
        builder.AppendLine("## Next Steps");
        builder.AppendLine();
        builder.AppendLine("1. Run `TEFO > Map > Assets > Full Fix And Setup Map Assets` after adding or replacing generated PNGs.");
        builder.AppendLine("2. Review this report for skipped files, suspicious spritesheets, duplicate names, and missing categories.");
        builder.AppendLine("3. Manually slice PNGs copied to `Assets/Art/Map/Spritesheets/NeedsManualSlice` if they are real spritesheets.");
        builder.AppendLine("4. Run `TEFO > Map > Demo > Generate Reference Map Demo` after Tile assets and prefabs are available.");
        builder.AppendLine("5. Check `Assets/Docs/MAP_DEMO_GENERATION_REPORT.md` after demo generation.");
        builder.AppendLine();

        File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
        File.WriteAllText(ImportReportPath, BuildImportReportSummary(report), Encoding.UTF8);
        AssetDatabase.ImportAsset(ReportPath);
        AssetDatabase.ImportAsset(ImportReportPath);
    }

    private static string BuildImportReportSummary(IngestReport report)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# TEFO Map Import Report");
        builder.AppendLine();
        builder.AppendLine($"Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine("Source: `TEFOMapAssetIngestTool`");
        builder.AppendLine();
        builder.AppendLine($"- Total PNG scanned from TEFO_Map_AssetPack: {report.TotalPngScannedFromAssetPack}");
        builder.AppendLine($"- Total PNG copied to canonical folders: {report.TotalPngCopied}");
        builder.AppendLine("- Canonical map PNGs use Sprite/Single, PPU 32, Point filter, no compression, no mipmaps, Clamp wrap, Full Rect mesh, and no generated physics shape.");
        builder.AppendLine($"- Tile assets created: {report.TileAssetsCreated}");
        builder.AppendLine($"- Tile assets updated: {report.TileAssetsUpdated}");
        builder.AppendLine($"- Prop prefabs created: {report.PropPrefabsCreated}");
        builder.AppendLine($"- Prop prefabs updated: {report.PropPrefabsUpdated}");
        builder.AppendLine($"- Building prefabs created: {report.BuildingPrefabsCreated}");
        builder.AppendLine($"- Building prefabs updated: {report.BuildingPrefabsUpdated}");
        builder.AppendLine($"- Missing categories after rebuild: {report.MissingImportantCategories.Distinct().Count()}");
        builder.AppendLine($"- Suspicious files: {report.SuspiciousFiles.Distinct().Count()}");
        builder.AppendLine($"- Duplicate files: {report.DuplicateFiles.Distinct().Count()}");
        builder.AppendLine($"- Full details: `{ReportPath}`");
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string title, IEnumerable<string> lines)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        List<string> items = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct().OrderBy(line => line).ToList();
        if (items.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (string item in items)
            {
                builder.AppendLine($"- {item}");
            }
        }

        builder.AppendLine();
    }

    private static string GetCanonicalFolder(Classification classification)
    {
        if (classification.Kind == MapAssetKind.Tile)
        {
            return $"Assets/Art/Map/Tiles/{classification.Category}";
        }

        if (classification.Kind == MapAssetKind.Prop)
        {
            return $"Assets/Art/Map/Props/{classification.Category}";
        }

        if (classification.Kind == MapAssetKind.Building)
        {
            return $"Assets/Art/Map/Buildings/{classification.Category}";
        }

        if (classification.Kind == MapAssetKind.Spritesheet)
        {
            return SpritesheetReviewFolder;
        }

        return string.Empty;
    }

    private static int GetPropSortingOrder(ScannedPng png)
    {
        string category = png.Classification.Category;
        if (!string.Equals(category, "Town", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(category, "Street", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string text = Normalize(png.AssetPath);
        return ContainsAny(text, "front", "foreground", "lamp", "sign", "fence", "barrel", "crate", "tire") || png.Info.Height > 48 ? 30 : 0;
    }

    private static bool ShouldPropBlock(ScannedPng png)
    {
        string text = Normalize(png.AssetPath);
        return ContainsAny(text, "tree", "rock_large", "lamp", "fence", "barrel", "crate", "tire", "dock_pillar", "stump");
    }

    private static string GetCategoryAfter(string path, string marker)
    {
        string remainder = path.Substring(marker.Length);
        int slash = remainder.IndexOf('/');
        return slash < 0 ? string.Empty : remainder.Substring(0, slash);
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(Normalize(token)));
    }

    private static bool ShouldIgnorePath(string path)
    {
        string normalized = NormalizePath(path);
        return IgnoredPathParts.Any(part => normalized.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
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
        string lower = name.ToLowerInvariant();
        StringBuilder builder = new StringBuilder(lower.Length);
        foreach (char c in lower)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_');
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

    private static string Normalize(string text)
    {
        return NormalizePath(text ?? string.Empty).Replace("-", "_").Replace(" ", "_").ToLowerInvariant();
    }

    private static string Compact(string text)
    {
        return Normalize(text).Replace("_", string.Empty).Replace("/", string.Empty);
    }

    private readonly struct RunOptions
    {
        public readonly bool CopyToCanonical;
        public readonly bool RebuildAssets;
        public readonly bool SetupLayers;
        public readonly string Mode;

        public RunOptions(bool copyToCanonical, bool rebuildAssets, bool setupLayers, string mode)
        {
            CopyToCanonical = copyToCanonical;
            RebuildAssets = rebuildAssets;
            SetupLayers = setupLayers;
            Mode = mode;
        }
    }

    private readonly struct SourceRoot
    {
        public readonly string Path;
        public readonly bool IsAssetPack;
        public readonly bool IsFallback;

        public SourceRoot(string path, bool isAssetPack, bool isFallback)
        {
            Path = path;
            IsAssetPack = isAssetPack;
            IsFallback = isFallback;
        }
    }

    private readonly struct ScannedPng
    {
        public readonly string AssetPath;
        public readonly Classification Classification;
        public readonly PngInfo Info;

        public ScannedPng(string assetPath, Classification classification, PngInfo info)
        {
            AssetPath = assetPath;
            Classification = classification;
            Info = info;
        }
    }

    private readonly struct PngInfo
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool HasAlpha;
        public readonly bool LooksLikeCheckerboard;

        public PngInfo(int width, int height, bool hasAlpha, bool looksLikeCheckerboard)
        {
            Width = width;
            Height = height;
            HasAlpha = hasAlpha;
            LooksLikeCheckerboard = looksLikeCheckerboard;
        }
    }

    private readonly struct ManifestEntry
    {
        public static readonly ManifestEntry Invalid = new ManifestEntry(string.Empty, Classification.Unknown);

        public readonly string Token;
        public readonly Classification Classification;
        public bool IsValid => !string.IsNullOrEmpty(Token) && Classification.Kind != MapAssetKind.Unknown;

        public ManifestEntry(string token, Classification classification)
        {
            Token = token;
            Classification = classification;
        }
    }

    private readonly struct Classification
    {
        public static readonly Classification Unknown = new Classification(MapAssetKind.Unknown, string.Empty);

        public readonly MapAssetKind Kind;
        public readonly string Category;

        public Classification(MapAssetKind kind, string category)
        {
            Kind = string.IsNullOrEmpty(category) ? MapAssetKind.Unknown : kind;
            Category = category;
        }

        public static Classification Spritesheet(string category)
        {
            return new Classification(MapAssetKind.Spritesheet, category);
        }
    }

    private enum MapAssetKind
    {
        Unknown,
        Tile,
        Prop,
        Building,
        Spritesheet
    }

    private sealed class IngestReport
    {
        public readonly string Mode;
        public int TotalPngScanned;
        public int TotalPngScannedFromAssetPack;
        public int TotalPngClassified;
        public int TotalPngCopied;
        public int SuspiciousSpritesheets;
        public int ImportSettingsUpdated;
        public int TileAssetsCreated;
        public int TileAssetsUpdated;
        public int PropPrefabsCreated;
        public int PropPrefabsUpdated;
        public int BuildingPrefabsCreated;
        public int BuildingPrefabsUpdated;
        public int ManifestEntriesLoaded;
        public bool LayerSetupEnsured;

        public readonly List<string> SourceRootsScanned = new List<string>();
        public readonly List<string> ManifestFiles = new List<string>();
        public readonly List<string> SourceToDestinationFiles = new List<string>();
        public readonly List<string> CopiedFiles = new List<string>();
        public readonly List<string> CanonicalFiles = new List<string>();
        public readonly List<string> DuplicateFiles = new List<string>();
        public readonly List<string> SuspiciousFiles = new List<string>();
        public readonly List<string> UnknownCategoryFiles = new List<string>();
        public readonly List<string> SkippedFiles = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> MissingImportantCategories = new List<string>();

        public IngestReport(string mode)
        {
            Mode = mode;
        }
    }
}
#endif
