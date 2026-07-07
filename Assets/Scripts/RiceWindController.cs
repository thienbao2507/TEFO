using UnityEngine;

public class RiceWindController : MonoBehaviour
{
    public static RiceWindController Instance { get; private set; }

    [Header("Wind")]
    [SerializeField] private float baseWindStrength = 0.8f;
    [SerializeField] private float windVariation = 0.4f;
    [SerializeField] private float windChangeSpeed = 0.25f;

    [Header("Gust")]
    [SerializeField] private float gustStrength = 0.6f;
    [SerializeField] private float gustSpeed = 0.08f;
    [SerializeField] private float gustThreshold = 0.72f;

    public float WindStrength { get; private set; } = 1f;

    private float windSeed;
    private float gustSeed;

    private void Awake()
    {
        Instance = this;
        windSeed = Random.Range(0f, 1000f);
        gustSeed = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        float windNoise = Mathf.PerlinNoise(windSeed, Time.time * windChangeSpeed);
        float gustNoise = Mathf.PerlinNoise(gustSeed, Time.time * gustSpeed);

        float normalWind = baseWindStrength + windNoise * windVariation;
        float gust = Mathf.InverseLerp(gustThreshold, 1f, gustNoise) * gustStrength;

        WindStrength = Mathf.Clamp(normalWind + gust, 0.2f, 2f);
    }
}