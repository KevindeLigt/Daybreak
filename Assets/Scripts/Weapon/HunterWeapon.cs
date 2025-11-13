using UnityEngine;
using UnityEngine.InputSystem;

public class HunterWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [Tooltip("How many seconds between shots.")]
    public float fireRate = 0.4f;

    [Tooltip("How far the weapon can shoot.")]
    public float range = 100f;

    [Tooltip("Damage dealt per shot.")]
    public float damage = 25f;

    [Header("References")]
    [Tooltip("The camera used for aiming (usually the player's camera).")]
    public Camera playerCamera;

    [Tooltip("Optional impact effect prefab to spawn at hit location.")]
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
        if (playerCamera == null)
        {
            Debug.LogWarning("Player camera not assigned to HunterWeapon!");
            return;
        }

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, range))
        {
            Debug.Log($"Hit {hit.transform.name}");

            // Handle visual impact
            if (impactEffectPrefab)
            {
                GameObject impact = Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }

            // Apply damage if the object has an EnemyHealth component
            if (hit.collider.TryGetComponent(out EnemyHealth enemyHealth))
            {
                enemyHealth.TakeDamage(damage);
            }
        }
    }
}
