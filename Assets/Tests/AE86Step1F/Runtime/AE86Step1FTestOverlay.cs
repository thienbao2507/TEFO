using UnityEngine;

/// <summary>Read-only diagnostics for the isolated Step 1F AE86 play-mode test.</summary>
public sealed class AE86Step1FTestOverlay : MonoBehaviour
{
    [SerializeField] private Rigidbody2D observedBody;
    [SerializeField] private SpriteRenderer observedRenderer;
    private float smoothedDeltaTime;

    private void Awake()
    {
        if (observedBody == null)
            observedBody = GetComponent<Rigidbody2D>();
        if (observedRenderer == null)
            observedRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        Vector2 velocity = observedBody == null ? Vector2.zero : observedBody.linearVelocity;
        float heading = velocity.sqrMagnitude < 0.0001f ? 0f : Mathf.Repeat(Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg, 360f);
        int sector = Mathf.RoundToInt(heading / 11.25f) % 32;
        string spriteName = observedRenderer != null && observedRenderer.sprite != null ? observedRenderer.sprite.name : "<null>";
        bool flipX = observedRenderer != null && observedRenderer.flipX;
        Vector3 position = observedBody == null ? transform.position : observedBody.transform.position;
        float fps = smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f;

        GUI.Box(new Rect(12, 12, 410, 244), "AE86 Step 1F — read-only runtime audit");
        GUI.Label(new Rect(24, 42, 390, 20), $"Speed: {velocity.magnitude:0.00}");
        GUI.Label(new Rect(24, 62, 390, 20), $"Velocity heading: {heading:0.00}°");
        GUI.Label(new Rect(24, 82, 390, 20), $"Estimated 32-sector: {sector:00} ({sector * 11.25f:0.00}°)");
        GUI.Label(new Rect(24, 102, 390, 20), $"Sprite: {spriteName}");
        GUI.Label(new Rect(24, 122, 390, 20), $"SpriteRenderer.flipX: {flipX}");
        GUI.Label(new Rect(24, 142, 390, 20), $"Position: ({position.x:0.00}, {position.y:0.00}, {position.z:0.00})");
        GUI.Label(new Rect(24, 162, 390, 20), $"FPS: {fps:0.0}");
        GUI.Label(new Rect(24, 188, 390, 20), "Drive: Horizontal/Vertical input axes (typically arrows or WASD)");
        GUI.Label(new Rect(24, 208, 390, 20), "Test slow CW/CCW circles and transitions near 22.5°, 0°, 292.5°.");
        GUI.Label(new Rect(24, 228, 390, 20), "Do not adjust visualSteerLeadAngle during this test.");
    }
}
