using UnityEngine;

public enum CarAE86MoveDirection
{
    Down,
    DownDownRight,
    DownRight,
    RightDownRight,
    Right,
    RightUpRight,
    UpRight,
    UpUpRight,
    Up,
    UpUpLeft,
    UpLeft,
    LeftUpLeft,
    Left,
    LeftDownLeft,
    DownLeft,
    DownDownLeft
}

[RequireComponent(typeof(Rigidbody2D))]
public class CarAE86Controller : MonoBehaviour
{
    private const float MovingSpeedThreshold = 0.05f;
    private const float DirectionEpsilon = 0.0001f;
    private const float InputDeadZone = 0.01f;
    private const float LegacyDirectionStepAngle = 22.5f;
    private const int DirectionSectorCount32 = 32;
    private const float DirectionStepAngle32 = 360f / DirectionSectorCount32;
    private const float DirectionHalfStepAngle32 = DirectionStepAngle32 * 0.5f;
    private const float MaxDirectionSnapHysteresis = DirectionHalfStepAngle32 - 0.01f;

    [Header("Movement")]
    [SerializeField] private bool controlEnabled;
    [SerializeField] private float maxForwardSpeed = 6f;
    [SerializeField] private float maxReverseSpeed = 2.5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float brakeDeceleration = 14f;
    [SerializeField] private float naturalDeceleration = 5f;
    [SerializeField] private float turnSpeed = 220f;
    [SerializeField] private float minSpeedToTurn = 0.08f;
    [SerializeField] private float reverseTurnMultiplier = 0.7f;

    [Header("Handling")]
    [SerializeField] private float traction = 7f;
    [SerializeField] private float lateralGrip = 5f;
    [SerializeField] private float lowSpeedSteerBoost = 1.1f;
    [SerializeField] private float visualSteerLeadAngle = 10f;
    [SerializeField, Range(0f, MaxDirectionSnapHysteresis)] private float directionSnapHysteresis = 2.5f;

    [Header("Physics")]
    [SerializeField] private RigidbodyType2D bodyType = RigidbodyType2D.Dynamic;

    [Header("Runtime")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private Vector2 facingDirection = Vector2.down;
    [SerializeField, Range(0, DirectionSectorCount32 - 1)] private int currentDirectionIndex32 = 24;

    private Rigidbody2D rb;
    private float throttleInput;
    private float steerInput;
    private float facingAngleDegrees = -90f;
    private CarAE86MoveDirection currentMoveDirection = CarAE86MoveDirection.Down;

    public Vector2 MoveInput => new Vector2(steerInput, throttleInput);
    public Vector2 CurrentVelocity => rb == null ? facingDirection * currentSpeed : rb.linearVelocity;
    public Vector2 FacingDirection => facingDirection;
    public float CurrentSpeed => currentSpeed;
    public CarAE86MoveDirection CurrentMoveDirection => currentMoveDirection;
    public int CurrentDirectionIndex32 => currentDirectionIndex32;
    public float CurrentSpeed01 => GetMaxSpeedForCurrentDirection() <= 0f ? 0f : Mathf.Clamp01(Mathf.Abs(currentSpeed) / GetMaxSpeedForCurrentDirection());
    public bool IsMoving => CurrentVelocity.sqrMagnitude > MovingSpeedThreshold * MovingSpeedThreshold;
    public bool IsControlEnabled => controlEnabled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogWarning($"{name}: CarAE86Controller requires a Rigidbody2D.", this);
            return;
        }

        ConfigureRigidbody();
        SetFacingFromDirection(facingDirection.sqrMagnitude > DirectionEpsilon ? facingDirection.normalized : Vector2.down);
        currentSpeed = Vector2.Dot(rb.linearVelocity, facingDirection);
    }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
            ConfigureRigidbody();

        SetFacingFromDirection(Vector2.down);
    }

    private void OnValidate()
    {
        directionSnapHysteresis = Mathf.Clamp(directionSnapHysteresis, 0f, MaxDirectionSnapHysteresis);
        currentDirectionIndex32 = WrapDirectionIndex32(currentDirectionIndex32);
    }

    private void Update()
    {
        if (!controlEnabled)
        {
            throttleInput = 0f;
            steerInput = 0f;
            return;
        }

        throttleInput = Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
        steerInput = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        float deltaTime = Time.fixedDeltaTime;
        Vector2 velocity = rb.linearVelocity;

        currentSpeed = Vector2.Dot(velocity, facingDirection);
        UpdateSpeed(deltaTime);

        float steerSpeedFactor = GetSteerSpeedFactor(Mathf.Max(velocity.magnitude, Mathf.Abs(currentSpeed)));
        UpdateFacing(deltaTime, steerSpeedFactor);

        Vector2 handledVelocity = ApplyVelocityHandling(velocity, deltaTime);
        rb.linearVelocity = handledVelocity;
        currentSpeed = Vector2.Dot(handledVelocity, facingDirection);

        UpdateVisualDirection(steerSpeedFactor);
    }

    private void UpdateSpeed(float deltaTime)
    {
        if (throttleInput > 0f)
        {
            if (currentSpeed < -minSpeedToTurn)
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeDeceleration * deltaTime);
            else
                currentSpeed += acceleration * deltaTime;
        }
        else if (throttleInput < 0f)
        {
            if (currentSpeed > minSpeedToTurn)
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeDeceleration * deltaTime);
            else
                currentSpeed -= acceleration * deltaTime;
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, naturalDeceleration * deltaTime);
        }

        currentSpeed = Mathf.Clamp(currentSpeed, -Mathf.Max(0f, maxReverseSpeed), Mathf.Max(0f, maxForwardSpeed));
    }

    private void UpdateFacing(float deltaTime, float steerSpeedFactor)
    {
        if (Mathf.Abs(steerInput) < InputDeadZone || steerSpeedFactor <= 0f)
            return;

        float directionSign = currentSpeed >= 0f ? 1f : -1f;
        float reverseMultiplier = currentSpeed < 0f ? reverseTurnMultiplier : 1f;
        float angleDelta = -steerInput * turnSpeed * steerSpeedFactor * directionSign * reverseMultiplier * deltaTime;

        facingAngleDegrees = NormalizeAngle(facingAngleDegrees + angleDelta);
        facingDirection = AngleToDirection(facingAngleDegrees);
    }

    private Vector2 ApplyVelocityHandling(Vector2 velocity, float deltaTime)
    {
        Vector2 forwardVelocity = facingDirection * Vector2.Dot(velocity, facingDirection);
        Vector2 sideVelocity = velocity - forwardVelocity;
        Vector2 desiredForwardVelocity = facingDirection * currentSpeed;

        forwardVelocity = Vector2.MoveTowards(forwardVelocity, desiredForwardVelocity, Mathf.Max(0f, traction) * deltaTime);
        sideVelocity = Vector2.MoveTowards(sideVelocity, Vector2.zero, Mathf.Max(0f, lateralGrip) * deltaTime);

        Vector2 handledVelocity = forwardVelocity + sideVelocity;
        float speedLimit = Mathf.Max(0f, maxForwardSpeed, maxReverseSpeed);

        if (speedLimit > 0f && handledVelocity.sqrMagnitude > speedLimit * speedLimit)
            handledVelocity = handledVelocity.normalized * speedLimit;

        return handledVelocity;
    }

    private float GetSteerSpeedFactor(float speedMagnitude)
    {
        if (speedMagnitude <= InputDeadZone)
            return 0f;

        float turnReferenceSpeed = Mathf.Max(0.01f, minSpeedToTurn);
        return Mathf.Clamp01(speedMagnitude / turnReferenceSpeed * Mathf.Max(0f, lowSpeedSteerBoost));
    }

    private void UpdateVisualDirection(float steerSpeedFactor)
    {
        float visualAngle = facingAngleDegrees;

        if (Mathf.Abs(steerInput) >= InputDeadZone && steerSpeedFactor > 0f)
        {
            float directionSign = currentSpeed >= 0f ? 1f : -1f;
            float reverseMultiplier = currentSpeed < 0f ? reverseTurnMultiplier : 1f;
            visualAngle += -steerInput * visualSteerLeadAngle * steerSpeedFactor * directionSign * reverseMultiplier;
        }

        visualAngle = NormalizeAngle(visualAngle);
        int nextDirectionIndex = GetDirectionIndex32(visualAngle);

        if (nextDirectionIndex == currentDirectionIndex32)
            return;

        float currentDirectionCenter = GetDirectionCenterAngle32(currentDirectionIndex32);
        float hysteresis = Mathf.Clamp(directionSnapHysteresis, 0f, MaxDirectionSnapHysteresis);
        float switchThreshold = DirectionHalfStepAngle32 + hysteresis;

        if (Mathf.Abs(Mathf.DeltaAngle(currentDirectionCenter, visualAngle)) >= switchThreshold)
        {
            currentDirectionIndex32 = nextDirectionIndex;
            currentMoveDirection = GetDirectionFromAngle(GetDirectionCenterAngle32(currentDirectionIndex32));
        }
    }

    private void ConfigureRigidbody()
    {
        rb.bodyType = bodyType;
        rb.gravityScale = 0f;
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;

        if (!controlEnabled)
        {
            throttleInput = 0f;
            steerInput = 0f;
        }
    }

    private float GetMaxSpeedForCurrentDirection()
    {
        return currentSpeed < 0f ? maxReverseSpeed : maxForwardSpeed;
    }

    private void SetFacingFromDirection(Vector2 direction)
    {
        facingDirection = direction.sqrMagnitude > DirectionEpsilon ? direction.normalized : Vector2.down;
        facingAngleDegrees = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        facingAngleDegrees = NormalizeAngle(facingAngleDegrees);
        currentDirectionIndex32 = GetDirectionIndex32(facingAngleDegrees);
        currentMoveDirection = GetDirectionFromAngle(facingAngleDegrees);
    }

    private static Vector2 AngleToDirection(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    private static float NormalizeAngle(float angleDegrees)
    {
        angleDegrees %= 360f;

        if (angleDegrees < 0f)
            angleDegrees += 360f;

        return angleDegrees;
    }

    private static CarAE86MoveDirection GetDirectionFromVector(Vector2 direction)
    {
        if (direction.sqrMagnitude <= DirectionEpsilon)
            return CarAE86MoveDirection.Down;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return GetDirectionFromAngle(angle);
    }

    private static CarAE86MoveDirection GetDirectionFromAngle(float angleDegrees)
    {
        int sector = Mathf.RoundToInt(NormalizeAngle(angleDegrees) / LegacyDirectionStepAngle) % 16;

        switch (sector)
        {
            case 0:
                return CarAE86MoveDirection.Right;
            case 1:
                return CarAE86MoveDirection.RightUpRight;
            case 2:
                return CarAE86MoveDirection.UpRight;
            case 3:
                return CarAE86MoveDirection.UpUpRight;
            case 4:
                return CarAE86MoveDirection.Up;
            case 5:
                return CarAE86MoveDirection.UpUpLeft;
            case 6:
                return CarAE86MoveDirection.UpLeft;
            case 7:
                return CarAE86MoveDirection.LeftUpLeft;
            case 8:
                return CarAE86MoveDirection.Left;
            case 9:
                return CarAE86MoveDirection.LeftDownLeft;
            case 10:
                return CarAE86MoveDirection.DownLeft;
            case 11:
                return CarAE86MoveDirection.DownDownLeft;
            case 12:
                return CarAE86MoveDirection.Down;
            case 13:
                return CarAE86MoveDirection.DownDownRight;
            case 14:
                return CarAE86MoveDirection.DownRight;
            case 15:
                return CarAE86MoveDirection.RightDownRight;
            default:
                return CarAE86MoveDirection.Down;
        }
    }

    private static int GetDirectionIndex32(float angleDegrees)
    {
        return Mathf.RoundToInt(NormalizeAngle(angleDegrees) / DirectionStepAngle32) % DirectionSectorCount32;
    }

    private static float GetDirectionCenterAngle32(int directionIndex)
    {
        return WrapDirectionIndex32(directionIndex) * DirectionStepAngle32;
    }

    private static int WrapDirectionIndex32(int directionIndex)
    {
        directionIndex %= DirectionSectorCount32;

        if (directionIndex < 0)
            directionIndex += DirectionSectorCount32;

        return directionIndex;
    }

    private static float GetDirectionCenterAngle(CarAE86MoveDirection direction)
    {
        switch (direction)
        {
            case CarAE86MoveDirection.Right:
                return 0f;
            case CarAE86MoveDirection.RightUpRight:
                return 22.5f;
            case CarAE86MoveDirection.UpRight:
                return 45f;
            case CarAE86MoveDirection.UpUpRight:
                return 67.5f;
            case CarAE86MoveDirection.Up:
                return 90f;
            case CarAE86MoveDirection.UpUpLeft:
                return 112.5f;
            case CarAE86MoveDirection.UpLeft:
                return 135f;
            case CarAE86MoveDirection.LeftUpLeft:
                return 157.5f;
            case CarAE86MoveDirection.Left:
                return 180f;
            case CarAE86MoveDirection.LeftDownLeft:
                return 202.5f;
            case CarAE86MoveDirection.DownLeft:
                return 225f;
            case CarAE86MoveDirection.DownDownLeft:
                return 247.5f;
            case CarAE86MoveDirection.Down:
                return 270f;
            case CarAE86MoveDirection.DownDownRight:
                return 292.5f;
            case CarAE86MoveDirection.DownRight:
                return 315f;
            case CarAE86MoveDirection.RightDownRight:
                return 337.5f;
            default:
                return 270f;
        }
    }
}
