using UnityEngine;

/// <summary>
/// Replaces only the gameplay interaction that normally enables the active car.
/// It does not alter handling, physics, direction mapping, or sprite selection.
/// </summary>
public sealed class AE86Step1FTestControlBootstrap : MonoBehaviour
{
    [SerializeField] private CarTopDownController testCar;

    private void Awake()
    {
        if (testCar == null)
            testCar = FindFirstObjectByType<CarTopDownController>();

        if (testCar == null)
        {
            Debug.LogError("AE86 Step 1F test car was not found; input control could not be enabled.", this);
            return;
        }

        testCar.EnableControl();
    }
}
