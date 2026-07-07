using UnityEngine;

public class RiceSway : MonoBehaviour
{
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
    [SerializeField] private float pushRadius = 0.45f;
    [SerializeField] private float pushAngle = 16f;
    [SerializeField] private float pushSmooth = 12f;
    [SerializeField] private float recoverSmooth = 4f;
    [SerializeField] private float pushedXMoveAmount = 0.04f;

    private static Transform cachedPlayerTransform;
    private float currentPush;
    private float currentPushDirection;

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
            return;

        cachedPlayerTransform = player.transform;
        playerTransform = cachedPlayerTransform;
    }

    private void UpdatePlayerPush()
    {
        if (playerTransform == null)
        {
            ResolvePlayer();
            currentPush = Mathf.Lerp(currentPush, 0f, Time.deltaTime * recoverSmooth);
            return;
        }

        Vector2 ricePosition = transform.position;
        Vector2 playerPosition = playerTransform.position;
        float distance = Vector2.Distance(ricePosition, playerPosition);

        float targetPush = Mathf.InverseLerp(pushRadius, 0f, distance);
        targetPush = Mathf.SmoothStep(0f, 1f, targetPush);

        if (targetPush > 0.01f)
        {
            currentPushDirection = playerPosition.x >= ricePosition.x ? 1f : -1f;
            currentPush = Mathf.Lerp(currentPush, targetPush, Time.deltaTime * pushSmooth);
        }
        else
        {
            currentPush = Mathf.Lerp(currentPush, 0f, Time.deltaTime * recoverSmooth);
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
}