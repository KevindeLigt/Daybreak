using UnityEngine;
using UnityEngine.InputSystem;

public class HunterWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    public float fireRate = 0.4f;
    public float range = 100f;
    public float damage = 25f;

    [Header("References")]
    public Camera playerCamera;
    public GameObject impactEffectPrefab;

    private float nextFireTime;

    public void Fire(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (Time.time < nextFireTime) return;

        nextFireTime = Time.time + fireRate;
        Shoot();
    }

    private void Shoot()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, range))
        {
            Debug.Log($"Hit {hit.transform.name}");

            if (impactEffectPrefab)
            {
                GameObject impact = Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }
        }
    }
}
