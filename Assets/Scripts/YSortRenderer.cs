using UnityEngine;

public class YSortRenderer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private int baseSortingOrder = 10000;
    [SerializeField] private float ySortMultiplier = 100f;
    [SerializeField] private float yOffset = -0.35f;
    [SerializeField] private int orderOffset = 0;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null)
            return;

        float sortY = transform.position.y + yOffset;
        int sortingOrder = baseSortingOrder - Mathf.RoundToInt(sortY * ySortMultiplier) + orderOffset;

        targetRenderer.sortingOrder = sortingOrder;
    }
}