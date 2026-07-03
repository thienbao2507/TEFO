#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class TEFOSmallMapRebuildTool
{
    private const int MapWidth = 48;
    private const int MapHeight = 80;
    private const int Ppu = 32;
    private const string RootName = "TEFO_Map";
    private const string GridName = "Grid";
    private const string ReportPath = "Assets/Docs/SMALL_MAP_REBUILD_REPORT.md";
    private const string BackupScenePath = "Assets/Scenes/MainScene_Backup_Before_SmallMap_Rebuild.unity";
    private const string PropsContainerName = "TEFO_Demo_Generated_Props";
    private const string BuildingsContainerName = "TEFO_Demo_Generated_Buildings";
    private const string MetadataName = "TEFO_Demo_Generated_Metadata";

    private static readonly string[] PackRoots =
    {
        "Assets/TEFO_SmallMap_Rebuild_AssetPack",
        "TEFO_SmallMap_Rebuild_AssetPack",
        "Assets/Downloads/TEFO_SmallMap_Rebuild_AssetPack",
        "Assets/Generated/TEFO_SmallMap_Rebuild_AssetPack"
    };

    private static readonly string[] TileCategories =
    {
        "Grass", "Dirt", "Road", "Road_Marking", "Sidewalk", "Curb", "Stone", "Decals"
    };

    private static readonly string[] PropCategories =
    {
        "Nature", "Town", "Street"
    };

    private static readonly LayerSpec[] LayerSpecs =
    {
        new LayerSpec("Ground", -100, false, true),
        new LayerSpec("Grass", -90, false, true),
        new LayerSpec("Dirt", -80, false, true),
        new LayerSpec("Road", -70, false, true),
        new LayerSpec("Road_Marking", -60, false, true),
        new LayerSpec("Sidewalk", -50, false, true),
        new LayerSpec("Curb", -45, false, true),
        new LayerSpec("Stone", -40, false, true),
        new LayerSpec("Decals_Back", -20, false, true),
        new LayerSpec("Props_Back", 0, false, true),
        new LayerSpec("Props_Front", 35, false, true),
        new LayerSpec("Collision", 100, true, false)
    };

    private static readonly HashSet<string> ProtectedObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Player",
        "Player_old",
        "Main Camera",
        "Global Light 2D",
        "GameManager",
        "Canvas",
        "EventSystem",
        "PlayerSpawnPoint",
        "Car_Basic",
        "Car_Truck",
        "Car_Sport"
    };

    [MenuItem("TEFO/Map/Small Rebuild/Full Rebuild Small Map")]
    public static void FullRebuildSmallMap()
    {
        RebuildReport report = new RebuildReport("Full Rebuild Small Map");
        BackupCurrentScene(report);
        SmallPack pack = FindSmallPack(report);
        List<ManifestEntry> manifestEntries = LoadManifest(pack, report);
        EnsureFolders();
        List<string> copiedPngs = IngestSmallPack(pack, manifestEntries, report);
        AssetDatabase.Refresh();
        ApplyImportSettings(copiedPngs, report);
        AssetDatabase.Refresh();
        CreateTileAssets(copiedPngs, report);
        CreatePropPrefabs(copiedPngs, report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ClearOldMapOnly(report);
        SmallMapScene scene = CreateSmallMapLayerStack(report);
        GenerateSmallNeighborhoodMap(scene, report);
        MovePlayerCameraAndCar(report);
        WriteReport(report);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"TEFO small map rebuild finished. Report: {ReportPath}");
    }

    [MenuItem("TEFO/Map/Small Rebuild/Clear Old Map Only")]
    public static void ClearOldMapOnlyMenu()
    {
        RebuildReport report = new RebuildReport("Clear Old Map Only");
        BackupCurrentScene(report);
        ClearOldMapOnly(report);
        WriteReport(report);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"TEFO old map clear finished. Report: {ReportPath}");
    }

    [MenuItem("TEFO/Map/Small Rebuild/Generate Small Neighborhood Map")]
    public static void GenerateSmallNeighborhoodMapMenu()
    {
        RebuildReport report = new RebuildReport("Generate Small Neighborhood Map");
        BackupCurrentScene(report);
        ClearOldMapOnly(report);
        SmallMapScene scene = CreateSmallMapLayerStack(report);
        GenerateSmallNeighborhoodMap(scene, report);
        MovePlayerCameraAndCar(report);
        WriteReport(report);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"TEFO small neighborhood map generated. Report: {ReportPath}");
    }

    private static void BackupCurrentScene(RebuildReport report)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            StopBeforeMapDelete(report, "Active scene has no asset path; small map rebuild stopped before deleting old map because backup scene could not be created.");
        }

        if (!EditorSceneManager.SaveScene(activeScene))
        {
            StopBeforeMapDelete(report, "Active scene could not be saved; small map rebuild stopped before deleting old map because backup scene could not be created.");
        }

        EnsureFolder("Assets/Scenes");
        string backupPath = AssetDatabase.GenerateUniqueAssetPath(BackupScenePath);
        if (AssetDatabase.CopyAsset(activeScene.path, backupPath))
        {
            report.BackupScenePath = backupPath;
            AssetDatabase.ImportAsset(backupPath);
        }
        else
        {
            StopBeforeMapDelete(report, $"Failed to create backup scene from {activeScene.path}; small map rebuild stopped before deleting old map.");
        }
    }

    private static void StopBeforeMapDelete(RebuildReport report, string message)
    {
        report.Warnings.Add(message);
        WriteReport(report);
        throw new InvalidOperationException(message);
    }

    private static SmallPack FindSmallPack(RebuildReport report)
    {
        foreach (string root in PackRoots)
        {
            string unityReadyRoot = NormalizePath($"{root}/UnityReady/Assets/Art/Map");
            string manifestPath = NormalizePath($"{root}/Docs/asset_manifest.csv");
            if (Directory.Exists(unityReadyRoot) && File.Exists(manifestPath))
            {
                report.SourcePackRoot = NormalizePath(root);
                report.ManifestPath = manifestPath;
                return new SmallPack(NormalizePath(root), unityReadyRoot, manifestPath);
            }
        }

        report.Warnings.Add("TEFO_SmallMap_Rebuild_AssetPack was not found in any expected location.");
        return SmallPack.Invalid;
    }

    private static List<ManifestEntry> LoadManifest(SmallPack pack, RebuildReport report)
    {
        List<ManifestEntry> entries = new List<ManifestEntry>();
        if (!pack.IsValid)
        {
            return entries;
        }

        string[] lines = File.ReadAllLines(pack.ManifestPath);
        if (lines.Length == 0)
        {
            report.Warnings.Add($"{pack.ManifestPath}: manifest is empty.");
            return entries;
        }

        List<string> headers = SplitCsvLine(lines[0]).Select(NormalizeHeader).ToList();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
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
            string folder = NormalizePath(GetValue(values, "suggested_unity_folder"));
            string assetType = GetValue(values, "asset_type");
            if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(folder))
            {
                report.Warnings.Add($"{pack.ManifestPath}: skipped incomplete manifest row {i + 1}.");
                continue;
            }

            string sourcePath = FindSourcePng(pack.UnityReadyMapRoot, filename);
            if (string.IsNullOrEmpty(sourcePath))
            {
                report.Warnings.Add($"{filename}: manifest source PNG not found under {pack.UnityReadyMapRoot}.");
                continue;
            }

            entries.Add(new ManifestEntry(filename, sourcePath, folder, assetType));
            report.SmallPackAssetNames.Add(Path.GetFileNameWithoutExtension(filename));
        }

        report.ManifestEntries = entries.Count;
        return entries;
    }

    private static string FindSourcePng(string root, string filename)
    {
        foreach (string rawPath in Directory.GetFiles(root, filename, SearchOption.AllDirectories))
        {
            return NormalizePath(rawPath);
        }

        return string.Empty;
    }

    private static List<string> IngestSmallPack(SmallPack pack, List<ManifestEntry> entries, RebuildReport report)
    {
        List<string> copied = new List<string>();
        if (!pack.IsValid)
        {
            return copied;
        }

        foreach (ManifestEntry entry in entries)
        {
            string destinationFolder = NormalizePath(entry.SuggestedUnityFolder);
            if (!destinationFolder.StartsWith("Assets/Art/Map/", StringComparison.OrdinalIgnoreCase))
            {
                report.Warnings.Add($"{entry.Filename}: manifest destination is outside Assets/Art/Map and was skipped: {destinationFolder}");
                continue;
            }

            EnsureFolder(destinationFolder);
            string destinationPath = NormalizePath($"{destinationFolder}/{SanitizeFileName(entry.Filename)}");
            if (File.Exists(destinationPath))
            {
                if (FilesHaveSameBytes(entry.SourcePath, destinationPath))
                {
                    copied.Add(destinationPath);
                    report.SourceToDestination.Add($"{entry.SourcePath} -> {destinationPath} (identical existing file reused)");
                    continue;
                }

                destinationPath = GenerateUniqueAssetFilePath(destinationPath);
                report.Warnings.Add($"{entry.Filename}: destination already existed with different contents; copied to {destinationPath}.");
            }

            File.Copy(entry.SourcePath, destinationPath, overwrite: false);
            AssetDatabase.ImportAsset(destinationPath);
            copied.Add(destinationPath);
            report.PngsIngested++;
            report.SourceToDestination.Add($"{entry.SourcePath} -> {destinationPath}");
        }

        return copied.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ApplyImportSettings(IEnumerable<string> pngPaths, RebuildReport report)
    {
        foreach (string path in pngPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                report.Warnings.Add($"{path}: TextureImporter not found.");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
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

    private static void CreateTileAssets(IEnumerable<string> pngPaths, RebuildReport report)
    {
        IEnumerable<string> tilePngs = pngPaths
            .Where(path => NormalizePath(path).StartsWith("Assets/Art/Map/Tiles/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string pngPath in tilePngs)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite == null)
            {
                report.Warnings.Add($"{pngPath}: sprite not available after import.");
                continue;
            }

            string category = GetCategoryAfter(pngPath, "Assets/Art/Map/Tiles/");
            if (string.IsNullOrEmpty(category))
            {
                report.Warnings.Add($"{pngPath}: could not determine tile category.");
                continue;
            }

            string targetFolder = NormalizePath($"Assets/Tiles/{category}");
            EnsureFolder(targetFolder);
            string targetPath = NormalizePath($"{targetFolder}/{Path.GetFileNameWithoutExtension(pngPath)}.asset");
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(targetPath);
            if (tile == null && File.Exists(targetPath))
            {
                report.Warnings.Add($"{targetPath}: target exists but is not a Tile asset.");
                continue;
            }

            Tile.ColliderType colliderType = category.Equals("Collision", StringComparison.OrdinalIgnoreCase) ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = Path.GetFileNameWithoutExtension(pngPath);
                tile.sprite = sprite;
                tile.colliderType = colliderType;
                AssetDatabase.CreateAsset(tile, targetPath);
                report.TileAssetsCreated++;
            }
            else
            {
                bool changed = tile.sprite != sprite || tile.colliderType != colliderType;
                tile.sprite = sprite;
                tile.colliderType = colliderType;
                if (changed)
                {
                    EditorUtility.SetDirty(tile);
                    report.TileAssetsUpdated++;
                }
            }
        }
    }

    private static void CreatePropPrefabs(IEnumerable<string> pngPaths, RebuildReport report)
    {
        IEnumerable<string> propPngs = pngPaths
            .Where(path => NormalizePath(path).StartsWith("Assets/Art/Map/Props/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string pngPath in propPngs)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite == null)
            {
                report.Warnings.Add($"{pngPath}: sprite not available after import.");
                continue;
            }

            string category = GetCategoryAfter(pngPath, "Assets/Art/Map/Props/");
            if (string.IsNullOrEmpty(category))
            {
                report.Warnings.Add($"{pngPath}: could not determine prop category.");
                continue;
            }

            string targetFolder = NormalizePath($"Assets/Prefabs/Props/{category}");
            EnsureFolder(targetFolder);
            string targetPath = NormalizePath($"{targetFolder}/{Path.GetFileNameWithoutExtension(pngPath)}.prefab");
            bool existed = File.Exists(targetPath);
            GameObject root = existed ? PrefabUtility.LoadPrefabContents(targetPath) : new GameObject(Path.GetFileNameWithoutExtension(pngPath));
            try
            {
                root.name = Path.GetFileNameWithoutExtension(pngPath);
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
                renderer.sortingOrder = category.Equals("Nature", StringComparison.OrdinalIgnoreCase) ? 25 : 30;

                bool shouldBlock = ShouldPropBlock(pngPath);
                BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
                if (shouldBlock && collider == null)
                {
                    root.AddComponent<BoxCollider2D>();
                }
                else if (!shouldBlock && collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                PrefabUtility.SaveAsPrefabAsset(root, targetPath);
                if (existed)
                {
                    report.PropPrefabsUpdated++;
                }
                else
                {
                    report.PropPrefabsCreated++;
                }
            }
            finally
            {
                if (existed)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }

    private static bool ShouldPropBlock(string path)
    {
        string text = Normalize(path);
        string[] tokens = { "tree", "rock", "lamp", "bench", "bollard", "sign", "mailbox", "trash", "fence", "post" };
        return tokens.Any(text.Contains);
    }

    private static void ClearOldMapOnly(RebuildReport report)
    {
        string[] namesToDelete =
        {
            RootName,
            GridName,
            "Road_Test",
            "Road_Line_01",
            "Road_Line_02",
            "Road_Line_03",
            "Road_Line_04",
            "Road_Line_05",
            PropsContainerName,
            BuildingsContainerName,
            MetadataName
        };

        foreach (string objectName in namesToDelete)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing == null)
            {
                continue;
            }

            if (IsProtectedObject(existing) || ContainsProtectedDescendant(existing.transform))
            {
                report.Warnings.Add($"{objectName}: not deleted because it is protected or contains protected gameplay objects.");
                continue;
            }

            report.DeletedOldObjects.Add(GetHierarchyPath(existing.transform));
            Undo.DestroyObjectImmediate(existing);
        }
    }

    private static SmallMapScene CreateSmallMapLayerStack(RebuildReport report)
    {
        GameObject root = GetOrCreateSceneObject(RootName, "Create TEFO small map root", report);
        GameObject gridObject = GetOrCreateChild(root.transform, GridName, "Create TEFO small map grid", report);
        ResetTransform(root.transform);
        ResetTransform(gridObject.transform);

        Grid grid = GetOrAddComponent<Grid>(gridObject);
        grid.cellSize = new Vector3(1f, 1f, 0f);
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        Dictionary<string, Tilemap> tilemaps = new Dictionary<string, Tilemap>(StringComparer.OrdinalIgnoreCase);
        foreach (LayerSpec spec in LayerSpecs)
        {
            GameObject layer = GetOrCreateChild(gridObject.transform, spec.Name, $"Create {spec.Name}", report);
            ResetTransform(layer.transform);

            Tilemap tilemap = GetOrAddComponent<Tilemap>(layer);
            tilemap.ClearAllTiles();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            tilemap.color = spec.Visible ? Color.white : new Color(1f, 0f, 0f, 0.2f);

            TilemapRenderer renderer = GetOrAddComponent<TilemapRenderer>(layer);
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = spec.SortingOrder;
            renderer.enabled = spec.Visible;

            if (spec.HasCollider)
            {
                TilemapCollider2D tilemapCollider = GetOrAddComponent<TilemapCollider2D>(layer);
                Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(layer);
                CompositeCollider2D compositeCollider = GetOrAddComponent<CompositeCollider2D>(layer);
                tilemapCollider.usedByComposite = true;
                compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
                body.bodyType = RigidbodyType2D.Static;
                body.simulated = true;
            }

            tilemaps[spec.Name] = tilemap;
        }

        Transform propsContainer = CreateGeneratedContainer(root.transform, PropsContainerName, report);
        GameObject metadata = GetOrCreateChild(root.transform, MetadataName, "Create small map metadata marker", report);
        ResetTransform(metadata.transform);
        metadata.hideFlags = HideFlags.NotEditable;

        report.MapLayerStackCreated = true;
        return new SmallMapScene(root, tilemaps, propsContainer);
    }

    private static T GetOrAddComponent<T>(GameObject obj) where T : Component
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj), "Cannot get or add a component on a null GameObject.");
        }

        T component = obj.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(obj);
    }

    private static GameObject GetOrCreateSceneObject(string name, string undoName, RebuildReport report)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            report.Warnings.Add($"{name}: existing scene object reused.");
            return existing;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, undoName);
        return created;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name, string undoName, RebuildReport report)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent), $"Cannot create or reuse child {name} without a parent.");
        }

        Transform existingChild = parent.Find(name);
        if (existingChild != null)
        {
            report.Warnings.Add($"{GetHierarchyPath(existingChild)}: existing layer/container reused.");
            return existingChild.gameObject;
        }

        GameObject existingByName = GameObject.Find(name);
        if (existingByName != null && !IsProtectedObject(existingByName) && !ContainsProtectedDescendant(existingByName.transform))
        {
            Undo.SetTransformParent(existingByName.transform, parent, $"Parent existing {name}");
            report.Warnings.Add($"{name}: existing scene object reparented under {GetHierarchyPath(parent)}.");
            return existingByName;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, undoName);
        Undo.SetTransformParent(created.transform, parent, $"Parent {name}");
        return created;
    }

    private static Transform CreateGeneratedContainer(Transform root, string name, RebuildReport report)
    {
        GameObject container = GetOrCreateChild(root, name, $"Create {name}", report);
        ResetTransform(container.transform);
        ClearGeneratedChildren(container.transform, report);
        return container.transform;
    }

    private static void ClearGeneratedChildren(Transform container, RebuildReport report)
    {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in container)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            if (IsProtectedObject(child) || ContainsProtectedDescendant(child.transform))
            {
                report.Warnings.Add($"{GetHierarchyPath(child.transform)}: generated child was not cleared because it is protected.");
                continue;
            }

            Undo.DestroyObjectImmediate(child);
        }
    }

    private static void GenerateSmallNeighborhoodMap(SmallMapScene scene, RebuildReport report)
    {
        SmallTileLibrary tiles = SmallTileLibrary.Load(report.SmallPackAssetNames, report);
        SmallPropLibrary props = SmallPropLibrary.Load(report.SmallPackAssetNames, report);
        SmallMapState state = new SmallMapState();

        PaintBase(scene, tiles, report);
        PaintRoads(scene, tiles, state, report);
        PaintCentralLane(scene, tiles, state, report);
        PaintResidentialBlocks(scene, tiles, state, report);
        PaintYards(scene, tiles, state, report);
        PaintRoadMarkings(scene, tiles, report);
        PlaceSmallMapProps(scene, props, state, report);
        ReserveBuildingLots(state, report);
    }

    private static void PaintBase(SmallMapScene scene, SmallTileLibrary tiles, RebuildReport report)
    {
        TileBase ground = tiles.Pick("Grass", "grass");
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                PaintCell(scene, report, "Ground", ground, x, y);
                PaintCell(scene, report, "Grass", tiles.Pick("Grass", "grass", "lawn"), x, y);
            }
        }
    }

    private static void PaintRoads(SmallMapScene scene, SmallTileLibrary tiles, SmallMapState state, RebuildReport report)
    {
        PaintRect(scene, report, "Road", tiles.Pick("Road", "road"), 0, 0, 8, MapHeight, state.Road);
        PaintRect(scene, report, "Road", tiles.Pick("Road", "road"), 40, 0, 8, MapHeight, state.Road);
        PaintRect(scene, report, "Road", tiles.Pick("Road", "road"), 0, 14, MapWidth, 5, state.Road);
        PaintRect(scene, report, "Road", tiles.Pick("Road", "road"), 0, 38, MapWidth, 5, state.Road);
        PaintRect(scene, report, "Road", tiles.Pick("Road", "road"), 0, 62, MapWidth, 5, state.Road);

        PaintVerticalSidewalk(scene, tiles, report, 8);
        PaintVerticalSidewalk(scene, tiles, report, 39);
        PaintHorizontalSidewalk(scene, tiles, report, 13);
        PaintHorizontalSidewalk(scene, tiles, report, 19);
        PaintHorizontalSidewalk(scene, tiles, report, 37);
        PaintHorizontalSidewalk(scene, tiles, report, 43);
        PaintHorizontalSidewalk(scene, tiles, report, 61);
        PaintHorizontalSidewalk(scene, tiles, report, 67);
    }

    private static void PaintCentralLane(SmallMapScene scene, SmallTileLibrary tiles, SmallMapState state, RebuildReport report)
    {
        TileBase lane = tiles.Pick("Stone", "stone", "plaza") ?? tiles.Pick("Dirt", "dirt") ?? tiles.Pick("Sidewalk", "plaza");
        PaintRect(scene, report, "Stone", lane, 22, 0, 6, MapHeight, state.Walkable);
        PaintRect(scene, report, "Curb", tiles.Pick("Curb", "curb") ?? tiles.Pick("Sidewalk", "edge"), 21, 0, 1, MapHeight, null);
        PaintRect(scene, report, "Curb", tiles.Pick("Curb", "curb") ?? tiles.Pick("Sidewalk", "edge"), 28, 0, 1, MapHeight, null);
    }

    private static void PaintResidentialBlocks(SmallMapScene scene, SmallTileLibrary tiles, SmallMapState state, RebuildReport report)
    {
        int[,] blocks =
        {
            { 10, 21, 20, 36 },
            { 29, 21, 38, 36 },
            { 10, 44, 20, 60 },
            { 29, 44, 38, 60 },
            { 10, 68, 20, 78 },
            { 29, 68, 38, 78 },
            { 10, 2, 20, 12 },
            { 29, 2, 38, 12 }
        };

        for (int i = 0; i < blocks.GetLength(0); i++)
        {
            for (int x = blocks[i, 0]; x <= blocks[i, 2]; x++)
            {
                for (int y = blocks[i, 1]; y <= blocks[i, 3]; y++)
                {
                    PaintCell(scene, report, "Grass", tiles.Pick("Grass", "grass", "lawn"), x, y);
                    state.Yard[x, y] = true;
                }
            }
        }
    }

    private static void PaintYards(SmallMapScene scene, SmallTileLibrary tiles, SmallMapState state, RebuildReport report)
    {
        PaintYard(scene, tiles, state, report, 10, 24, 9, 9);
        PaintYard(scene, tiles, state, report, 30, 27, 8, 10);
        PaintYard(scene, tiles, state, report, 9, 7, 11, 6);
        PaintYard(scene, tiles, state, report, 29, 8, 9, 6);
        PaintYard(scene, tiles, state, report, 11, 50, 8, 7);
        PaintYard(scene, tiles, state, report, 30, 51, 8, 7);
    }

    private static void PaintYard(SmallMapScene scene, SmallTileLibrary tiles, SmallMapState state, RebuildReport report, int x, int y, int width, int height)
    {
        TileBase yardTile = tiles.Pick("Stone", "stone", "plaza") ?? tiles.Pick("Sidewalk", "plaza", "sidewalk");
        PaintRect(scene, report, "Stone", yardTile, x, y, width, height, state.Yard);
    }

    private static void PaintRoadMarkings(SmallMapScene scene, SmallTileLibrary tiles, RebuildReport report)
    {
        TileBase verticalLine = tiles.Pick("Road_Marking", "line", "v") ?? tiles.Pick("Road_Marking", "line");
        TileBase horizontalLine = tiles.Pick("Road_Marking", "line", "h") ?? tiles.Pick("Road_Marking", "line");
        for (int y = 3; y < MapHeight; y += 6)
        {
            PaintCell(scene, report, "Road_Marking", verticalLine, 4, y);
            PaintCell(scene, report, "Road_Marking", verticalLine, 44, y);
            PaintCell(scene, report, "Road_Marking", verticalLine, 24, y);
        }

        for (int x = 2; x < MapWidth; x += 6)
        {
            PaintCell(scene, report, "Road_Marking", horizontalLine, x, 16);
            PaintCell(scene, report, "Road_Marking", horizontalLine, x, 40);
            PaintCell(scene, report, "Road_Marking", horizontalLine, x, 64);
        }

        int[] crosswalkYs = { 16, 40, 64 };
        foreach (int y in crosswalkYs)
        {
            PaintCrosswalk(scene, tiles, report, 8, y);
            PaintCrosswalk(scene, tiles, report, 39, y);
            PaintCrosswalk(scene, tiles, report, 24, y);
        }
    }

    private static void PaintCrosswalk(SmallMapScene scene, SmallTileLibrary tiles, RebuildReport report, int centerX, int centerY)
    {
        TileBase crosswalk = tiles.Pick("Road_Marking", "crosswalk") ?? tiles.Pick("Road_Marking", "line");
        for (int dx = -2; dx <= 2; dx++)
        {
            PaintCell(scene, report, "Road_Marking", crosswalk, centerX + dx, centerY - 2);
            PaintCell(scene, report, "Road_Marking", crosswalk, centerX + dx, centerY + 2);
        }
    }

    private static void PaintVerticalSidewalk(SmallMapScene scene, SmallTileLibrary tiles, RebuildReport report, int x)
    {
        TileBase sidewalk = tiles.Pick("Sidewalk", "sidewalk", "plaza");
        TileBase curb = tiles.Pick("Curb", "curb") ?? sidewalk;
        PaintRect(scene, report, "Sidewalk", sidewalk, x, 0, 1, MapHeight, null);
        PaintRect(scene, report, "Curb", curb, x, 0, 1, MapHeight, null);
    }

    private static void PaintHorizontalSidewalk(SmallMapScene scene, SmallTileLibrary tiles, RebuildReport report, int y)
    {
        TileBase sidewalk = tiles.Pick("Sidewalk", "sidewalk", "plaza");
        TileBase curb = tiles.Pick("Curb", "curb") ?? sidewalk;
        PaintRect(scene, report, "Sidewalk", sidewalk, 0, y, MapWidth, 1, null);
        PaintRect(scene, report, "Curb", curb, 0, y, MapWidth, 1, null);
    }

    private static void PlaceSmallMapProps(SmallMapScene scene, SmallPropLibrary props, SmallMapState state, RebuildReport report)
    {
        Vector2Int[] treePositions =
        {
            new Vector2Int(12, 10), new Vector2Int(18, 30), new Vector2Int(11, 55), new Vector2Int(18, 72),
            new Vector2Int(31, 10), new Vector2Int(37, 31), new Vector2Int(31, 56), new Vector2Int(36, 72)
        };

        foreach (Vector2Int position in treePositions)
        {
            PlaceProp(scene, props, state, report, position.x, position.y, "tree", "large", "medium");
        }

        Vector2Int[] townPropPositions =
        {
            new Vector2Int(9, 20), new Vector2Int(38, 20), new Vector2Int(9, 44), new Vector2Int(38, 44),
            new Vector2Int(9, 68), new Vector2Int(38, 68), new Vector2Int(20, 8), new Vector2Int(29, 9),
            new Vector2Int(20, 32), new Vector2Int(29, 34), new Vector2Int(20, 56), new Vector2Int(29, 57)
        };

        string[][] propTokenSets =
        {
            new[] { "lamp" },
            new[] { "bench" },
            new[] { "sign" },
            new[] { "mailbox" },
            new[] { "trash" },
            new[] { "bollard", "post" }
        };

        for (int i = 0; i < townPropPositions.Length; i++)
        {
            string[] tokens = propTokenSets[i % propTokenSets.Length];
            PlaceProp(scene, props, state, report, townPropPositions[i].x, townPropPositions[i].y, tokens);
        }
    }

    private static void PlaceProp(SmallMapScene scene, SmallPropLibrary props, SmallMapState state, RebuildReport report, int x, int y, params string[] tokens)
    {
        if (!IsInsideMap(x, y) || state.Road[x, y] || state.Occupied[x, y] || IsNearSpawn(x, y))
        {
            report.Warnings.Add($"Prop skipped at {x},{y}: blocked by road, occupancy, or spawn.");
            return;
        }

        GameObject prefab = props.Pick(tokens);
        if (prefab == null)
        {
            report.MissingCategories.Add($"Prop: {string.Join("/", tokens)}");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            report.Warnings.Add($"{prefab.name}: could not instantiate prop.");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");
        Undo.SetTransformParent(instance.transform, scene.PropsContainer, $"Parent {prefab.name}");
        instance.transform.position = new Vector3(x + 0.5f, y + 0.5f, instance.transform.position.z);
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        state.Occupied[x, y] = true;
        report.PropsPlaced++;
    }

    private static void ReserveBuildingLots(SmallMapState state, RebuildReport report)
    {
        string[] lotNames =
        {
            "lot_top_left",
            "lot_mid_left",
            "lot_bottom_left",
            "lot_top_right",
            "lot_mid_right",
            "lot_bottom_right"
        };

        foreach (string lotName in lotNames)
        {
            report.ReservedLots.Add(lotName);
        }
    }

    private static void PaintRect(SmallMapScene scene, RebuildReport report, string layerName, TileBase tile, int x, int y, int width, int height, bool[,] mask)
    {
        for (int px = x; px < x + width; px++)
        {
            for (int py = y; py < y + height; py++)
            {
                PaintCell(scene, report, layerName, tile, px, py);
                if (mask != null && IsInsideMap(px, py))
                {
                    mask[px, py] = true;
                }
            }
        }
    }

    private static void PaintCell(SmallMapScene scene, RebuildReport report, string layerName, TileBase tile, int x, int y)
    {
        if (tile == null || !IsInsideMap(x, y))
        {
            return;
        }

        if (!scene.Tilemaps.TryGetValue(layerName, out Tilemap tilemap))
        {
            report.Warnings.Add($"Layer missing: {layerName}");
            return;
        }

        Vector3Int position = new Vector3Int(x, y, 0);
        tilemap.SetTile(position, tile);
        report.RecordTilePainted(layerName, position);
    }

    private static void MovePlayerCameraAndCar(RebuildReport report)
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Move player to small map spawn");
            player.transform.position = new Vector3(24f, 8f, player.transform.position.z);
            report.PlayerSpawn = "x=24, y=8, z=0";
        }
        else
        {
            report.Warnings.Add("Player not found; spawn was not moved.");
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = GameObject.Find("Main Camera");
            camera = cameraObject == null ? null : cameraObject.GetComponent<Camera>();
        }

        if (camera != null)
        {
            Undo.RecordObject(camera.transform, "Move camera to small map spawn");
            camera.transform.position = new Vector3(24f, 8f, camera.transform.position.z);
        }
        else
        {
            report.Warnings.Add("Main Camera not found; camera was not moved.");
        }

        GameObject car = GameObject.Find("Car_Basic") ?? GameObject.Find("Car_Truck") ?? GameObject.Find("Car_Sport");
        if (car != null)
        {
            Undo.RecordObject(car.transform, "Move car to small map road");
            car.transform.position = new Vector3(4f, 12f, car.transform.position.z);
            report.CarPosition = $"{car.name}: x=4, y=12, z=0";
        }
        else
        {
            report.Warnings.Add("No scene car object found to move onto the left road.");
        }
    }

    private static void EnsureFolders()
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

        EnsureFolder("Assets/Docs");
        EnsureFolder("Assets/Scenes");
    }

    private static void WriteReport(RebuildReport report)
    {
        EnsureFolder("Assets/Docs");
        AddMissingCategoryWarnings(report);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# TEFO Small Map Rebuild Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Run mode: {report.Mode}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Backup scene path: {ValueOrNone(report.BackupScenePath)}");
        builder.AppendLine($"- Source pack root: {ValueOrNone(report.SourcePackRoot)}");
        builder.AppendLine($"- Manifest path: {ValueOrNone(report.ManifestPath)}");
        builder.AppendLine($"- Manifest entries loaded: {report.ManifestEntries}");
        builder.AppendLine($"- PNGs ingested: {report.PngsIngested}");
        builder.AppendLine($"- Import settings updated: {report.ImportSettingsUpdated}");
        builder.AppendLine($"- Tile assets created: {report.TileAssetsCreated}");
        builder.AppendLine($"- Tile assets updated: {report.TileAssetsUpdated}");
        builder.AppendLine($"- Prop prefabs created: {report.PropPrefabsCreated}");
        builder.AppendLine($"- Prop prefabs updated: {report.PropPrefabsUpdated}");
        builder.AppendLine($"- Old map objects deleted: {report.DeletedOldObjects.Count}");
        builder.AppendLine($"- Map layer stack created: {(report.MapLayerStackCreated ? "yes" : "no")}");
        builder.AppendLine($"- Props placed: {report.PropsPlaced}");
        builder.AppendLine($"- Player spawn position: {ValueOrNone(report.PlayerSpawn)}");
        builder.AppendLine($"- Car position: {ValueOrNone(report.CarPosition)}");
        builder.AppendLine();
        AppendCountMap(builder, "Tile Counts Per Layer", report.TilesPaintedByLayer.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.OrdinalIgnoreCase));
        AppendList(builder, "Deleted Old Map Objects", report.DeletedOldObjects);
        AppendList(builder, "Source PNG To Destination PNG", report.SourceToDestination);
        AppendList(builder, "Reserved Empty Lots", report.ReservedLots);
        AppendList(builder, "Missing Categories", report.MissingCategories);
        AppendList(builder, "Fallback Tiles Used", report.FallbackTilesUsed);
        AppendList(builder, "Warnings", report.Warnings);
        builder.AppendLine("## Next Manual Polish Steps");
        builder.AppendLine();
        builder.AppendLine("- Review the 48x80 `TEFO_Map` scene layout before saving over any production scene.");
        builder.AppendLine("- Add true Dirt, Stone, Curb, Road_Marking, and Decals tiles to the small pack if the report lists fallbacks.");
        builder.AppendLine("- Add building prefabs later if the reserved lots should become houses/shops.");
        builder.AppendLine("- Tune collision after playtesting player and vehicle movement.");
        builder.AppendLine();

        File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void AddMissingCategoryWarnings(RebuildReport report)
    {
        foreach (string category in TileCategories)
        {
            if (!Directory.Exists($"Assets/Tiles/{category}") || Directory.GetFiles($"Assets/Tiles/{category}", "*.asset", SearchOption.AllDirectories).Length == 0)
            {
                report.MissingCategories.Add($"Tile: {category}");
            }
        }

        foreach (string category in PropCategories)
        {
            if (!Directory.Exists($"Assets/Prefabs/Props/{category}") || Directory.GetFiles($"Assets/Prefabs/Props/{category}", "*.prefab", SearchOption.AllDirectories).Length == 0)
            {
                report.MissingCategories.Add($"Prop: {category}");
            }
        }
    }

    private static bool IsProtectedObject(GameObject gameObject)
    {
        if (ProtectedObjectNames.Contains(gameObject.name))
        {
            return true;
        }

        string lower = gameObject.name.ToLowerInvariant();
        return lower.Contains("weapon") || lower.Contains("player") || lower.Contains("car_");
    }

    private static bool ContainsProtectedDescendant(Transform transform)
    {
        foreach (Transform child in transform)
        {
            if (IsProtectedObject(child.gameObject) || ContainsProtectedDescendant(child))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        List<string> parts = new List<string>();
        while (transform != null)
        {
            parts.Add(transform.name);
            transform = transform.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void ResetTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
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

    private static string GetCategoryAfter(string path, string marker)
    {
        string normalized = NormalizePath(path);
        int markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return string.Empty;
        }

        string remainder = normalized.Substring(markerIndex + marker.Length);
        int slash = remainder.IndexOf('/');
        return slash < 0 ? string.Empty : remainder.Substring(0, slash);
    }

    private static bool IsInsideMap(int x, int y)
    {
        return x >= 0 && x < MapWidth && y >= 0 && y < MapHeight;
    }

    private static bool IsNearSpawn(int x, int y)
    {
        return Mathf.Abs(x - 24) <= 2 && Mathf.Abs(y - 8) <= 2;
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private static string Normalize(string text)
    {
        return NormalizePath(text).Replace("-", "_").Replace(" ", "_").ToLowerInvariant();
    }

    private static string NormalizeHeader(string header)
    {
        return Normalize(header).Trim('_');
    }

    private static string GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string value) ? value : string.Empty;
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

    private static string ValueOrNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value;
    }

    private static void AppendCountMap(StringBuilder builder, string title, Dictionary<string, int> counts)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        if (counts.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (KeyValuePair<string, int> pair in counts.OrderBy(pair => pair.Key))
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }
        }

        builder.AppendLine();
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

    private readonly struct SmallPack
    {
        public static readonly SmallPack Invalid = new SmallPack(string.Empty, string.Empty, string.Empty);

        public readonly string Root;
        public readonly string UnityReadyMapRoot;
        public readonly string ManifestPath;
        public bool IsValid => !string.IsNullOrEmpty(Root);

        public SmallPack(string root, string unityReadyMapRoot, string manifestPath)
        {
            Root = root;
            UnityReadyMapRoot = unityReadyMapRoot;
            ManifestPath = manifestPath;
        }
    }

    private readonly struct ManifestEntry
    {
        public readonly string Filename;
        public readonly string SourcePath;
        public readonly string SuggestedUnityFolder;
        public readonly string AssetType;

        public ManifestEntry(string filename, string sourcePath, string suggestedUnityFolder, string assetType)
        {
            Filename = filename;
            SourcePath = sourcePath;
            SuggestedUnityFolder = suggestedUnityFolder;
            AssetType = assetType;
        }
    }

    private readonly struct LayerSpec
    {
        public readonly string Name;
        public readonly int SortingOrder;
        public readonly bool HasCollider;
        public readonly bool Visible;

        public LayerSpec(string name, int sortingOrder, bool hasCollider, bool visible)
        {
            Name = name;
            SortingOrder = sortingOrder;
            HasCollider = hasCollider;
            Visible = visible;
        }
    }

    private readonly struct SmallMapScene
    {
        public readonly GameObject Root;
        public readonly Dictionary<string, Tilemap> Tilemaps;
        public readonly Transform PropsContainer;

        public SmallMapScene(GameObject root, Dictionary<string, Tilemap> tilemaps, Transform propsContainer)
        {
            Root = root;
            Tilemaps = tilemaps;
            PropsContainer = propsContainer;
        }
    }

    private sealed class SmallMapState
    {
        public readonly bool[,] Road = new bool[MapWidth, MapHeight];
        public readonly bool[,] Walkable = new bool[MapWidth, MapHeight];
        public readonly bool[,] Yard = new bool[MapWidth, MapHeight];
        public readonly bool[,] Occupied = new bool[MapWidth, MapHeight];
    }

    private sealed class SmallTileLibrary
    {
        private readonly Dictionary<string, List<TileEntry>> tilesByCategory;
        private readonly HashSet<string> preferredNames;
        private readonly RebuildReport report;

        private SmallTileLibrary(Dictionary<string, List<TileEntry>> tilesByCategory, HashSet<string> preferredNames, RebuildReport report)
        {
            this.tilesByCategory = tilesByCategory;
            this.preferredNames = preferredNames;
            this.report = report;
        }

        public static SmallTileLibrary Load(HashSet<string> preferredNames, RebuildReport report)
        {
            Dictionary<string, List<TileEntry>> tiles = new Dictionary<string, List<TileEntry>>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists("Assets/Tiles"))
            {
                foreach (string rawPath in Directory.GetFiles("Assets/Tiles", "*.asset", SearchOption.AllDirectories))
                {
                    string path = NormalizePath(rawPath);
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile == null)
                    {
                        continue;
                    }

                    string category = DetermineTileCategory(path);
                    if (!tiles.TryGetValue(category, out List<TileEntry> list))
                    {
                        list = new List<TileEntry>();
                        tiles[category] = list;
                    }

                    list.Add(new TileEntry(path, tile));
                }
            }

            return new SmallTileLibrary(tiles, preferredNames, report);
        }

        public TileBase Pick(string category, params string[] keywords)
        {
            List<TileEntry> candidates = Find(category, keywords);
            if (candidates.Count > 0)
            {
                return candidates[0].Tile;
            }

            string fallbackCategory = GetFallbackCategory(category);
            if (!string.IsNullOrEmpty(fallbackCategory) && !fallbackCategory.Equals(category, StringComparison.OrdinalIgnoreCase))
            {
                List<TileEntry> fallbacks = Find(fallbackCategory, new string[0]);
                if (fallbacks.Count > 0)
                {
                    report.FallbackTilesUsed.Add($"{category}: used {fallbacks[0].Path} from {fallbackCategory}.");
                    return fallbacks[0].Tile;
                }
            }

            report.MissingCategories.Add($"Tile: {category}");
            return null;
        }

        private List<TileEntry> Find(string category, string[] keywords)
        {
            if (!tilesByCategory.TryGetValue(category, out List<TileEntry> list))
            {
                return new List<TileEntry>();
            }

            IEnumerable<TileEntry> query = list;
            string[] normalizedKeywords = keywords == null ? new string[0] : keywords.Where(token => !string.IsNullOrWhiteSpace(token)).Select(Normalize).ToArray();
            if (normalizedKeywords.Length > 0)
            {
                List<TileEntry> matching = query.Where(entry => normalizedKeywords.Any(entry.SearchText.Contains)).ToList();
                if (matching.Count > 0)
                {
                    query = matching;
                }
            }

            return query
                .OrderByDescending(entry => preferredNames.Contains(entry.Name) ? 1000 : 0)
                .ThenByDescending(entry => entry.Score(normalizedKeywords))
                .ThenBy(entry => entry.Path)
                .ToList();
        }

        private static string GetFallbackCategory(string category)
        {
            switch (category)
            {
                case "Dirt":
                case "Stone":
                case "Curb":
                case "Decals":
                    return "Sidewalk";
                case "Road_Marking":
                    return "Road";
                default:
                    return "Grass";
            }
        }

        private static string DetermineTileCategory(string path)
        {
            string text = Normalize(path);
            if (text.Contains("road_marking") || text.Contains("crosswalk") || text.Contains("line") || text.Contains("dash"))
            {
                return "Road_Marking";
            }

            if (text.Contains("/stone/") || text.Contains("stone") || text.Contains("plaza"))
            {
                return "Stone";
            }

            if (text.Contains("/dirt/") || text.Contains("dirt"))
            {
                return "Dirt";
            }

            if (text.Contains("/sidewalk/") || text.Contains("sidewalk"))
            {
                return "Sidewalk";
            }

            if (text.Contains("/curb/") || text.Contains("curb"))
            {
                return "Curb";
            }

            if (text.Contains("/decals/") || text.Contains("decal"))
            {
                return "Decals";
            }

            if (text.Contains("/road/") || text.Contains("road"))
            {
                return "Road";
            }

            return "Grass";
        }
    }

    private sealed class SmallPropLibrary
    {
        private readonly List<PropEntry> props;
        private readonly HashSet<string> preferredNames;

        private SmallPropLibrary(List<PropEntry> props, HashSet<string> preferredNames)
        {
            this.props = props;
            this.preferredNames = preferredNames;
        }

        public static SmallPropLibrary Load(HashSet<string> preferredNames, RebuildReport report)
        {
            List<PropEntry> props = new List<PropEntry>();
            if (Directory.Exists("Assets/Prefabs/Props"))
            {
                foreach (string rawPath in Directory.GetFiles("Assets/Prefabs/Props", "*.prefab", SearchOption.AllDirectories))
                {
                    string path = NormalizePath(rawPath);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        props.Add(new PropEntry(path, prefab));
                    }
                }
            }

            return new SmallPropLibrary(props, preferredNames);
        }

        public GameObject Pick(params string[] keywords)
        {
            string[] normalizedKeywords = keywords == null ? new string[0] : keywords.Where(token => !string.IsNullOrWhiteSpace(token)).Select(Normalize).ToArray();
            List<PropEntry> candidates = props
                .Where(entry => normalizedKeywords.Length == 0 || normalizedKeywords.Any(entry.SearchText.Contains))
                .OrderByDescending(entry => preferredNames.Contains(entry.Name) ? 1000 : 0)
                .ThenByDescending(entry => entry.Score(normalizedKeywords))
                .ThenBy(entry => entry.Path)
                .ToList();

            return candidates.Count == 0 ? null : candidates[0].Prefab;
        }
    }

    private readonly struct TileEntry
    {
        public readonly string Path;
        public readonly string Name;
        public readonly string SearchText;
        public readonly TileBase Tile;

        public TileEntry(string path, TileBase tile)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
            SearchText = Normalize($"{path} {Name}");
            Tile = tile;
        }

        public int Score(IEnumerable<string> keywords)
        {
            string searchText = SearchText;
            return keywords.Count(keyword => searchText.Contains(keyword)) * 10;
        }
    }

    private readonly struct PropEntry
    {
        public readonly string Path;
        public readonly string Name;
        public readonly string SearchText;
        public readonly GameObject Prefab;

        public PropEntry(string path, GameObject prefab)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
            SearchText = Normalize($"{path} {Name}");
            Prefab = prefab;
        }

        public int Score(IEnumerable<string> keywords)
        {
            string searchText = SearchText;
            return keywords.Count(keyword => searchText.Contains(keyword)) * 10;
        }
    }

    private sealed class RebuildReport
    {
        public readonly string Mode;
        public string BackupScenePath;
        public string SourcePackRoot;
        public string ManifestPath;
        public int ManifestEntries;
        public int PngsIngested;
        public int ImportSettingsUpdated;
        public int TileAssetsCreated;
        public int TileAssetsUpdated;
        public int PropPrefabsCreated;
        public int PropPrefabsUpdated;
        public int PropsPlaced;
        public bool MapLayerStackCreated;
        public string PlayerSpawn;
        public string CarPosition;
        public readonly HashSet<string> SmallPackAssetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> SourceToDestination = new List<string>();
        public readonly List<string> DeletedOldObjects = new List<string>();
        public readonly List<string> ReservedLots = new List<string>();
        public readonly List<string> MissingCategories = new List<string>();
        public readonly List<string> FallbackTilesUsed = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly Dictionary<string, HashSet<Vector3Int>> TilesPaintedByLayer = new Dictionary<string, HashSet<Vector3Int>>(StringComparer.OrdinalIgnoreCase);

        public RebuildReport(string mode)
        {
            Mode = mode;
        }

        public void RecordTilePainted(string layerName, Vector3Int position)
        {
            if (!TilesPaintedByLayer.TryGetValue(layerName, out HashSet<Vector3Int> positions))
            {
                positions = new HashSet<Vector3Int>();
                TilesPaintedByLayer[layerName] = positions;
            }

            positions.Add(position);
        }
    }
}
#endif
