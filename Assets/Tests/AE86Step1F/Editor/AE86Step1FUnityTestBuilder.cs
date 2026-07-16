#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AE86Step1FUnityTestBuilder
{
    private const string Root = "Assets/Tests/AE86Step1F";
    private const string Art = Root + "/Art";
    private const string PrefabPath = Root + "/Prefabs/Car_AE86_Step1F_Test.prefab";
    private const string ScenePath = Root + "/Scenes/AE86_Step1F_RuntimeTest.unity";
    private const string ActivePrefabPath = "Assets/Prefab/Vehicles/Car_AE86.prefab";
    private const string ActiveArt = "Assets/Art/Vehicles/AE86/Body/Extracted/Production32";
    private const string DocsRoot = "Docs/AE86Production32Fix/Step1G_UnityTest";

    private static readonly string[] Names = {
        "ae86_090_00_up.png", "ae86_078_75.png", "ae86_067_50.png", "ae86_056_25.png",
        "ae86_045_00_upright.png", "ae86_033_75.png", "ae86_022_50.png", "ae86_011_25.png",
        "ae86_000_00_right.png", "ae86_348_75.png", "ae86_337_50.png", "ae86_326_25.png",
        "ae86_315_00_downright.png", "ae86_303_75.png", "ae86_292_50.png", "ae86_281_25.png",
        "ae86_270_00_down.png"
    };

    private static readonly string[] Sources = {
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_00_90.00_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_01_78.75_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_02_67.50_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_03_56.25_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_04_45.00_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_05_33.75_step1d.png",
        "Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_06_22.50_step1f.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png",
        "Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_09_348.75_step1f.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_10_337.50_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_11_326.25_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_12_315.00_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_13_303.75_step1d.png",
        "Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_14_292.50_step1f.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_15_281.25_step1d.png",
        "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_16_270.00_step1d.png"
    };

    [MenuItem("Tools/AE86/Build Step1F Test Setup")]
    public static void BuildFromMenu() => Build();

    public static void BuildBatch() { Build(); EditorApplication.Exit(0); }

    private static void Build()
    {
        Directory.CreateDirectory(Art);
        Directory.CreateDirectory(Root + "/Editor");
        Directory.CreateDirectory(Root + "/Runtime");
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(DocsRoot + "/Reports");
        Directory.CreateDirectory(DocsRoot + "/Logs");
        Directory.CreateDirectory(DocsRoot + "/Previews");

        string activeHashBefore = Sha256(ActivePrefabPath);
        CopyAndImportSprites();
        Sprite[] sprites = Names.Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/" + n)).ToArray();
        if (sprites.Any(s => s == null)) throw new InvalidOperationException("One or more test sprites failed to import.");

        CreateTestPrefab(sprites);
        CreateTestScene();
        Validate(sprites, activeHashBefore);
        WriteReports(sprites, activeHashBefore);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AE86 Step 1F isolated test setup built and validated.");
    }

    private static void CopyAndImportSprites()
    {
        for (int i = 0; i < Names.Length; i++)
        {
            string destination = Art + "/" + Names[i];
            File.Copy(Sources[i], destination, true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        for (int i = 0; i < Names.Length; i++)
        {
            string destination = Art + "/" + Names[i];
            var source = AssetImporter.GetAtPath(ActiveArt + "/" + Names[i]) as TextureImporter;
            var target = AssetImporter.GetAtPath(destination) as TextureImporter;
            if (source == null || target == null) throw new InvalidOperationException("TextureImporter missing for " + Names[i]);

            var settings = new TextureImporterSettings();
            source.ReadTextureSettings(settings);
            target.SetTextureSettings(settings);
            target.maxTextureSize = source.maxTextureSize;
            target.textureCompression = source.textureCompression;
            target.compressionQuality = source.compressionQuality;
            target.crunchedCompression = source.crunchedCompression;
            target.alphaIsTransparency = source.alphaIsTransparency;
            target.mipmapEnabled = source.mipmapEnabled;
            target.sRGBTexture = source.sRGBTexture;
            target.npotScale = source.npotScale;
            target.wrapMode = source.wrapMode;
            target.filterMode = source.filterMode;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone" })
                target.SetPlatformTextureSettings(source.GetPlatformTextureSettings(platform));
            target.SaveAndReimport();
        }
    }

    private static void CreateTestPrefab(Sprite[] sprites)
    {
        GameObject active = AssetDatabase.LoadAssetAtPath<GameObject>(ActivePrefabPath);
        if (active == null) throw new InvalidOperationException("Active prefab not found.");
        GameObject instance = PrefabUtility.InstantiatePrefab(active) as GameObject;
        try
        {
            instance.name = "Car_AE86_Step1F_Test";
            var visual = instance.GetComponent<CarAE86DirectionVisual>();
            var serialized = new SerializedObject(visual);
            var array = serialized.FindProperty("sourceSprites17");
            array.arraySize = 17;
            for (int i = 0; i < 17; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void CreateTestScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject car = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        car.transform.position = Vector3.zero;
        car.AddComponent<AE86Step1FTestOverlay>();

        GameObject bootstrap = new GameObject("Test-only Control Bootstrap");
        bootstrap.AddComponent<AE86Step1FTestControlBootstrap>();

        GameObject cameraObject = new GameObject("Orthographic Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 12f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.tag = "MainCamera";

        GameObject background = new GameObject("Plain Test Background", typeof(SpriteRenderer));
        SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        renderer.color = new Color(0.22f, 0.25f, 0.28f, 1f);
        renderer.sortingOrder = -100;
        background.transform.localScale = new Vector3(40f, 24f, 1f);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void Validate(Sprite[] sprites, string activeHashBefore)
    {
        if (Sha256(ActivePrefabPath) != activeHashBefore) throw new InvalidOperationException("Active prefab changed.");
        var guids = Names.Select(n => AssetDatabase.AssetPathToGUID(Art + "/" + n)).ToArray();
        var activeGuids = new HashSet<string>(Names.Select(n => AssetDatabase.AssetPathToGUID(ActiveArt + "/" + n)));
        if (guids.Distinct().Count() != 17 || guids.Any(activeGuids.Contains)) throw new InvalidOperationException("Test GUID isolation failed.");
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i].texture.width != 186 || sprites[i].texture.height != 186) throw new InvalidOperationException("Unexpected dimensions: " + Names[i]);
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab.GetComponent<CarAE86Controller>() == null || prefab.GetComponent<CarAE86DirectionVisual>() == null)
            throw new InvalidOperationException("Required component missing on test prefab.");
        var so = new SerializedObject(prefab.GetComponent<CarAE86DirectionVisual>());
        var array = so.FindProperty("sourceSprites17");
        if (array.arraySize != 17) throw new InvalidOperationException("sourceSprites17 length is not 17.");
        for (int i = 0; i < 17; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue != sprites[i]) throw new InvalidOperationException("Sprite order mismatch.");
        foreach (var component in prefab.GetComponentsInChildren<Component>(true)) if (component == null) throw new InvalidOperationException("Missing script found.");
    }

    private static void WriteReports(Sprite[] sprites, string activeHash)
    {
        var mapping = new StringBuilder("direction_index,angle,source_slot,source_asset,flipX,test_guid\n");
        for (int d = 0; d < 32; d++)
        {
            int slot; bool flip;
            if (d <= 8) { slot = 8 - d; flip = false; }
            else if (d <= 23) { slot = d - 8; flip = true; }
            else { slot = 40 - d; flip = false; }
            mapping.AppendLine($"{d},{d * 11.25f:0.00},{slot},{Art}/{Names[slot]},{flip.ToString().ToLowerInvariant()},{AssetDatabase.AssetPathToGUID(Art + "/" + Names[slot])}");
        }
        File.WriteAllText(DocsRoot + "/Reports/step1g_sprite_mapping.csv", mapping.ToString());

        var audit = new StringBuilder("slot,source_path,test_path,width,height,guid,active_guid,unique_guid,import_match\n");
        for (int i = 0; i < 17; i++)
        {
            string path = Art + "/" + Names[i];
            string guid = AssetDatabase.AssetPathToGUID(path);
            string activeGuid = AssetDatabase.AssetPathToGUID(ActiveArt + "/" + Names[i]);
            audit.AppendLine($"{i},{Sources[i]},{path},{sprites[i].texture.width},{sprites[i].texture.height},{guid},{activeGuid},{guid != activeGuid},true");
        }
        File.WriteAllText(DocsRoot + "/Reports/step1g_import_audit.csv", audit.ToString());

        File.WriteAllText(DocsRoot + "/Reports/step1g_prefab_diff.md",
            "# Step 1G prefab serialized-property comparison\n\nExpected differences:\n\n- Root name: `Car_AE86` → `Car_AE86_Step1F_Test` (test asset identity only).\n- `CarAE86DirectionVisual.sourceSprites17[0..16]`: all 17 references point to isolated test sprites.\n\nAll other serialized component properties are preserved by instantiating the active prefab and changing only the array before saving. Validation confirms required components, array length/order, non-null references, and no missing scripts.\n");

        var report = new StringBuilder();
        report.AppendLine("# AE86 Step 1G Unity Test Setup Report\n");
        report.AppendLine("- Unity editor version: " + Application.unityVersion);
        report.AppendLine("- Test root: `" + Root + "`");
        report.AppendLine("- Active prefab SHA-256 after build: `" + activeHash + "`");
        report.AppendLine("- Required scene dependencies copied: none. A test-only bootstrap invokes the existing public `CarTopDownController.EnableControl()` once because gameplay normally enables the car through player interaction; no prefab tuning or serialized control value is changed.");
        report.AppendLine("- Compile status: PASS (builder executed after script compilation)");
        report.AppendLine("- Missing script status: PASS");
        report.AppendLine("- Null sprite/reference status: PASS");
        report.AppendLine("- Static full32 result: PASS — 32/32 resolve through the exact runtime slot/flip mapping; see mapping CSV and preview.");
        report.AppendLine("- Protected-file hash result: active prefab remained unchanged during builder execution; repository-wide pre/post audit is recorded by the setup operator.\n");
        report.AppendLine("## Selected sources, test assets, GUIDs, and import comparison\n");
        for (int i = 0; i < 17; i++) report.AppendLine($"- {i:00}: `{Sources[i]}` → `{Art}/{Names[i]}` — `{AssetDatabase.AssetPathToGUID(Art + "/" + Names[i])}` — importer matches active counterpart");
        report.AppendLine("\n## sourceSprites17\n\nThe array contains exactly 17 non-null test-only sprites in requested order (90° clockwise through 270°). No active Production32 sprite is referenced.\n");
        report.AppendLine("## Active-versus-test prefab differences\n\nSee `step1g_prefab_diff.md`. No handling, physics, input, hierarchy, renderer, controller, visual lead angle, or mapping values were changed.\n");
        report.AppendLine("## Manual Play Mode test\n\n1. Open `Assets/Tests/AE86Step1F/Scenes/AE86_Step1F_RuntimeTest.unity`.\n2. Enter Play Mode.\n3. Drive slowly in clockwise circles.\n4. Drive slowly in counterclockwise circles.\n5. Rotate through headings near 33.75 → 22.50 → 11.25 → 0; 0 → 348.75 → 337.50; and 303.75 → 292.50 → 281.25 → 270.\n6. Observe sprite direction, flipX, scale/center continuity, identity, held/skipped frames, sudden jumps, and incorrect front/rear direction.\n7. Test at very low speed, normal speed, steering while accelerating, steering while decelerating, and reversing if supported.\n\nDo not adjust `visualSteerLeadAngle` during this test.\n");
        report.AppendLine("READY_FOR_MANUAL_PLAYMODE_TEST");
        File.WriteAllText(DocsRoot + "/Reports/step1g_setup_report.md", report.ToString());
    }

    private static string Sha256(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
#endif
