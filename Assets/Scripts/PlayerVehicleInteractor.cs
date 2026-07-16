using TMPro;
using UnityEngine;

public class PlayerVehicleInteractor : MonoBehaviour
{


    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private Collider2D playerBodyCollider;
    [SerializeField] private GameObject weaponRoot;
    [SerializeField] private WeaponAttack weaponAttack;

    private bool isDriving;
    private CarTopDownController currentCar;
    [SerializeField] private TMP_Text interactPromptText;
    [SerializeField] private float interactRadius = 1.8f;

    private CarTopDownController nearbyCar;
    public static CarTopDownController CurrentDrivenCar { get; private set; }

    private void Awake()
    {
        if (weaponRoot == null)
        {
            Transform weaponPivot = transform.Find("WeaponPivot");

            if (weaponPivot != null)
                weaponRoot = weaponPivot.gameObject;
        }

        if (weaponAttack == null)
            weaponAttack = GetComponentInChildren<WeaponAttack>(true);
    }

    private void Update()
    {
        DetectNearbyCar();

        if (!Input.GetKeyDown(KeyCode.F))
            return;

        if (isDriving)
        {
            ExitCar();
            return;
        }

        if (nearbyCar != null)
        {
            EnterCar();
        }
    }
    
    private void Start()
    {
        HidePrompt();
    }


    private CarTopDownController GetCarFromCollider(Collider2D other)
    {
        CarTopDownController car = other.GetComponent<CarTopDownController>();

        if (car == null)
            car = other.GetComponentInParent<CarTopDownController>();

        return car;
    }

    private void DetectNearbyCar()
    {
        if (isDriving)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRadius);

        CarTopDownController closestCar = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            CarTopDownController car = GetCarFromCollider(hit);

            if (car == null)
                continue;

            float distance = Vector2.Distance(transform.position, car.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestCar = car;
            }
        }

        nearbyCar = closestCar;

        if (nearbyCar != null)
        {
            ShowPrompt();
        }
        else
        {
            HidePrompt();
        }
    }

    private void ShowPrompt()
    {
        if (interactPromptText == null)
            return;

        interactPromptText.gameObject.SetActive(true);
        interactPromptText.text = "Press F";
    }

    private void HidePrompt()
    {
        if (interactPromptText == null)
            return;

        interactPromptText.gameObject.SetActive(false);
    }

    private void SetWeaponActive(bool active)
    {
        if (active)
        {
            if (weaponRoot != null)
                weaponRoot.SetActive(true);

            if (weaponAttack != null)
                weaponAttack.enabled = true;

            return;
        }

        if (weaponAttack != null)
            weaponAttack.enabled = false;

        if (weaponRoot != null)
            weaponRoot.SetActive(false);
    }

    private void EnterCar()
    {
        currentCar = nearbyCar;
        isDriving = true;
        CurrentDrivenCar = currentCar;

        HidePrompt();

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerRenderer != null)
            playerRenderer.enabled = false;

        if (playerBodyCollider != null)
            playerBodyCollider.enabled = false;

        SetWeaponActive(false);

        currentCar.EnableControl();

        if (cameraFollow != null)
            cameraFollow.SetTarget(currentCar.transform);
    }

    private void ExitCar()
    {
        isDriving = false;

        currentCar.DisableControl();

        transform.position = currentCar.transform.position + currentCar.transform.right * 1.2f;

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerRenderer != null)
            playerRenderer.enabled = true;

        if (playerBodyCollider != null)
            playerBodyCollider.enabled = true;

        SetWeaponActive(true);

        if (cameraFollow != null)
            cameraFollow.SetTarget(transform);

        CurrentDrivenCar = null;
        currentCar = null;
    }
}
