using TMPro;
using UnityEngine;

public class CarHUD : MonoBehaviour
{
    [SerializeField] private PlayerCarSpawner carSpawner;
    [SerializeField] private TMP_Text carInfoText;

    private void Update()
    {
        if (carInfoText == null)
            return;

        CarTopDownController carController = PlayerVehicleInteractor.CurrentDrivenCar;
        GameObject currentCar = null;

        if (carController != null)
            currentCar = carController.gameObject;
        else if (carSpawner != null)
            currentCar = carSpawner.CurrentCar;

        if (currentCar == null)
        {
            carInfoText.text = "No car";
            return;
        }

        if (carController == null)
            carController = currentCar.GetComponent<CarTopDownController>();

        if (carController == null)
            return;

        float speedKmh = carController.Speed * 10f;
        CarHealth carHealth = currentCar.GetComponent<CarHealth>();

        if (carHealth == null)
        {
            carInfoText.text = $"Car: {carController.CarName}\nSpeed: {speedKmh:0} km/h";
            return;
        }

        carInfoText.text = $"Car: {carController.CarName}\nSpeed: {speedKmh:0} km/h\nHealth: {carHealth.CurrentHealth:0}/{carHealth.MaxHealth:0}";
    }
}
