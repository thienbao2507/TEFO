using UnityEngine;

public class CarTopDownController : MonoBehaviour
{
    [SerializeField] private CarData carData;
    [SerializeField] private bool canControl = false;
    private float acceleration;
    private float maxSpeed;
    private float turnSpeed;
    private float driftFactor;

    private Rigidbody2D rb;
    private float moveInput;
    private float turnInput;

    public string CarName
    {
        get
        {
            if (carData == null)
                return "Unknown Car";

            return carData.carName;
        }
    }

    public float Speed
    {
        get
        {
            if (rb == null)
                return 0f;

            return rb.linearVelocity.magnitude;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (carData != null)
        {
            acceleration = carData.acceleration;
            maxSpeed = carData.maxSpeed;
            turnSpeed = carData.turnSpeed;
            driftFactor = carData.driftFactor;
        }
    }

    private void Update()
    {
        if (!canControl)
        {
            moveInput = 0f;
            turnInput = 0f;
            return;
        }

        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");
    }

    private void FixedUpdate()
    {
        Vector2 forwardForce = transform.up * moveInput * acceleration;
        rb.AddForce(forwardForce, ForceMode2D.Force);

        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        if (rb.linearVelocity.magnitude > 0.2f)
        {
            float rotationAmount = -turnInput * turnSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation + rotationAmount);
        }

        Vector2 forwardVelocity = transform.up * Vector2.Dot(rb.linearVelocity, transform.up);
        Vector2 sideVelocity = transform.right * Vector2.Dot(rb.linearVelocity, transform.right);

        rb.linearVelocity = forwardVelocity + sideVelocity * driftFactor;
    }
    public void EnableControl()
    {
        canControl = true;
    }

    public void DisableControl()
    {
        canControl = false;
        moveInput = 0f;
        turnInput = 0f;
    }
}