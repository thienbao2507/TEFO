using System.Collections;
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [SerializeField] private float attackStartRotationZ = 135f;
    [SerializeField] private float attackEndRotationZ = -60f;
    [SerializeField] private float windUpDuration = 0.05f;
    [SerializeField] private float attackDuration = 0.14f;
    [SerializeField] private float returnDuration = 0.12f;

    private Quaternion idleRotation;
    private bool isAttacking;

    private void Awake()
    {
        idleRotation = transform.localRotation;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            StartCoroutine(Attack());
        }
    }

    private IEnumerator Attack()
    {
        isAttacking = true;

        Quaternion startRotation = Quaternion.Euler(0f, 0f, attackStartRotationZ);
        Quaternion endRotation = Quaternion.Euler(0f, 0f, attackEndRotationZ);

        float timer = 0f;

        while (timer < windUpDuration)
        {
            timer += Time.deltaTime;
            float t = timer / windUpDuration;

            transform.localRotation = Quaternion.Lerp(idleRotation, startRotation, t);
            yield return null;
        }

        timer = 0f;

        while (timer < attackDuration)
        {
            timer += Time.deltaTime;
            float t = timer / attackDuration;
            float angle = Mathf.Lerp(attackStartRotationZ, attackEndRotationZ, t);

            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        timer = 0f;

        while (timer < returnDuration)
        {
            timer += Time.deltaTime;
            float t = timer / returnDuration;

            transform.localRotation = Quaternion.Lerp(endRotation, idleRotation, t);
            yield return null;
        }

        transform.localRotation = idleRotation;
        isAttacking = false;
    }
}