#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TEFOMapLayerSetupTool
{
    private const string RootName = "TEFO_Map";
    private const string GridName = "Grid";

    private static readonly LayerSpec[] TilemapLayerSpecs =
    {
        new LayerSpec("Ground", -100, false, false),
        new LayerSpec("Grass", -90, false, false),
        new LayerSpec("Forest", -85, false, false),
        new LayerSpec("Dirt", -80, false, false),
        new LayerSpec("Road", -70, false, false),
        new LayerSpec("Road_Marking", -60, false, false),
        new LayerSpec("Sidewalk", -50, false, false),
        new LayerSpec("Curb", -45, false, false),
        new LayerSpec("Sand", -40, false, false),
        new LayerSpec("Water", -35, false, false),
        new LayerSpec("Decals_Back", -20, false, false),
        new LayerSpec("Props_Back", 0, false, false),
        new LayerSpec("Buildings", 10, false, false),
        new LayerSpec("Props_Front", 30, false, false),
        new LayerSpec("Collision", 100, true, true)
    };

    [MenuItem("TEFO/Map/Create Tilemap Layer Stack")]
    public static void CreateTilemapLayerStack()
    {
        SetupLayerStack(true);
    }

    public static GameObject SetupLayerStack(bool selectRoot)
    {
        GameObject root = FindOrCreateRoot();
        GameObject gridObject = FindOrCreateChild(root.transform, GridName);
        ConfigureTransform(gridObject.transform);

        Grid grid = EnsureComponent<Grid>(gridObject);
        Undo.RecordObject(grid, "Configure TEFO map grid");
        grid.cellSize = new Vector3(1f, 1f, 0f);
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
        EditorUtility.SetDirty(grid);

        foreach (LayerSpec spec in TilemapLayerSpecs)
        {
            GameObject layerObject = FindOrCreateLayer(root.transform, gridObject.transform, spec.Name);
            ConfigureTilemapLayer(layerObject, spec);
        }

        if (selectRoot)
        {
            Selection.activeGameObject = root;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("TEFO map Tilemap layer stack is ready.");
        return root;
    }

    [MenuItem("TEFO/Map/Apply Tilemap Sorting Orders")]
    public static void ApplyTilemapSortingOrders()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogWarning($"No {RootName} object found. Create the layer stack first.");
            return;
        }

        Transform gridTransform = FindGridTransform(root);
        int updatedCount = 0;

        foreach (LayerSpec spec in TilemapLayerSpecs)
        {
            Transform layerTransform = gridTransform.Find(spec.Name);
            if (layerTransform == null)
            {
                continue;
            }

            TilemapRenderer renderer = layerTransform.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                continue;
            }

            Undo.RecordObject(renderer, "Apply TEFO tilemap sorting order");
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = spec.SortingOrder;
            renderer.enabled = !spec.HideRenderer;
            EditorUtility.SetDirty(renderer);
            updatedCount++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"TEFO tilemap sorting orders applied to {updatedCount} layer(s).");
    }

    private static GameObject FindOrCreateRoot()
    {
        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            return root;
        }

        root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create TEFO map root");
        return root;
    }

    private static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
        Undo.SetTransformParent(child.transform, parent, $"Parent {childName}");
        ConfigureTransform(child.transform);
        return child;
    }

    private static GameObject FindOrCreateLayer(Transform root, Transform gridParent, string layerName)
    {
        Transform existing = gridParent.Find(layerName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        Transform legacyDirectChild = root.Find(layerName);
        if (legacyDirectChild != null)
        {
            Undo.SetTransformParent(legacyDirectChild, gridParent, $"Move {layerName} under TEFO Grid");
            ConfigureTransform(legacyDirectChild);
            return legacyDirectChild.gameObject;
        }

        GameObject layerObject = new GameObject(layerName);
        Undo.RegisterCreatedObjectUndo(layerObject, $"Create {layerName} tilemap layer");
        Undo.SetTransformParent(layerObject.transform, gridParent, $"Parent {layerName} tilemap layer");
        ConfigureTransform(layerObject.transform);
        return layerObject;
    }

    private static Transform FindGridTransform(GameObject root)
    {
        Transform gridTransform = root.transform.Find(GridName);
        return gridTransform != null ? gridTransform : root.transform;
    }

    private static void ConfigureTilemapLayer(GameObject layerObject, LayerSpec spec)
    {
        ConfigureTransform(layerObject.transform);

        Tilemap tilemap = EnsureComponent<Tilemap>(layerObject);
        TilemapRenderer renderer = EnsureComponent<TilemapRenderer>(layerObject);

        Undo.RecordObject(tilemap, "Configure TEFO tilemap");
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        tilemap.color = spec.HasCollider ? new Color(1f, 0f, 0f, 0.35f) : Color.white;
        EditorUtility.SetDirty(tilemap);

        Undo.RecordObject(renderer, "Configure TEFO tilemap renderer");
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = spec.SortingOrder;
        renderer.enabled = !spec.HideRenderer;
        EditorUtility.SetDirty(renderer);

        if (!spec.HasCollider)
        {
            return;
        }

        TilemapCollider2D tilemapCollider = EnsureComponent<TilemapCollider2D>(layerObject);
        CompositeCollider2D compositeCollider = EnsureComponent<CompositeCollider2D>(layerObject);
        Rigidbody2D body = EnsureComponent<Rigidbody2D>(layerObject);

        Undo.RecordObject(tilemapCollider, "Configure TEFO collision tilemap");
        tilemapCollider.usedByComposite = true;
        EditorUtility.SetDirty(tilemapCollider);

        Undo.RecordObject(compositeCollider, "Configure TEFO composite collider");
        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        EditorUtility.SetDirty(compositeCollider);

        Undo.RecordObject(body, "Configure TEFO collision body");
        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
        EditorUtility.SetDirty(body);
    }

    private static void ConfigureTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(gameObject);
    }

    private readonly struct LayerSpec
    {
        public readonly string Name;
        public readonly int SortingOrder;
        public readonly bool HasCollider;
        public readonly bool HideRenderer;

        public LayerSpec(string name, int sortingOrder, bool hasCollider, bool hideRenderer)
        {
            Name = name;
            SortingOrder = sortingOrder;
            HasCollider = hasCollider;
            HideRenderer = hideRenderer;
        }
    }
}
#endif

