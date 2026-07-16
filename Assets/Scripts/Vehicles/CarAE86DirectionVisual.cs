using System.Collections.Generic;
using UnityEngine;

public class CarAE86DirectionVisual : MonoBehaviour
{
    private const int DirectionCount32 = 32;
    private const int SourceSpriteCount = 17;

    private static readonly float[] SourceAngles =
    {
        90f, 78.75f, 67.5f, 56.25f, 45f, 33.75f, 22.5f, 11.25f, 0f,
        348.75f, 337.5f, 326.25f, 315f, 303.75f, 292.5f, 281.25f, 270f
    };

    [Header("References")]
    [SerializeField] private CarAE86Controller controller;
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("32 Direction Source Sprites")]
    [Tooltip("17 sprites ordered from 90 degrees (Up) clockwise through Right to 270 degrees (Down).")]
    [SerializeField] private Sprite[] sourceSprites17 = new Sprite[SourceSpriteCount];

    private bool warnedMissingController;
    private bool warnedMissingRenderer;
    private bool warnedInvalidSourceArray;
    private readonly HashSet<int> warnedMissingSpriteSlots = new HashSet<int>();
    private int lastDirectionIndex = -1;
    private Sprite lastSprite;
    private bool lastFlipX;
    private bool hasAppliedVisual;

    private void Awake()
    {
        ResolveReferences();
        ValidateSourceSprites();
        ApplyCurrentDirection();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ApplyCurrentDirection();
    }

    private void ResolveReferences()
    {
        if (controller == null)
            controller = GetComponent<CarAE86Controller>();

        if (bodyRenderer == null)
        {
            Transform body = transform.Find("Body");
            if (body != null)
                bodyRenderer = body.GetComponent<SpriteRenderer>();
        }

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void ApplyCurrentDirection()
    {
        if (controller == null)
        {
            if (!warnedMissingController)
            {
                Debug.LogWarning($"{name}: CarAE86DirectionVisual is missing a CarAE86Controller reference.", this);
                warnedMissingController = true;
            }

            return;
        }

        if (bodyRenderer == null)
        {
            if (!warnedMissingRenderer)
            {
                Debug.LogWarning($"{name}: CarAE86DirectionVisual is missing a Body SpriteRenderer reference.", this);
                warnedMissingRenderer = true;
            }

            return;
        }

        int directionIndex = WrapDirectionIndex32(controller.CurrentDirectionIndex32);
        ResolveSourceSlot(directionIndex, out int sourceIndex, out bool flipX);
        Sprite nextSprite = GetSourceSprite(sourceIndex);

        if (nextSprite == null)
            return;

        if (hasAppliedVisual &&
            directionIndex == lastDirectionIndex &&
            nextSprite == lastSprite &&
            flipX == lastFlipX &&
            bodyRenderer.sprite == nextSprite &&
            bodyRenderer.flipX == flipX)
        {
            return;
        }

        if (bodyRenderer.sprite != nextSprite)
            bodyRenderer.sprite = nextSprite;

        if (bodyRenderer.flipX != flipX)
            bodyRenderer.flipX = flipX;

        lastDirectionIndex = directionIndex;
        lastSprite = nextSprite;
        lastFlipX = flipX;
        hasAppliedVisual = true;
    }

    private static void ResolveSourceSlot(int directionIndex, out int sourceIndex, out bool flipX)
    {
        directionIndex = WrapDirectionIndex32(directionIndex);

        if (directionIndex <= 8)
        {
            sourceIndex = 8 - directionIndex;
            flipX = false;
            return;
        }

        if (directionIndex <= 23)
        {
            sourceIndex = directionIndex - 8;
            flipX = true;
            return;
        }

        sourceIndex = 40 - directionIndex;
        flipX = false;
    }

    private Sprite GetSourceSprite(int sourceIndex)
    {
        if (TryGetAssignedSprite(sourceIndex, out Sprite sprite))
            return sprite;

        int fallbackIndex = FindNearestAssignedSlot(sourceIndex);
        Sprite fallback = fallbackIndex >= 0 ? sourceSprites17[fallbackIndex] : null;
        WarnMissingSpriteSlotOnce(sourceIndex, fallbackIndex, fallback);
        return fallback;
    }

    private void ValidateSourceSprites()
    {
        int actualLength = sourceSprites17 == null ? 0 : sourceSprites17.Length;

        if (actualLength != SourceSpriteCount && !warnedInvalidSourceArray)
        {
            Debug.LogWarning(
                $"{name}: CarAE86DirectionVisual sourceSprites17 must contain exactly {SourceSpriteCount} sprites; current length is {actualLength}. Missing directions will use the nearest assigned source.",
                this);
            warnedInvalidSourceArray = true;
        }

        for (int sourceIndex = 0; sourceIndex < SourceSpriteCount; sourceIndex++)
        {
            if (TryGetAssignedSprite(sourceIndex, out _))
                continue;

            int fallbackIndex = FindNearestAssignedSlot(sourceIndex);
            Sprite fallback = fallbackIndex >= 0 ? sourceSprites17[fallbackIndex] : null;
            WarnMissingSpriteSlotOnce(sourceIndex, fallbackIndex, fallback);
        }
    }

    private bool TryGetAssignedSprite(int sourceIndex, out Sprite sprite)
    {
        sprite = null;

        if (sourceSprites17 == null || sourceIndex < 0 || sourceIndex >= sourceSprites17.Length)
            return false;

        sprite = sourceSprites17[sourceIndex];
        return sprite != null;
    }

    private int FindNearestAssignedSlot(int missingSourceIndex)
    {
        for (int distance = 1; distance < SourceSpriteCount; distance++)
        {
            int lowerIndex = missingSourceIndex - distance;
            if (TryGetAssignedSprite(lowerIndex, out _))
                return lowerIndex;

            int upperIndex = missingSourceIndex + distance;
            if (TryGetAssignedSprite(upperIndex, out _))
                return upperIndex;
        }

        return -1;
    }

    private void WarnMissingSpriteSlotOnce(int missingSourceIndex, int fallbackIndex, Sprite fallback)
    {
        if (!warnedMissingSpriteSlots.Add(missingSourceIndex))
            return;

        float expectedAngle = SourceAngles[missingSourceIndex];
        if (fallback != null)
        {
            Debug.LogWarning(
                $"{name}: CarAE86DirectionVisual sourceSprites17[{missingSourceIndex}] ({expectedAngle:0.##} degrees) is missing; using nearest slot {fallbackIndex} ({SourceAngles[fallbackIndex]:0.##} degrees), '{fallback.name}'.",
                this);
            return;
        }

        Debug.LogWarning(
            $"{name}: CarAE86DirectionVisual sourceSprites17[{missingSourceIndex}] ({expectedAngle:0.##} degrees) is missing and no fallback sprite is assigned.",
            this);
    }

    private static int WrapDirectionIndex32(int directionIndex)
    {
        directionIndex %= DirectionCount32;

        if (directionIndex < 0)
            directionIndex += DirectionCount32;

        return directionIndex;
    }
}
