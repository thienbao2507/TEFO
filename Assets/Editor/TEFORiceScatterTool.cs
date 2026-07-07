using UnityEditor;
using UnityEngine;

public class TEFORiceScatterTool : EditorWindow
{
    private GameObject ricePrefab;
    private Transform parent;
    private Vector2 startPosition;
    private int rows = 5;
    private int columns = 8;
    private float spacingX = 0.22f;
    private float spacingY = 0.28f;
    private float randomOffset = 0.05f;
    private float minScale = 0.9f;
    private float maxScale = 1.1f;
    private float zPosition = 0f;
    private bool randomFlipX = true;
    private int baseSortingOrder = 10000;
    private float ySortMultiplier = 100f;

    [MenuItem("TEFO/Props/Scatter Rice Field")]
    public static void OpenWindow()
    {
        GetWindow<TEFORiceScatterTool>("Rice Scatter");
    }

    private void OnGUI()
    {
        ricePrefab = (GameObject)EditorGUILayout.ObjectField("Rice Prefab", ricePrefab, typeof(GameObject), false);
        parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);

        EditorGUILayout.Space();

        startPosition = EditorGUILayout.Vector2Field("Start Position", startPosition);
        rows = EditorGUILayout.IntField("Rows", rows);
        columns = EditorGUILayout.IntField("Columns", columns);

        EditorGUILayout.Space();

        spacingX = EditorGUILayout.FloatField("Spacing X", spacingX);
        spacingY = EditorGUILayout.FloatField("Spacing Y", spacingY);
        randomOffset = EditorGUILayout.FloatField("Random Offset", randomOffset);

        EditorGUILayout.Space();

        minScale = EditorGUILayout.FloatField("Min Scale", minScale);
        maxScale = EditorGUILayout.FloatField("Max Scale", maxScale);
        zPosition = EditorGUILayout.FloatField("Z Position", zPosition);
        randomFlipX = EditorGUILayout.Toggle("Random Flip X", randomFlipX);

        EditorGUILayout.Space();

        baseSortingOrder = EditorGUILayout.IntField("Base Sorting Order", baseSortingOrder);
        ySortMultiplier = EditorGUILayout.FloatField("Y Sort Multiplier", ySortMultiplier);

        EditorGUILayout.Space();

        if (GUILayout.Button("Use Selected Object Position"))
        {
            UseSelectedObjectPosition();
        }

        if (GUILayout.Button("Scatter Rice"))
        {
            ScatterRice();
        }
    }

    private void UseSelectedObjectPosition()
    {
        if (Selection.activeTransform == null)
            return;

        Vector3 position = Selection.activeTransform.position;
        startPosition = new Vector2(position.x, position.y);
    }

    private void ScatterRice()
    {
        if (ricePrefab == null)
        {
            Debug.LogError("Rice Prefab is missing.");
            return;
        }

        if (rows <= 0 || columns <= 0)
        {
            Debug.LogError("Rows and Columns must be greater than 0.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Scatter Rice Field");

        Vector3 baseScale = ricePrefab.transform.localScale;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(ricePrefab) as GameObject;

                if (instance == null)
                    instance = Instantiate(ricePrefab);

                Undo.RegisterCreatedObjectUndo(instance, "Create Rice Plant");

                if (parent != null)
                    instance.transform.SetParent(parent, true);

                float offsetX = Random.Range(-randomOffset, randomOffset);
                float offsetY = Random.Range(-randomOffset, randomOffset);

                float posX = startPosition.x + x * spacingX + offsetX;
                float posY = startPosition.y + y * spacingY + offsetY;

                instance.transform.position = new Vector3(posX, posY, zPosition);

                float scaleMultiplier = Random.Range(minScale, maxScale);
                instance.transform.localScale = baseScale * scaleMultiplier;

                SpriteRenderer spriteRenderer = instance.GetComponent<SpriteRenderer>();

                if (spriteRenderer != null)
                {
                    int sortingOrder = baseSortingOrder - Mathf.RoundToInt(posY * ySortMultiplier);
                    spriteRenderer.sortingOrder = sortingOrder;

                    if (randomFlipX)
                        spriteRenderer.flipX = Random.value > 0.5f;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }
}