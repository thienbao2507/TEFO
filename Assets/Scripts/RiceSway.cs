using UnityEngine;

public class RiceSway : MonoBehaviour
{
    private const float PushDirectionDeadZone = 0.03f;

    [Header("Rotation Sway")]
    [SerializeField] private float minAngle = 2f;
    [SerializeField] private float maxAngle = 5f;
    [SerializeField] private float minSpeed = 0.8f;
    [SerializeField] private float maxSpeed = 1.6f;

    [Header("Position Sway")]
    [SerializeField] private float xMoveAmount = 0.015f;

    [Header("Noise")]
    [SerializeField] private float noiseAmount = 0.35f;
    [SerializeField] private float noiseSpeed = 0.8f;

    [Header("Player Push")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private Vector2 playerInteractionOffset = new Vector2(0f, -0.35f);
    [SerializeField] private float pushRadius = 0.45f;
    [SerializeField] private float pushAngle = 16f;
    [SerializeField] private float pushSmooth = 12f;
    [SerializeField] private float recoverSmooth = 4f;
    [SerializeField] private float pushedXMoveAmount = 0.04f;
    [SerializeField, Range(0f, 1f)] private float minimumVisiblePush = 0.18f;
    [SerializeField] private float interactionWidth = 0.55f;
    [SerializeField] private float interactionLength = 0.8f;
    [SerializeField] private float movementTrailLength = 0.8f;
    [SerializeField] private float centerlinePushStrength = 1f;
    [SerializeField] private float sidePushStrength = 1f;
    [SerializeField] private float minMovementSpeedForTrail = 0.2f;

    [Header("Push Memory")]
    [SerializeField] private float pushMemoryDuration = 0.35f;
    [SerializeField] private float exitRecoverSmooth = 0.7f;
    [SerializeField, Range(0f, 1f)] private float memoryPushRetention = 0.85f;
    [SerializeField, Range(0.1f, 1f)] private float horizontalRecoverMultiplier = 0.55f;
    [SerializeField, Range(0.1f, 1f)] private float diagonalRecoverMultiplier = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool debugPush;
    [SerializeField] private bool debugThisPlant;
    [SerializeField] private float currentDistance;
    [SerializeField] private float currentTargetPush;
    [SerializeField] private float currentPush;
    [SerializeField] private Vector2 currentPlayerVelocity;

    private static Transform cachedPlayerTransform;
    private static int cachedPlayerPositionFrame = -1;
    private static Vector2 cachedPlayerPushPosition;
    private static Vector2 cachedPreviousPlayerPushPosition;
    private static Vector2 cachedPlayerVelocity;
    private static bool cachedPlayerPositionInitialized;

    private float currentPushDirection;
    private float lastPushTime = -999f;
    private float lastPushAmount;
    private float lastPushDirection;
    private float lastRecoverMultiplier = 1f;

    private Quaternion startRotation;
    private Vector3 startPosition;

    private float angle;
    private float speed;
    private float phase;
    private float noiseSeed;

    private void Awake()
    {
        startRotation = transform.localRotation;
        startPosition = transform.localPosition;

        angle = Random.Range(minAngle, maxAngle);
        speed = Random.Range(minSpeed, maxSpeed);
        phase = Random.Range(0f, Mathf.PI * 2f);
        noiseSeed = Random.Range(0f, 1000f);

        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (!autoFindPlayer || playerTransform != null)
            return;

        if (cachedPlayerTransform != null)
        {
            playerTransform = cachedPlayerTransform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            player = GameObject.Find("Player");

        if (player == null)
        {
            if (debugPush && debugThisPlant)
                Debug.LogWarning($"{name}: RiceSway could not find a GameObject tagged or named Player.", this);

            return;
        }

        cachedPlayerTransform = player.transform;
        playerTransform = cachedPlayerTransform;
    }

    private void UpdateCachedPlayerMotion()
    {
        if (Time.frameCount == cachedPlayerPositionFrame)
            return;

        cachedPlayerPositionFrame = Time.frameCount;

        Vector2 playerPushPosition = (Vector2)playerTransform.position + playerInteractionOffset;

        if (cachedPlayerPositionInitialized && Time.deltaTime > 0f)
            cachedPlayerVelocity = (playerPushPosition - cachedPreviousPlayerPushPosition) / Time.deltaTime;
        else
            cachedPlayerVelocity = Vector2.zero;

        cachedPreviousPlayerPushPosition = playerPushPosition;
        cachedPlayerPushPosition = playerPushPosition;
        cachedPlayerPositionInitialized = true;
    }

    private Vector2 GetPlayerPushPosition()
    {
        UpdateCachedPlayerMotion();
        return cachedPlayerPushPosition;
    }

    private static float GetPositionPushDirection(Vector2 playerToRice, float fallbackSeed)
    {
        if (Mathf.Abs(playerToRice.x) > PushDirectionDeadZone)
            return playerToRice.x < 0f ? 1f : -1f;

        return fallbackSeed % 2f < 1f ? 1f : -1f;
    }

    private Vector2 GetTrailDirection()
    {
        if (cachedPlayerVelocity.sqrMagnitude > 0.0001f)
            return cachedPlayerVelocity.normalized;

        return Vector2.up;
    }

    private float GetDirectionalRecoverMultiplier()
    {
        if (cachedPlayerVelocity.sqrMagnitude < minMovementSpeedForTrail * minMovementSpeedForTrail)
            return 1f;

        Vector2 direction = cachedPlayerVelocity.normalized;

        float horizontalAmount = Mathf.Abs(direction.x);
        float verticalAmount = Mathf.Abs(direction.y);

        float horizontalBlend = Mathf.Clamp01((horizontalAmount - verticalAmount) / 0.35f);
        float diagonalBlend = Mathf.Clamp01(1f - Mathf.Abs(horizontalAmount - verticalAmount) / 0.25f);

        float multiplier = Mathf.Lerp(1f, horizontalRecoverMultiplier, horizontalBlend);
        multiplier = Mathf.Lerp(multiplier, diagonalRecoverMultiplier, diagonalBlend);

        return Mathf.Clamp(multiplier, 0.1f, 1f);
    }

    private void RememberPush()
    {
        lastPushTime = Time.time;
        lastPushAmount = Mathf.Max(currentPush, currentTargetPush);
        lastPushDirection = currentPushDirection;
        lastRecoverMultiplier = GetDirectionalRecoverMultiplier();
    }

    private void RecoverPush()
    {
        bool stillInMemory = Time.time - lastPushTime <= pushMemoryDuration;
        float recoverSpeed = exitRecoverSmooth * lastRecoverMultiplier;

        if (stillInMemory)
        {
            currentPushDirection = lastPushDirection;
            float memoryTarget = lastPushAmount * memoryPushRetention;
            currentPush = Mathf.Lerp(currentPush, memoryTarget, Time.deltaTime * recoverSpeed);
            return;
        }

        currentPush = Mathf.Lerp(currentPush, 0f, Time.deltaTime * recoverSpeed);
    }

    private void UpdatePlayerPush()
    {
        if (playerTransform == null)
        {
            ResolvePlayer();
            currentDistance = float.PositiveInfinity;
            currentTargetPush = 0f;
            currentPlayerVelocity = Vector2.zero;
            RecoverPush();
            return;
        }

        Vector2 ricePosition = transform.position;
        Vector2 playerPosition = GetPlayerPushPosition();
        currentPlayerVelocity = cachedPlayerVelocity;
        Vector2 playerToRice = ricePosition - playerPosition;
        float radius = Mathf.Max(0.01f, pushRadius);
        float trailSpeed = cachedPlayerVelocity.magnitude;
        bool playerMovingEnough = trailSpeed >= minMovementSpeedForTrail;
        float trailFactor = Mathf.InverseLerp(minMovementSpeedForTrail, minMovementSpeedForTrail * 3f, trailSpeed);
        float sideRadius = Mathf.Max(0.01f, interactionWidth);
        float alongRadius = Mathf.Max(0.01f, interactionLength + movementTrailLength * trailFactor);
        float broadRadius = Mathf.Max(radius, alongRadius, sideRadius);
        float sqrDistance = playerToRice.sqrMagnitude;
        float sqrBroadRadius = broadRadius * broadRadius;

        currentDistance = float.PositiveInfinity;
        currentTargetPush = 0f;

        if (playerMovingEnough && sqrDistance <= sqrBroadRadius)
        {
            Vector2 trailDirection = GetTrailDirection();
            Vector2 sideDirection = new Vector2(-trailDirection.y, trailDirection.x);

            float alongDistance = Mathf.Abs(Vector2.Dot(playerToRice, trailDirection));
            float sideDistance = Mathf.Abs(Vector2.Dot(playerToRice, sideDirection));
            float normalizedAlong = alongDistance / alongRadius;
            float normalizedSide = sideDistance / sideRadius;
            float ellipseDistance = normalizedAlong * normalizedAlong + normalizedSide * normalizedSide;

            if (ellipseDistance <= 1f)
            {
                currentDistance = Mathf.Sqrt(sqrDistance);

                float distanceFalloff = Mathf.SmoothStep(1f, 0f, Mathf.Sqrt(ellipseDistance));
                float centerlineFactor = 1f - Mathf.Clamp01(normalizedSide);
                float pushStrength = Mathf.Lerp(sidePushStrength, centerlinePushStrength, centerlineFactor);
                float targetPush = Mathf.Clamp01(distanceFalloff * pushStrength);

                currentTargetPush = Mathf.Max(targetPush, minimumVisiblePush);
                currentPushDirection = GetPositionPushDirection(playerToRice, noiseSeed);
                currentPush = Mathf.Lerp(currentPush, currentTargetPush, Time.deltaTime * pushSmooth);
                RememberPush();
            }
            else
            {
                RecoverPush();
            }
        }
        else
        {
            RecoverPush();
        }

        if (debugPush && debugThisPlant)
        {
            Debug.Log(
                $"{name}: player={playerTransform.name}, distance={currentDistance:F2}, targetPush={currentTargetPush:F2}, currentPush={currentPush:F2}, direction={currentPushDirection:F0}, playerVelocity={currentPlayerVelocity}",
                this);
        }
    }

    private void LateUpdate()
    {
        UpdatePlayerPush();

        float windStrength = 1f;

        if (RiceWindController.Instance != null)
            windStrength = RiceWindController.Instance.WindStrength;

        float time = Time.time * speed + phase;

        float mainWave = Mathf.Sin(time);
        float smallWave = Mathf.Sin(time * 0.47f + phase) * 0.35f;
        float noise = (Mathf.PerlinNoise(noiseSeed, Time.time * noiseSpeed) - 0.5f) * noiseAmount;

        float finalWave = mainWave + smallWave + noise;
        float windAngle = finalWave * angle * windStrength;
        float pushFinalAngle = currentPushDirection * currentPush * pushAngle;

        transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, windAngle + pushFinalAngle);

        float windMoveX = finalWave * xMoveAmount * windStrength;
        float pushMoveX = currentPushDirection * currentPush * pushedXMoveAmount;
        transform.localPosition = startPosition + new Vector3(windMoveX + pushMoveX, 0f, 0f);
    }

    private void OnDisable()
    {
        currentPush = 0f;
        transform.localRotation = startRotation;
        transform.localPosition = startPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, pushRadius));

        if (playerTransform == null)
            return;

        Vector3 playerPushPosition = playerTransform.position + (Vector3)playerInteractionOffset;
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(transform.position, playerPushPosition);
        Gizmos.DrawWireSphere(playerPushPosition, 0.08f);
    }
}
