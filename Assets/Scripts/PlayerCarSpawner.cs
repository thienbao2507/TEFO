using UnityEngine;

public class PlayerCarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] carPrefabs;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private int selectedCarIndex = 0;

    private GameObject currentCar;

    public GameObject CurrentCar
    {
        get
        {
            return currentCar;
        }
    }

    private void Start()
    {
        SpawnCar(selectedCarIndex);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SpawnCar(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            SpawnCar(1);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            SpawnCar(2);
    }

    private void SpawnCar(int carIndex)
    {
        if (carPrefabs == null || carPrefabs.Length == 0 || spawnPoint == null)
            return;

        if (carIndex < 0 || carIndex >= carPrefabs.Length)
            return;

        if (currentCar != null)
        {
            Destroy(currentCar);
        }

        currentCar = Instantiate(carPrefabs[carIndex], spawnPoint.position, spawnPoint.rotation);
    }

}