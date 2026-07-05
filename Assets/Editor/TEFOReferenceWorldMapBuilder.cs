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

public static class TEFOReferenceWorldMapBuilder
{
    private const int MapWidth = 128;
    private const int MapHeight = 128;
    private const int Ppu = 32;
    private const int Seed = 2507;
    private const string RootName = "TEFO_Map";
    private const string GridName = "Grid";
    private const string ReportPath = "Assets/Docs/REFERENCE_WORLD_MAP_REPORT.md";
    private const string BackupScenePath = "Assets/Scenes/MainScene_Backup_Before_ReferenceWorldMap.unity";

    private static readonly LayerSpec[] LayerSpecs =
    {
        new LayerSpec("Ground", -100, false, true),
        new LayerSpec("Grass", -90, false, true),
        new LayerSpec("Forest", -85, false, true),
        new LayerSpec("Dirt", -80, false, true),
        new LayerSpec("Farm", -75, false, true),
        new LayerSpec("Road", -70, false, true),
        new LayerSpec("Road_Marking", -60, false, true),
        new LayerSpec("Sidewalk", -50, false, true),
        new LayerSpec("Curb", -45, false, true),
        new LayerSpec("Sand", -40, false, true),
        new LayerSpec("Water", -95, false, true),
        new LayerSpec("Decals_Back", -20, false, true),
        new LayerSpec("Props_Back", 0, false, true),
        new LayerSpec("Buildings", 10, false, true),
        new LayerSpec("Props_Front", 35, false, true),
        new LayerSpec("Collision", 100, true, false)
    };

    private static readonly string[] MapOnlyObjectNames =
    {
        RootName,
        GridName,
        "Road_Test",
        "Road_Line_01",
        "Road_Line_02",
        "Road_Line_03",
        "Road_Line_04",
        "Road_Line_05",
        "TEFO_Demo_Generated_Props",
        "TEFO_Demo_Generated_Buildings",
        "TEFO_Demo_Generated_Metadata"
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

    private static readonly string[] TileRoots =
    {
        "Assets/Tiles",
        "Assets/Art/Map/Tiles"
    };

    private static readonly string[] PropRoots =
    {
        "Assets/Prefabs/Props",
        "Assets/Art/Map/Props"
    };

    private static readonly string[] BuildingRoots =
    {
        "Assets/Prefabs/Buildings",
        "Assets/Art/Map/Buildings"
    };

    [MenuItem("TEFO/Map/Reference World/Rebuild Reference World Map")]
    public static void RebuildReferenceWorldMap()
    {
        WorldReport report = new WorldReport();

        BackupCurrentScene(report);
        ClearOldMapOnly(report);

        WorldScene scene = CreateWorldLayerStack(report);
        AssetLibrary library = AssetLibrary.Load(report);
        WorldState state = new WorldState();
        MapContext context = new MapContext(scene, library, state, report, new System.Random(Seed));

        ReserveArea(context, 60, 51, 9, 9);
        PaintBaseWorld(context);
        PaintForestZone(context);
        PaintRuralVillage(context);
        PaintNatureTransition(context);
        PaintTown(context);
        PaintBeachAndOcean(context);
        PaintLighthouseArea(context);
        PaintDecals(context);

        PlaceForestProps(context);
        PlaceVillagePropsAndBuildings(context);
        PlaceTransitionProps(context);
        PlaceTownPropsAndBuildings(context);
        PlaceBeachProps(context);
        PlaceLighthouseProps(context);

        MovePlayerCameraAndCar(report);
        FinalizeCollision(context);
        FinishScene(scene, report);
    }

    private static void BackupCurrentScene(WorldReport report)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            StopBeforeMapDelete(report, "Active scene has no saved scene path; reference world rebuild stopped before deleting map objects.");
        }

        if (!EditorSceneManager.SaveScene(activeScene))
        {
            StopBeforeMapDelete(report, "Active scene could not be saved; reference world rebuild stopped before deleting map objects.");
        }

        EnsureFolder("Assets/Scenes");
        string backupPath = AssetDatabase.GenerateUniqueAssetPath(BackupScenePath);
        if (!AssetDatabase.CopyAsset(activeScene.path, backupPath))
        {
            StopBeforeMapDelete(report, $"Failed to create backup scene at {backupPath}; reference world rebuild stopped before deleting map objects.");
        }

        report.BackupScenePath = backupPath;
        AssetDatabase.ImportAsset(backupPath);
    }

    private static void StopBeforeMapDelete(WorldReport report, string message)
    {
        report.Warnings.Add(message);
        WriteReport(report);
        throw new InvalidOperationException(message);
    }

    private static void ClearOldMapOnly(WorldReport report)
    {
        foreach (string objectName in MapOnlyObjectNames)
        {
            GameObject found = GameObject.Find(objectName);
            if (found == null || ProtectedObjectNames.Contains(found.name))
            {
                continue;
            }

            report.DeletedMapObjects.Add(found.name);
            UnityEngine.Object.DestroyImmediate(found);
        }

        List<GameObject> generatedObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => !ProtectedObjectNames.Contains(go.name)
                && go.scene.IsValid()
                && (go.name.StartsWith("TEFO_Demo_Generated_", StringComparison.OrdinalIgnoreCase)
                    || go.name.StartsWith("TEFO_ReferenceWorld_", StringComparison.OrdinalIgnoreCase)
                    || go.name.IndexOf("Generated_Map", StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();

        foreach (GameObject generatedObject in generatedObjects)
        {
            report.DeletedMapObjects.Add(generatedObject.name);
            UnityEngine.Object.DestroyImmediate(generatedObject);
        }
    }

    private static WorldScene CreateWorldLayerStack(WorldReport report)
    {
        GameObject root = new GameObject(RootName);
        GameObject gridObject = new GameObject(GridName);
        gridObject.transform.SetParent(root.transform, false);
        ConfigureTransform(root.transform);
        ConfigureTransform(gridObject.transform);

        Grid grid = GetOrAddComponent<Grid>(gridObject);
        grid.cellSize = new Vector3(1f, 1f, 0f);
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        Dictionary<string, Tilemap> tilemaps = new Dictionary<string, Tilemap>(StringComparer.OrdinalIgnoreCase);
        foreach (LayerSpec spec in LayerSpecs)
        {
            GameObject layerObject = new GameObject(spec.Name);
            layerObject.transform.SetParent(gridObject.transform, false);
            ConfigureTransform(layerObject.transform);

            Tilemap tilemap = GetOrAddComponent<Tilemap>(layerObject);
            TilemapRenderer renderer = GetOrAddComponent<TilemapRenderer>(layerObject);
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            tilemap.color = spec.HasCollider ? new Color(1f, 0f, 0f, 0.25f) : Color.white;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = spec.SortingOrder;
            renderer.enabled = spec.RendererEnabled;

            if (spec.HasCollider)
            {
                TilemapCollider2D tilemapCollider = GetOrAddComponent<TilemapCollider2D>(layerObject);
                CompositeCollider2D compositeCollider = GetOrAddComponent<CompositeCollider2D>(layerObject);
                Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(layerObject);
                tilemapCollider.usedByComposite = true;
                compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
                body.bodyType = RigidbodyType2D.Static;
                body.simulated = true;
            }

            tilemaps[spec.Name] = tilemap;
        }

        report.MapLayerStackCreated = true;
        return new WorldScene(root, gridObject.transform, tilemaps);
    }

    private static void PaintBaseWorld(MapContext context)
    {
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                PaintCell(context, "Ground", PickTile(context, "Grass", "grass", "center", "lawn"), x, y);
                PaintCell(context, "Grass", PickRandomVariant(context, "Grass"), x, y);
            }
        }
    }

    private static void PaintForestZone(MapContext context)
    {
        for (int y = 0; y < MapHeight; y++)
        {
            int fadeBoundary = Mathf.RoundToInt(39f + Mathf.Sin(y * 0.11f) * 4.5f + Mathf.Sin(y * 0.37f) * 2.5f + (Noise01(3, y, 17) - 0.5f) * 7f);
            fadeBoundary = Mathf.Clamp(fadeBoundary, 34, 48);

            for (int x = 0; x <= fadeBoundary; x++)
            {
                bool pathOpening = DistanceToPolyline(x, y, new Vector2Int(3, 82), new Vector2Int(15, 75), new Vector2Int(28, 62), new Vector2Int(38, 57)) < 4f
                    || DistanceToPolyline(x, y, new Vector2Int(4, 35), new Vector2Int(14, 43), new Vector2Int(27, 49), new Vector2Int(39, 52)) < 3.5f;

                if (pathOpening && context.Random.NextDouble() < 0.62)
                {
                    continue;
                }

                string layerName = x < 35 || Noise01(x, y, 5) > 0.34f ? "Forest" : "Grass";
                PaintCell(context, layerName, PickRandomVariant(context, "Forest"), x, y);
            }
        }

        PaintWindingPath(context, 2, new Vector2Int(1, 82), new Vector2Int(16, 75), new Vector2Int(28, 62), new Vector2Int(38, 57), new Vector2Int(48, 56));
        PaintWindingPath(context, 2, new Vector2Int(2, 35), new Vector2Int(14, 43), new Vector2Int(27, 49), new Vector2Int(39, 52), new Vector2Int(52, 54));
        PaintWindingPath(context, 1, new Vector2Int(7, 12), new Vector2Int(17, 26), new Vector2Int(27, 40), new Vector2Int(28, 58));

        PaintIrregularBlob(context, "Grass", "Grass", 13, 78, 7, 5, false, false);
        PaintIrregularBlob(context, "Grass", "Grass", 32, 60, 6, 4, false, false);
        PaintIrregularBlob(context, "Forest", "Forest", 6, 112, 10, 7, false, false);
    }

    private static void PaintRuralVillage(MapContext context)
    {
        PaintIrregularBlob(context, "Dirt", "Dirt", 28, 58, 9, 8, false, true);
        PaintIrregularBlob(context, "Sidewalk", "Sidewalk", 28, 58, 4, 3, false, true);

        PaintWindingPath(context, 2, new Vector2Int(28, 58), new Vector2Int(18, 68), new Vector2Int(12, 80));
        PaintWindingPath(context, 2, new Vector2Int(28, 58), new Vector2Int(18, 46), new Vector2Int(14, 34));
        PaintWindingPath(context, 2, new Vector2Int(28, 58), new Vector2Int(39, 56), new Vector2Int(52, 55), new Vector2Int(68, 52));
        PaintWindingPath(context, 1, new Vector2Int(20, 25), new Vector2Int(26, 41), new Vector2Int(28, 58), new Vector2Int(33, 75));

        PaintFarmPlot(context, 8, 25, 14, 20);
        PaintFarmPlot(context, 12, 70, 18, 20);
        PaintFarmPlot(context, 32, 22, 16, 18);

        PaintNoiseVariation(context, 10, 55, 18, 95, "Grass", 95, 1);
    }

    private static void PaintNatureTransition(MapContext context)
    {
        for (int x = 45; x <= 68; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (Noise01(x, y, 43) > 0.68f)
                {
                    PaintCell(context, "Grass", PickTile(context, "Grass", "flower", "dense", "tuft"), x, y);
                }
            }
        }

        PaintIrregularBlob(context, "Water", "Water", 53, 36, 7, 5, true, false);
        PaintIrregularBlob(context, "Water", "Water", 58, 18, 6, 4, true, false);
        PaintIrregularBlob(context, "Dirt", "Dirt", 55, 34, 8, 1, false, true);
        PaintIrregularBlob(context, "Dirt", "Dirt", 58, 20, 5, 1, false, true);

        PaintWindingPath(context, 2, new Vector2Int(48, 56), new Vector2Int(58, 55), new Vector2Int(68, 52), new Vector2Int(72, 52));
        PaintWindingPath(context, 1, new Vector2Int(45, 25), new Vector2Int(53, 36), new Vector2Int(62, 42), new Vector2Int(68, 48));
        PaintNoiseVariation(context, 45, 68, 0, 127, "Grass", 120, 1);
    }

    private static void PaintTown(MapContext context)
    {
        PaintRect(context, "Grass", PickRandomVariant(context, "Grass"), 68, 8, 45, 113, null);

        PaintRoadRect(context, 72, 0, 7, 128, true);
        PaintRoadRect(context, 94, 0, 7, 128, true);
        PaintRoadRect(context, 68, 22, 45, 7, false);
        PaintRoadRect(context, 68, 48, 45, 7, false);
        PaintRoadRect(context, 68, 74, 45, 7, false);
        PaintRoadRect(context, 68, 100, 45, 7, false);

        PaintSidewalkAroundRoad(context, 72, 0, 7, 128);
        PaintSidewalkAroundRoad(context, 94, 0, 7, 128);
        PaintSidewalkAroundRoad(context, 68, 22, 45, 7);
        PaintSidewalkAroundRoad(context, 68, 48, 45, 7);
        PaintSidewalkAroundRoad(context, 68, 74, 45, 7);
        PaintSidewalkAroundRoad(context, 68, 100, 45, 7);

        PaintRoadMarkings(context);
        PaintParkingLot(context, 72, 30, 16, 14);
        PaintTownPlaza(context, 84, 12, 12, 12);

        PaintRect(context, "Grass", PickTile(context, "Grass", "lawn", "center"), 80, 30, 12, 14, null);
        PaintRect(context, "Grass", PickTile(context, "Grass", "lawn", "center"), 102, 30, 9, 15, null);
        PaintRect(context, "Grass", PickTile(context, "Grass", "lawn", "center"), 80, 56, 12, 16, null);
        PaintRect(context, "Grass", PickTile(context, "Grass", "lawn", "center"), 102, 56, 9, 16, null);
        PaintRect(context, "Grass", PickTile(context, "Grass", "lawn", "center"), 80, 82, 12, 16, null);
        PaintRect(context, "Grass", PickTile(context, "Grass", "lawn", "center"), 102, 82, 9, 16, null);
        PaintNoiseVariation(context, 68, 112, 8, 120, "Grass", 70, 1);
    }

    private static void PaintBeachAndOcean(MapContext context)
    {
        for (int y = 0; y < MapHeight; y++)
        {
            int shoreX = Mathf.RoundToInt(121f + Mathf.Sin(y * 0.10f) * 1.9f + Mathf.Sin(y * 0.31f) * 1.1f + (Noise01(121, y, 71) - 0.5f) * 2.5f);
            shoreX = Mathf.Clamp(shoreX, 119, 123);

            for (int x = 112; x < shoreX; x++)
            {
                PaintCell(context, "Sand", PickRandomVariant(context, "Sand"), x, y);
                context.State.Sand[x, y] = true;
            }

            for (int x = shoreX; x < MapWidth; x++)
            {
                PaintCell(context, "Water", PickTile(context, "Water", x == shoreX ? new[] { "shore", "foam", "water" } : new[] { "ocean", "sea", "water" }), x, y);
                context.State.Water[x, y] = true;
            }

            PaintCell(context, "Decals_Back", PickTile(context, "Water", "foam", "wave", "shore"), shoreX - 1, y);
        }

        PaintWindingPath(context, 1, new Vector2Int(109, 104), new Vector2Int(115, 91), new Vector2Int(116, 70), new Vector2Int(115, 45), new Vector2Int(113, 20));
    }

    private static void PaintLighthouseArea(MapContext context)
    {
        PaintIrregularBlob(context, "Grass", "Grass", 113, 10, 7, 5, false, false);
        PaintIrregularBlob(context, "Sand", "Sand", 116, 8, 7, 4, false, false);
        PaintWindingPath(context, 1, new Vector2Int(99, 17), new Vector2Int(106, 14), new Vector2Int(113, 10));
    }

    private static void PaintDecals(MapContext context)
    {
        for (int i = 0; i < 340; i++)
        {
            int x = context.Random.Next(0, MapWidth);
            int y = context.Random.Next(0, MapHeight);
            if (AvoidRoadWaterAndSpawn(context, x, y))
            {
                continue;
            }

            PaintCell(context, "Decals_Back", PickRandomVariant(context, "Decals"), x, y);
        }
    }

    private static void PaintFarmPlot(MapContext context, int x, int y, int width, int height)
    {
        PaintIrregularBlob(context, "Dirt", "Dirt", x + width / 2, y + height / 2, width / 2 + 1, height / 2 + 1, false, false);
        PaintRect(context, "Farm", PickRandomVariant(context, "Farm"), x + 1, y + 1, width - 2, height - 2, null);
        ReserveArea(context, x, y, width, height);

        for (int px = x; px < x + width; px++)
        {
            PlacePrefabSafe(context, "Props_Back", "fence", px, y, 1, 1, false);
            PlacePrefabSafe(context, "Props_Back", "fence", px, y + height - 1, 1, 1, false);
        }

        for (int py = y + 1; py < y + height - 1; py++)
        {
            PlacePrefabSafe(context, "Props_Back", "fence", x, py, 1, 1, false);
            PlacePrefabSafe(context, "Props_Back", "fence", x + width - 1, py, 1, 1, false);
        }
    }

    private static void PaintParkingLot(MapContext context, int x, int y, int width, int height)
    {
        PaintRect(context, "Road", PickRandomVariant(context, "Road"), x, y, width, height, context.State.Road);

        for (int px = x + 2; px < x + width - 1; px += 3)
        {
            PaintLine(context, "Road_Marking", PickTile(context, "Road_Marking", "parking", "line"), px, y + 1, px, y + height - 2);
        }

        PaintLine(context, "Road_Marking", PickTile(context, "Road_Marking", "stop", "line"), x, y, x + width - 1, y);
    }

    private static void PaintTownPlaza(MapContext context, int x, int y, int width, int height)
    {
        PaintRect(context, "Sidewalk", PickTile(context, "Sidewalk", "plaza", "pavement", "stone"), x, y, width, height, null);
        PaintIrregularBlob(context, "Grass", "Grass", x + width / 2, y + height / 2, 3, 3, false, false);
    }

    private static void PaintNoiseVariation(MapContext context, int minX, int maxX, int minY, int maxY, string category, int attempts, int radius)
    {
        for (int i = 0; i < attempts; i++)
        {
            int x = context.Random.Next(minX, maxX + 1);
            int y = context.Random.Next(minY, maxY + 1);
            if (!IsInsideMap(x, y) || context.State.Road[x, y] || context.State.Water[x, y])
            {
                continue;
            }

            PaintIrregularBlob(context, "Grass", category, x, y, radius + context.Random.Next(0, 2), radius + context.Random.Next(0, 2), false, false);
        }
    }

    private static void PaintRect(MapContext context, string layerName, TileBase tile, int x, int y, int width, int height, bool[,] mask)
    {
        for (int px = x; px < x + width; px++)
        {
            for (int py = y; py < y + height; py++)
            {
                PaintCell(context, layerName, tile, px, py);
                if (mask != null && IsInsideMap(px, py))
                {
                    mask[px, py] = true;
                }
            }
        }
    }

    private static void PaintIrregularBlob(MapContext context, string layerName, string tileCategory, int centerX, int centerY, int radiusX, int radiusY, bool markWater, bool markPath)
    {
        for (int x = centerX - radiusX - 2; x <= centerX + radiusX + 2; x++)
        {
            for (int y = centerY - radiusY - 2; y <= centerY + radiusY + 2; y++)
            {
                if (!IsInsideMap(x, y))
                {
                    continue;
                }

                float nx = (x - centerX) / Mathf.Max(1f, radiusX);
                float ny = (y - centerY) / Mathf.Max(1f, radiusY);
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = 1f + (Noise01(x, y, centerX + centerY) - 0.5f) * 0.42f;
                if (distance > edge)
                {
                    continue;
                }

                PaintCell(context, layerName, PickRandomVariant(context, tileCategory), x, y);
                if (markWater)
                {
                    context.State.Water[x, y] = true;
                }

                if (markPath)
                {
                    context.State.Path[x, y] = true;
                }
            }
        }
    }

    private static void PaintWindingPath(MapContext context, int radius, params Vector2Int[] points)
    {
        int stepIndex = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[i + 1];
            Vector2 direction = end - start;
            Vector2 normal = direction.sqrMagnitude > 0.01f ? new Vector2(-direction.y, direction.x).normalized : Vector2.up;
            int steps = Mathf.Max(1, Mathf.RoundToInt(direction.magnitude * 3f));

            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                float wobble = Mathf.Sin((stepIndex + Seed) * 0.25f) * 1.15f + Mathf.Sin((stepIndex + Seed) * 0.09f) * 0.65f;
                Vector2 point = Vector2.Lerp(start, end, t) + normal * wobble;
                int x = Mathf.RoundToInt(point.x);
                int y = Mathf.RoundToInt(point.y);
                PaintBrush(context, "Dirt", x, y, radius + (Noise01(x, y, 23) > 0.74f ? 1 : 0), "Dirt", false, true);
                stepIndex++;
            }
        }
    }

    private static void PaintRoadRect(MapContext context, int x, int y, int width, int height, bool vertical)
    {
        PaintRect(context, "Road", PickRandomVariant(context, "Road"), x, y, width, height, context.State.Road);
        int center = vertical ? x + width / 2 : y + height / 2;

        if (vertical)
        {
            for (int py = y + 2; py < y + height - 2; py += 6)
            {
                PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "line", "dash", "v"), center, py);
            }
        }
        else
        {
            for (int px = x + 2; px < x + width - 2; px += 6)
            {
                PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "line", "dash", "h"), px, center);
            }
        }
    }

    private static void PaintSidewalkAroundRoad(MapContext context, int x, int y, int width, int height)
    {
        TileBase sidewalk = PickRandomVariant(context, "Sidewalk");
        TileBase curb = PickRandomVariant(context, "Curb");
        PaintRect(context, "Sidewalk", sidewalk, x - 2, y - 2, width + 4, 1, null);
        PaintRect(context, "Sidewalk", sidewalk, x - 2, y + height + 1, width + 4, 1, null);
        PaintRect(context, "Sidewalk", sidewalk, x - 2, y - 1, 1, height + 2, null);
        PaintRect(context, "Sidewalk", sidewalk, x + width + 1, y - 1, 1, height + 2, null);
        PaintRect(context, "Curb", curb, x - 1, y - 1, width + 2, 1, null);
        PaintRect(context, "Curb", curb, x - 1, y + height, width + 2, 1, null);
        PaintRect(context, "Curb", curb, x - 1, y, 1, height, null);
        PaintRect(context, "Curb", curb, x + width, y, 1, height, null);
    }

    private static void PaintCrosswalk(MapContext context, int centerX, int centerY)
    {
        for (int dx = -4; dx <= 4; dx++)
        {
            PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "crosswalk", "line"), centerX + dx, centerY - 3);
            PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "crosswalk", "line"), centerX + dx, centerY + 3);
        }

        for (int dy = -4; dy <= 4; dy++)
        {
            PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "crosswalk", "line"), centerX - 3, centerY + dy);
            PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "crosswalk", "line"), centerX + 3, centerY + dy);
        }
    }

    private static void PaintRoadMarkings(MapContext context)
    {
        int[] verticalCenters = { 75, 97 };
        int[] horizontalCenters = { 25, 51, 77, 103 };

        foreach (int x in verticalCenters)
        {
            for (int y = 4; y < MapHeight - 4; y += 6)
            {
                PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "dash", "line"), x, y);
            }
        }

        foreach (int y in horizontalCenters)
        {
            for (int x = 70; x <= 110; x += 6)
            {
                PaintCell(context, "Road_Marking", PickTile(context, "Road_Marking", "dash", "line"), x, y);
            }
        }

        foreach (int x in verticalCenters)
        {
            foreach (int y in horizontalCenters)
            {
                PaintCrosswalk(context, x, y);
            }
        }
    }

    private static void PaintLine(MapContext context, string layerName, TileBase tile, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Sign(x1 - x0);
        int dy = Math.Sign(y1 - y0);
        int x = x0;
        int y = y0;

        PaintCell(context, layerName, tile, x, y);
        while (x != x1 || y != y1)
        {
            if (x != x1)
            {
                x += dx;
            }

            if (y != y1)
            {
                y += dy;
            }

            PaintCell(context, layerName, tile, x, y);
        }
    }

    private static void PaintBrush(MapContext context, string layerName, int centerX, int centerY, int radius, string tileCategory, bool markWater, bool markPath)
    {
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (!IsInsideMap(x, y) || Vector2Int.Distance(new Vector2Int(x, y), new Vector2Int(centerX, centerY)) > radius + 0.45f)
                {
                    continue;
                }

                PaintCell(context, layerName, PickRandomVariant(context, tileCategory), x, y);
                if (markWater)
                {
                    context.State.Water[x, y] = true;
                }

                if (markPath)
                {
                    context.State.Path[x, y] = true;
                }
            }
        }
    }

    private static void PaintCell(MapContext context, string layerName, TileBase tile, int x, int y)
    {
        if (tile == null || !IsInsideMap(x, y) || !context.Scene.Tilemaps.TryGetValue(layerName, out Tilemap tilemap))
        {
            return;
        }

        tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        context.Report.CountTile(layerName, tile.name);
    }

    private static void PlaceForestProps(MapContext context)
    {
        for (int i = 0; i < 420; i++)
        {
            int x = context.Random.Next(0, 45);
            int y = context.Random.Next(0, MapHeight);
            if (context.State.Path[x, y] || context.Random.NextDouble() < x / 80f)
            {
                continue;
            }

            string token = context.Random.NextDouble() < 0.7 ? "tree" : context.Random.NextDouble() < 0.55 ? "bush" : "rock";
            PlacePrefabSafe(context, context.Random.NextDouble() < 0.55 ? "Props_Front" : "Props_Back", token, x, y, 1, 1, true);
        }
    }

    private static void PlaceVillagePropsAndBuildings(MapContext context)
    {
        PlacePrefabSafe(context, "Props_Back", "well", 28, 58, 2, 2, true);
        PlacePrefabSafe(context, "Props_Back", "fountain", 28, 58, 2, 2, true);
        PlacePrefabSafe(context, "Props_Back", "statue", 28, 58, 2, 2, true);

        Vector2Int[] houseSpots =
        {
            new Vector2Int(18, 57),
            new Vector2Int(22, 65),
            new Vector2Int(33, 63),
            new Vector2Int(38, 54),
            new Vector2Int(24, 49),
            new Vector2Int(42, 43),
            new Vector2Int(16, 88),
            new Vector2Int(39, 33)
        };

        foreach (Vector2Int spot in houseSpots)
        {
            PlacePrefabSafe(context, "Buildings", "house", spot.x, spot.y, 4, 4, true);
        }

        for (int i = 0; i < 120; i++)
        {
            int x = context.Random.Next(10, 56);
            int y = context.Random.Next(18, 96);
            if (AvoidRoadWaterAndSpawn(context, x, y))
            {
                continue;
            }

            string token = context.Random.NextDouble() < 0.45 ? "bush" : context.Random.NextDouble() < 0.6 ? "rock" : "grass";
            PlacePrefabSafe(context, "Props_Back", token, x, y, 1, 1, true);
        }
    }

    private static void PlaceTransitionProps(MapContext context)
    {
        PlacePrefabSafe(context, "Props_Back", "bridge", 55, 34, 3, 2, true);
        PlacePrefabSafe(context, "Props_Back", "dock", 58, 20, 3, 2, true);

        for (int i = 0; i < 95; i++)
        {
            int x = context.Random.Next(45, 69);
            int y = context.Random.Next(0, MapHeight);
            if (AvoidRoadWaterAndSpawn(context, x, y) || context.Random.NextDouble() < 0.28)
            {
                continue;
            }

            string token = context.Random.NextDouble() < 0.34 ? "tree" : context.Random.NextDouble() < 0.55 ? "rock" : "bush";
            PlacePrefabSafe(context, "Props_Back", token, x, y, 1, 1, true);
        }
    }

    private static void PlaceTownPropsAndBuildings(MapContext context)
    {
        Vector2Int[] buildingSpots =
        {
            new Vector2Int(82, 58),
            new Vector2Int(106, 59),
            new Vector2Int(82, 86),
            new Vector2Int(106, 86),
            new Vector2Int(83, 108),
            new Vector2Int(104, 108),
            new Vector2Int(107, 35),
            new Vector2Int(82, 37)
        };

        string[] buildingTokens = { "shop", "store", "town", "house", "garage" };
        for (int i = 0; i < buildingSpots.Length; i++)
        {
            Vector2Int spot = buildingSpots[i];
            PlacePrefabSafe(context, "Buildings", buildingTokens[i % buildingTokens.Length], spot.x, spot.y, 5, 5, true);
        }

        PlacePrefabSafe(context, "Props_Back", "fountain", 90, 18, 2, 2, true);
        PlacePrefabSafe(context, "Props_Back", "statue", 90, 18, 2, 2, true);

        for (int y = 12; y <= 116; y += 8)
        {
            PlacePrefabSafe(context, "Props_Back", "lamp", 70, y, 1, 1, true);
            PlacePrefabSafe(context, "Props_Back", "lamp", 102, y, 1, 1, true);
        }

        for (int x = 72; x <= 110; x += 9)
        {
            PlacePrefabSafe(context, "Props_Back", "sign", x, 30, 1, 1, true);
            PlacePrefabSafe(context, "Props_Back", "bench", x, 18, 1, 1, true);
        }

        for (int i = 0; i < 80; i++)
        {
            int x = context.Random.Next(68, 113);
            int y = context.Random.Next(8, 121);
            if (AvoidRoadWaterAndSpawn(context, x, y))
            {
                continue;
            }

            string token = context.Random.NextDouble() < 0.5 ? "crate" : context.Random.NextDouble() < 0.5 ? "trash" : "mailbox";
            PlacePrefabSafe(context, "Props_Back", token, x, y, 1, 1, true);
        }
    }

    private static void PlaceBeachProps(MapContext context)
    {
        for (int i = 0; i < 90; i++)
        {
            int x = context.Random.Next(112, 122);
            int y = context.Random.Next(0, MapHeight);
            if (!IsInsideMap(x, y) || context.State.Water[x, y] || context.State.Occupied[x, y])
            {
                continue;
            }

            string token = context.Random.NextDouble() < 0.35 ? "palm" : context.Random.NextDouble() < 0.45 ? "umbrella" : context.Random.NextDouble() < 0.65 ? "chair" : "rock";
            PlacePrefabSafe(context, "Props_Front", token, x, y, 1, 1, true);
        }

        PlacePrefabSafe(context, "Props_Back", "dock", 121, 64, 4, 2, true);
        PlacePrefabSafe(context, "Props_Back", "beach_hut", 114, 86, 3, 3, true);
    }

    private static void PlaceLighthouseProps(MapContext context)
    {
        bool placed = PlacePrefabSafe(context, "Buildings", "lighthouse", 115, 10, 5, 5, true);
        if (!placed)
        {
            placed = PlacePrefabSafe(context, "Buildings", "landmark", 115, 10, 5, 5, true);
        }

        if (!placed)
        {
            PlacePrefabSafe(context, "Buildings", "tower", 115, 10, 5, 5, true);
        }

        for (int i = 0; i < 26; i++)
        {
            int x = context.Random.Next(108, 124);
            int y = context.Random.Next(0, 19);
            if (!IsInsideMap(x, y) || context.State.Water[x, y])
            {
                continue;
            }

            PlacePrefabSafe(context, "Props_Back", context.Random.NextDouble() < 0.55 ? "rock" : "fence", x, y, 1, 1, true);
        }
    }

    private static bool PlacePrefabSafe(MapContext context, string layerName, string keyword, int x, int y, int width, int height, bool reportMissing)
    {
        if (!IsAreaClear(context, x, y, width, height))
        {
            return false;
        }

        GameObject prefab = context.Library.PickPrefab(keyword, context.Random);
        if (prefab == null)
        {
            if (reportMissing)
            {
                context.Report.AddMissingPrefab(keyword);
            }

            return false;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            return false;
        }

        Transform parent = context.Scene.GridTransform.Find(layerName);
        instance.transform.SetParent(parent != null ? parent : context.Scene.Root.transform, false);
        instance.transform.position = new Vector3(x + width * 0.5f, y + height * 0.5f, 0f);
        instance.name = $"ReferenceWorld_{prefab.name}_{x}_{y}";
        ReserveArea(context, x, y, width, height);
        context.Report.CountPlacedPrefab(keyword, prefab.name, layerName == "Buildings");
        return true;
    }

    private static bool IsAreaClear(MapContext context, int x, int y, int width, int height)
    {
        for (int px = x; px < x + width; px++)
        {
            for (int py = y; py < y + height; py++)
            {
                if (!IsInsideMap(px, py)
                    || context.State.Occupied[px, py]
                    || context.State.Water[px, py]
                    || context.State.Road[px, py]
                    || IsNearSpawn(px, py, 4))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ReserveArea(MapContext context, int x, int y, int width, int height)
    {
        for (int px = x; px < x + width; px++)
        {
            for (int py = y; py < y + height; py++)
            {
                if (IsInsideMap(px, py))
                {
                    context.State.Occupied[px, py] = true;
                }
            }
        }
    }

    private static bool AvoidRoadWaterAndSpawn(MapContext context, int x, int y)
    {
        return !IsInsideMap(x, y) || context.State.Road[x, y] || context.State.Water[x, y] || context.State.Occupied[x, y] || IsNearSpawn(x, y, 5);
    }

    private static void FinalizeCollision(MapContext context)
    {
        TileBase collisionTile = PickTile(context, "Collision", "collision") ?? PickRandomVariant(context, "Water");
        if (collisionTile == null)
        {
            context.Report.Warnings.Add("Collision tilemap could not be painted because no Collision or Water fallback tile was found.");
            return;
        }

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                bool blocks = context.State.Water[x, y] || (context.State.Occupied[x, y] && !context.State.Path[x, y] && !context.State.Road[x, y]);
                if (blocks && !IsNearSpawn(x, y, 4))
                {
                    PaintCell(context, "Collision", collisionTile, x, y);
                }
            }
        }
    }

    private static void MovePlayerCameraAndCar(WorldReport report)
    {
        MoveNamedObject("Player", new Vector3(64f, 55f, 0f), report);
        MoveNamedObject("PlayerSpawnPoint", new Vector3(64f, 55f, 0f), report);

        GameObject cameraObject = GameObject.Find("Main Camera");
        if (cameraObject != null)
        {
            cameraObject.transform.position = new Vector3(64f, 55f, cameraObject.transform.position.z);
            report.CameraPosition = cameraObject.transform.position;
        }

        GameObject car = GameObject.Find("Car_Basic") ?? GameObject.Find("Car_Truck") ?? GameObject.Find("Car_Sport");
        if (car != null)
        {
            car.transform.position = new Vector3(76f, 50f, 0f);
            report.CarMoved = $"{car.name} -> (76, 50, 0)";
        }
        else
        {
            report.Warnings.Add("No car object found to move to the town road.");
        }

        report.PlayerSpawnPosition = new Vector3(64f, 55f, 0f);
    }

    private static void MoveNamedObject(string objectName, Vector3 position, WorldReport report)
    {
        GameObject found = GameObject.Find(objectName);
        if (found == null)
        {
            report.Warnings.Add($"{objectName} was not found; it was not moved.");
            return;
        }

        found.transform.position = position;
    }

    private static void FinishScene(WorldScene scene, WorldReport report)
    {
        foreach (Tilemap tilemap in scene.Tilemaps.Values)
        {
            tilemap.CompressBounds();
            EditorUtility.SetDirty(tilemap);
        }

        Selection.activeGameObject = scene.Root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        WriteReport(report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"TEFO reference world map builder finished. Report: {ReportPath}");
    }

    private static Dictionary<string, List<TileEntry>> LoadTilesByCategory(WorldReport report)
    {
        Dictionary<string, List<TileEntry>> tiles = new Dictionary<string, List<TileEntry>>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets(string.Empty, ExistingRoots(TileRoots));

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile == null)
            {
                continue;
            }

            string category = CategorizeTile(path);
            if (!tiles.TryGetValue(category, out List<TileEntry> entries))
            {
                entries = new List<TileEntry>();
                tiles[category] = entries;
            }

            entries.Add(new TileEntry(tile, path, NormalizeSearchText(path)));
        }

        foreach (KeyValuePair<string, List<TileEntry>> pair in tiles)
        {
            report.TileCategoriesFound[pair.Key] = pair.Value.Count;
        }

        return tiles;
    }

    private static TileBase PickTile(MapContext context, string category, params string[] keywords)
    {
        return context.Library.PickTile(category, keywords, context.Random, context.Report);
    }

    private static TileBase PickRandomVariant(MapContext context, string category)
    {
        return context.Library.PickTile(category, Array.Empty<string>(), context.Random, context.Report);
    }

    private static void WriteReport(WorldReport report)
    {
        EnsureFolder("Assets/Docs");
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# TEFO Reference World Map Report");
        builder.AppendLine();
        builder.AppendLine($"- Backup scene path: `{report.BackupScenePath}`");
        builder.AppendLine($"- Map size: {MapWidth}x{MapHeight} tiles");
        builder.AppendLine($"- Tile size: 32x32");
        builder.AppendLine($"- PPU: {Ppu}");
        builder.AppendLine($"- Seed: {Seed}");
        builder.AppendLine($"- Player spawn position: {FormatVector(report.PlayerSpawnPosition)}");
        builder.AppendLine($"- Main Camera position: {FormatVector(report.CameraPosition)}");
        builder.AppendLine($"- Car moved: {report.CarMoved}");
        builder.AppendLine($"- Map layer stack created: {(report.MapLayerStackCreated ? "yes" : "no")}");
        builder.AppendLine();
        AppendCountMap(builder, "Tile Categories Found", report.TileCategoriesFound);
        AppendCountMap(builder, "Prefab Categories Found", report.PrefabCategoriesFound);
        AppendList(builder, "Missing Categories", report.MissingCategories.OrderBy(value => value));
        AppendList(builder, "Fallback Tiles Used", report.FallbackTilesUsed);
        AppendCountMap(builder, "Tile Counts Per Layer", report.TileCountsByLayer);
        AppendCountMap(builder, "Props Placed By Category", report.PropsPlacedByCategory);
        AppendCountMap(builder, "Buildings Placed By Category", report.BuildingsPlacedByCategory);
        AppendList(builder, "Deleted Old Map Objects", report.DeletedMapObjects);
        AppendList(builder, "Warnings", report.Warnings);
        builder.AppendLine("## Manual Polish Suggestions");
        builder.AppendLine("- Review the beach shoreline and replace any generic water/foam fallback tiles with hand-picked shore variants if your tileset has them.");
        builder.AppendLine("- Check lighthouse availability; if no lighthouse prefab exists, the builder falls back to landmark/tower-like building tokens.");
        builder.AppendLine("- Inspect town building lots for sprite footprint differences and nudge individual prefabs if a large art asset extends farther than its reserved tile area.");
        builder.AppendLine("- Add hand-authored bridge/dock prefabs around the ponds if the fallback token did not find one.");
        File.WriteAllText(ReportPath, builder.ToString());
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void AppendCountMap(StringBuilder builder, string title, Dictionary<string, int> counts)
    {
        builder.AppendLine($"## {title}");
        if (counts.Count == 0)
        {
            builder.AppendLine("- None");
            builder.AppendLine();
            return;
        }

        foreach (KeyValuePair<string, int> pair in counts.OrderBy(pair => pair.Key))
        {
            builder.AppendLine($"- {pair.Key}: {pair.Value}");
        }

        builder.AppendLine();
    }

    private static void AppendList(StringBuilder builder, string title, IEnumerable<string> values)
    {
        builder.AppendLine($"## {title}");
        List<string> items = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
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

    private static string CategorizeTile(string path)
    {
        string text = NormalizeSearchText(path);
        if (HasAny(text, "collision")) return "Collision";
        if (HasAny(text, "grass", "lawn", "meadow")) return "Grass";
        if (HasAny(text, "forest", "tree_floor", "dense", "woods")) return "Forest";
        if (HasAny(text, "dirt", "path", "trail", "soil")) return "Dirt";
        if (HasAny(text, "farm", "crop", "field", "hay")) return "Farm";
        if (HasAny(text, "marking", "crosswalk", "parking", "stop", "dash", "line")) return "Road_Marking";
        if (HasAny(text, "road", "asphalt", "street")) return "Road";
        if (HasAny(text, "sidewalk", "plaza", "pavement", "stone")) return "Sidewalk";
        if (HasAny(text, "curb", "kerb", "edge")) return "Curb";
        if (HasAny(text, "sand", "beach")) return "Sand";
        if (HasAny(text, "water", "ocean", "sea", "shore", "foam", "pond")) return "Water";
        if (HasAny(text, "crack", "leaf", "stain", "pebble", "dirt_patch", "moss", "decal")) return "Decals";
        return "Grass";
    }

    private static string CategorizePrefab(string path, bool building)
    {
        string text = NormalizeSearchText(path);
        if (building || HasAny(text, "house", "village", "town", "shop", "store", "garage", "lighthouse", "landmark", "tower"))
        {
            return "Buildings";
        }

        if (HasAny(text, "palm", "umbrella", "beach", "chair", "dock")) return "Beach";
        if (HasAny(text, "farm", "crop", "hay", "well")) return "Farm";
        if (HasAny(text, "lamp", "sign", "bench", "mailbox", "trash", "cone", "barrel", "crate", "tire", "fence", "fountain", "statue")) return "Town";
        if (HasAny(text, "tree", "bush", "rock", "stump", "log", "grass", "flower")) return "Nature";
        return "Props";
    }

    private static string[] ExistingRoots(IEnumerable<string> roots)
    {
        return roots.Where(AssetDatabase.IsValidFolder).ToArray();
    }

    private static bool HasAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string NormalizeSearchText(string value)
    {
        return value.Replace('\\', '/').Replace('-', '_').ToLowerInvariant();
    }

    private static float Noise01(int x, int y, int salt)
    {
        unchecked
        {
            int n = x * 73856093 ^ y * 19349663 ^ salt * 83492791 ^ Seed;
            n = (n << 13) ^ n;
            int value = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
            return value / 2147483647f;
        }
    }

    private static float DistanceToPolyline(int x, int y, params Vector2Int[] points)
    {
        float best = float.MaxValue;
        Vector2 p = new Vector2(x, y);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude <= 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }

        return best;
    }

    private static bool IsInsideMap(int x, int y)
    {
        return x >= 0 && x < MapWidth && y >= 0 && y < MapHeight;
    }

    private static bool IsNearSpawn(int x, int y, int radius)
    {
        return Mathf.Abs(x - 64) <= radius && Mathf.Abs(y - 55) <= radius;
    }

    private static void ConfigureTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace('\\', '/').Trim('/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
    }

    private readonly struct LayerSpec
    {
        public readonly string Name;
        public readonly int SortingOrder;
        public readonly bool HasCollider;
        public readonly bool RendererEnabled;

        public LayerSpec(string name, int sortingOrder, bool hasCollider, bool rendererEnabled)
        {
            Name = name;
            SortingOrder = sortingOrder;
            HasCollider = hasCollider;
            RendererEnabled = rendererEnabled;
        }
    }

    private readonly struct WorldScene
    {
        public readonly GameObject Root;
        public readonly Transform GridTransform;
        public readonly Dictionary<string, Tilemap> Tilemaps;

        public WorldScene(GameObject root, Transform gridTransform, Dictionary<string, Tilemap> tilemaps)
        {
            Root = root;
            GridTransform = gridTransform;
            Tilemaps = tilemaps;
        }
    }

    private sealed class WorldState
    {
        public readonly bool[,] Road = new bool[MapWidth, MapHeight];
        public readonly bool[,] Water = new bool[MapWidth, MapHeight];
        public readonly bool[,] Sand = new bool[MapWidth, MapHeight];
        public readonly bool[,] Path = new bool[MapWidth, MapHeight];
        public readonly bool[,] Occupied = new bool[MapWidth, MapHeight];
    }

    private sealed class MapContext
    {
        public readonly WorldScene Scene;
        public readonly AssetLibrary Library;
        public readonly WorldState State;
        public readonly WorldReport Report;
        public readonly System.Random Random;

        public MapContext(WorldScene scene, AssetLibrary library, WorldState state, WorldReport report, System.Random random)
        {
            Scene = scene;
            Library = library;
            State = state;
            Report = report;
            Random = random;
        }
    }

    private sealed class AssetLibrary
    {
        private readonly Dictionary<string, List<TileEntry>> tilesByCategory;
        private readonly List<PrefabEntry> prefabs;

        private AssetLibrary(Dictionary<string, List<TileEntry>> tilesByCategory, List<PrefabEntry> prefabs)
        {
            this.tilesByCategory = tilesByCategory;
            this.prefabs = prefabs;
        }

        public static AssetLibrary Load(WorldReport report)
        {
            Dictionary<string, List<TileEntry>> tiles = LoadTilesByCategory(report);
            List<PrefabEntry> prefabEntries = new List<PrefabEntry>();

            foreach (string root in ExistingRoots(PropRoots))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        continue;
                    }

                    string category = CategorizePrefab(path, false);
                    prefabEntries.Add(new PrefabEntry(prefab, path, category, NormalizeSearchText(path), false));
                    report.PrefabCategoriesFound[category] = report.PrefabCategoriesFound.TryGetValue(category, out int count) ? count + 1 : 1;
                }
            }

            foreach (string root in ExistingRoots(BuildingRoots))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        continue;
                    }

                    string category = CategorizePrefab(path, true);
                    prefabEntries.Add(new PrefabEntry(prefab, path, category, NormalizeSearchText(path), true));
                    report.PrefabCategoriesFound[category] = report.PrefabCategoriesFound.TryGetValue(category, out int count) ? count + 1 : 1;
                }
            }

            foreach (string expected in new[] { "Grass", "Forest", "Dirt", "Farm", "Road", "Road_Marking", "Sidewalk", "Curb", "Sand", "Water", "Decals" })
            {
                if (!tiles.ContainsKey(expected) || tiles[expected].Count == 0)
                {
                    report.MissingCategories.Add($"Tile: {expected}");
                }
            }

            foreach (string expected in new[] { "Nature", "Town", "Beach", "Farm", "Buildings" })
            {
                if (!report.PrefabCategoriesFound.ContainsKey(expected))
                {
                    report.MissingCategories.Add($"Prefab: {expected}");
                }
            }

            return new AssetLibrary(tiles, prefabEntries);
        }

        public TileBase PickTile(string category, string[] keywords, System.Random random, WorldReport report)
        {
            if (!tilesByCategory.TryGetValue(category, out List<TileEntry> entries) || entries.Count == 0)
            {
                report.MissingCategories.Add($"Tile: {category}");
                if (!tilesByCategory.TryGetValue("Grass", out entries) || entries.Count == 0)
                {
                    return null;
                }

                report.AddFallbackTile($"{category} -> Grass/{entries[0].Tile.name}");
            }

            List<TileEntry> matches = keywords.Length == 0
                ? entries
                : entries.Where(entry => keywords.Any(keyword => entry.SearchText.Contains(keyword.ToLowerInvariant()))).ToList();

            if (matches.Count == 0)
            {
                matches = entries;
                if (keywords.Length > 0)
                {
                    report.AddFallbackTile($"{category} keywords [{string.Join(", ", keywords)}] -> {entries[0].Tile.name}");
                }
            }

            return matches[random.Next(matches.Count)].Tile;
        }

        public GameObject PickPrefab(string keyword, System.Random random)
        {
            string token = NormalizeSearchText(keyword);
            List<PrefabEntry> matches = prefabs.Where(entry => entry.SearchText.Contains(token)).ToList();
            if (matches.Count == 0 && token == "tree")
            {
                matches = prefabs.Where(entry => entry.SearchText.Contains("oak") || entry.SearchText.Contains("pine")).ToList();
            }

            if (matches.Count == 0)
            {
                return null;
            }

            return matches[random.Next(matches.Count)].Prefab;
        }
    }

    private readonly struct TileEntry
    {
        public readonly TileBase Tile;
        public readonly string Path;
        public readonly string SearchText;

        public TileEntry(TileBase tile, string path, string searchText)
        {
            Tile = tile;
            Path = path;
            SearchText = searchText;
        }
    }

    private readonly struct PrefabEntry
    {
        public readonly GameObject Prefab;
        public readonly string Path;
        public readonly string Category;
        public readonly string SearchText;
        public readonly bool IsBuilding;

        public PrefabEntry(GameObject prefab, string path, string category, string searchText, bool isBuilding)
        {
            Prefab = prefab;
            Path = path;
            Category = category;
            SearchText = searchText;
            IsBuilding = isBuilding;
        }
    }

    private sealed class WorldReport
    {
        public string BackupScenePath = "not created";
        public bool MapLayerStackCreated;
        public Vector3 PlayerSpawnPosition;
        public Vector3 CameraPosition;
        public string CarMoved = "none";
        public readonly Dictionary<string, int> TileCategoriesFound = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> PrefabCategoriesFound = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> MissingCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> FallbackTilesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> TileCountsByLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> PropsPlacedByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> BuildingsPlacedByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> DeletedMapObjects = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public void CountTile(string layerName, string tileName)
        {
            TileCountsByLayer[layerName] = TileCountsByLayer.TryGetValue(layerName, out int count) ? count + 1 : 1;
        }

        public void CountPlacedPrefab(string keyword, string prefabName, bool building)
        {
            Dictionary<string, int> target = building ? BuildingsPlacedByCategory : PropsPlacedByCategory;
            string key = $"{keyword} ({prefabName})";
            target[key] = target.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        public void AddMissingPrefab(string keyword)
        {
            MissingCategories.Add($"Prefab keyword: {keyword}");
        }

        public void AddFallbackTile(string message)
        {
            FallbackTilesUsed.Add(message);
        }
    }
}
#endif
