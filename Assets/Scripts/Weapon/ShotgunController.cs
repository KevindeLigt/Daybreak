using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class DoubleBarrelShotgunController : MonoBehaviour
{
    [Header("Shotgun Settings")]
    public int pelletsPerShot = 12;
    public float spreadAngle = 6f;
    public float range = 40f;
    public float damagePerPellet = 10f;
    public float forcePerPellet = 3f;

    [Header("Reloading")]
    public float openTime = 0.4f;
    public float loadShellTime = 0.6f;
    public float closeTime = 0.4f;

    [Header("References")]
    public Camera fpsCamera;
    public Transform leftBarrel;
    public Transform rightBarrel;
    public GameObject tracerPrefab;
    public LayerMask hitMask;

    private int shellsLoaded = 2;   // 0 to 2
    private bool isReloading = false;
    private bool isFiring = false;
    private bool fireLeftNext = true;

    private RecoilSystem recoil;
    private CameraShake cameraShake;
    private WeaponSway weaponSway;

    void Start()
    {
        recoil = fpsCamera.GetComponent<RecoilSystem>();
        cameraShake = fpsCamera.GetComponent<CameraShake>();
        weaponSway = GetComponentInChildren<WeaponSway>();
    }


    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        TryFire();
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (!isReloading) StartCoroutine(ReloadRoutine());
    }

    private void TryFire()
    {
        if (isReloading) return;

        if (shellsLoaded <= 0)
        {
            // Dry fire sound here
            Debug.Log("CLICK! Empty!");
            return;
        }

        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        isFiring = true;

        Transform muzzle = fireLeftNext ? leftBarrel : rightBarrel;
        fireLeftNext = !fireLeftNext;

        FirePellets(muzzle);

        if (recoil) recoil.FireRecoil();
        if (cameraShake) cameraShake.Shake(0.1f, 0.25f);
        if (weaponSway) weaponSway.Kick();



        shellsLoaded--;
        UIManager.Instance.UpdateShotgunAmmo(shellsLoaded);

        yield return new WaitForSeconds(0.1f); // optionally time between barrels
        isFiring = false;
    }

    private void FirePellets(Transform muzzle)
    {
        for (int i = 0; i < pelletsPerShot; i++)
        {
            Vector3 direction = GetSpreadDirection();

            if (Physics.Raycast(fpsCamera.transform.position, direction, out RaycastHit hit, range, hitMask))
            {
                // Tracer from the barrel to the hit point
                SpawnTracer(muzzle.position, hit.point);

                // Apply damage + ragdoll force
                EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
                if (health != null)
                {
                    Vector3 force = direction.normalized * forcePerPellet;
                    health.TakeDamage(damagePerPellet, force);
                }
            }
            else
            {
                // miss tracer
                SpawnTracer(muzzle.position, fpsCamera.transform.position + direction * range);
            }
        }
    }

    private Vector3 GetSpreadDirection()
    {
        float x = Random.Range(-spreadAngle, spreadAngle);
        float y = Random.Range(-spreadAngle, spreadAngle);
        return Quaternion.Euler(x, y, 0) * fpsCamera.transform.forward;
    }

    private void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (tracerPrefab == null)
            return;

        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);

        GameObject tracer = Instantiate(tracerPrefab, start, Quaternion.LookRotation(direction));

        // Scale along Z-axis instead of Y
        tracer.transform.localScale = new Vector3(
            tracer.transform.localScale.x,
            tracer.transform.localScale.y,
            distance * 0.5f
        );

        // Move tracer to midpoint
        tracer.transform.position = start + direction * (distance * 0.5f);
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        Debug.Log("Opening shotgun...");
        yield return new WaitForSeconds(openTime);

        while (shellsLoaded < 2)
        {
            Debug.Log("Loading shell...");
            shellsLoaded++;
            UIManager.Instance.UpdateShotgunAmmo(shellsLoaded);
            yield return new WaitForSeconds(loadShellTime);
        }

        Debug.Log("Closing shotgun...");
        yield return new WaitForSeconds(closeTime);

        isReloading = false;
    }
}
