using System.Collections;
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [Header("Facing Poses")]
    [SerializeField] private Vector3 rightLocalPosition = new Vector3(0.22f, 0.05f, 0f);
    [SerializeField] private Vector3 leftLocalPosition = new Vector3(-0.22f, 0.05f, 0f);
    [SerializeField] private float idleRotationZ = 0f;

    [Header("Attack Rotations")]
    [SerializeField] private float attackStartRotationZ = 35f;
    [SerializeField] private float attackEndRotationZ = -75f;

    [Header("Timing")]
    [SerializeField] private float windUpDuration = 0.05f;
    [SerializeField] private float attackDuration = 0.14f;
    [SerializeField] private float returnDuration = 0.12f;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SpriteRenderer weaponSpriteRenderer;
    [SerializeField] private bool flipWeaponSpriteWhenFacingRight;
    [SerializeField] private bool flipWeaponSpriteWhenFacingLeft;

    private Coroutine attackCoroutine;
    private Vector3 rightLocalScale;
    private Vector3 leftLocalScale;
    private bool isAttacking;
    private bool isFacingLeft;

    private void Awake()
    {
        float scaleMagnitudeX = Mathf.Abs(transform.localScale.x);

        if (scaleMagnitudeX <= Mathf.Epsilon)
            scaleMagnitudeX = 1f;

        rightLocalScale = new Vector3(scaleMagnitudeX, transform.localScale.y, transform.localScale.z);
        leftLocalScale = new Vector3(-scaleMagnitudeX, transform.localScale.y, transform.localScale.z);

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (weaponSpriteRenderer == null)
            weaponSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        ApplyFacing();
    }

    private void OnEnable()
    {
        ApplyFacing();
    }

    private void Update()
    {
        if (!isAttacking)
            ApplyFacing();

        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            attackCoroutine = StartCoroutine(Attack());
        }
    }

    private void OnDisable()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        isAttacking = false;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        if (playerMovement != null)
            isFacingLeft = playerMovement.IsFacingLeft;

        ApplyPose(isFacingLeft);
    }

    private void ApplyPose(bool facingLeft)
    {
        transform.localPosition = facingLeft ? leftLocalPosition : rightLocalPosition;
        transform.localScale = facingLeft ? leftLocalScale : rightLocalScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, idleRotationZ);

        if (weaponSpriteRenderer != null)
            weaponSpriteRenderer.flipX = facingLeft ? flipWeaponSpriteWhenFacingLeft : flipWeaponSpriteWhenFacingRight;
    }

    private IEnumerator Attack()
    {
        isAttacking = true;
        bool attackFacingLeft = playerMovement != null ? playerMovement.IsFacingLeft : isFacingLeft;
        isFacingLeft = attackFacingLeft;

        ApplyPose(attackFacingLeft);

        float startAngle = attackFacingLeft ? attackEndRotationZ : attackStartRotationZ;
        float endAngle = attackFacingLeft ? attackStartRotationZ : attackEndRotationZ;

        Quaternion idleRotation = Quaternion.Euler(0f, 0f, idleRotationZ);
        Quaternion startRotation = Quaternion.Euler(0f, 0f, startAngle);
        Quaternion endRotation = Quaternion.Euler(0f, 0f, endAngle);

        yield return RotateWeapon(idleRotation, startRotation, windUpDuration);
        yield return RotateWeapon(startRotation, endRotation, attackDuration);
        yield return RotateWeapon(endRotation, idleRotation, returnDuration);

        transform.localRotation = idleRotation;
        isAttacking = false;
        attackCoroutine = null;
        ApplyFacing();
    }

    private IEnumerator RotateWeapon(Quaternion fromRotation, Quaternion toRotation, float duration)
    {
        if (duration <= 0f)
        {
            transform.localRotation = toRotation;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            transform.localRotation = Quaternion.Lerp(fromRotation, toRotation, t);
            yield return null;
        }

        transform.localRotation = toRotation;
    }
}
