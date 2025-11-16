using UnityEngine;
using System.Collections;

public class ShotgunController : MonoBehaviour
{
    [Header("Shotgun Settings")]
    public int pelletCount = 10;
    public float spreadAngle = 12f;
    public float range = 40f;
    public float damagePerPellet = 10f;
    public float forcePerPellet = 2f; // Lower now because warp pushes more strongly
    public float fireCooldown = 1.0f;
    public float reloadTime = 1.6f;

    [Header("References")]
    public Camera fpsCamera;
    public Transform muzzlePoint;
    public GameObject tracerPrefab;
    public LayerMask hitMask;

    private bool isReloading = false;
    private bool onCooldown = false;

    public void OnFire(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (onCooldown || isReloading) return;

        FireShotgun();
    }

    private void FireShotgun()
    {
        StartCoroutine(FireCooldownRoutine());

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = GetSpreadDirection();
            Vector3 origin = fpsCamera.transform.position;

            RaycastHit hit;
            bool didHit = Physics.Raycast(origin, direction, out hit, range, hitMask);

            // Debug ray (optional)
            Debug.DrawRay(origin, direction * range, didHit ? Color.green : Color.red, 1f);

            // Spawn tracer
            SpawnTracer(direction, didHit ? hit.point : origin + direction * range);

            if (!didHit) continue;

            // Handle damage
            EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
            if (health != null)
            {
                Vector3 force = direction.normalized * forcePerPellet;
                health.TakeDamage(damagePerPellet, force);
            }

        }

        StartCoroutine(ReloadRoutine());
    }

    private Vector3 GetSpreadDirection()
    {
        float x = Random.Range(-spreadAngle, spreadAngle);
        float y = Random.Range(-spreadAngle, spreadAngle);
        Quaternion rot = Quaternion.Euler(x, y, 0);
        return rot * fpsCamera.transform.forward;
    }

    private IEnumerator FireCooldownRoutine()
    {
        onCooldown = true;
        yield return new WaitForSeconds(fireCooldown);
        onCooldown = false;
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        isReloading = false;
    }

    private void SpawnTracer(Vector3 dir, Vector3 endPoint)
    {
        if (tracerPrefab == null || muzzlePoint == null) return;

        GameObject tracer = Instantiate(tracerPrefab, muzzlePoint.position, Quaternion.identity);
        var lr = tracer.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.SetPosition(0, muzzlePoint.position);
            lr.SetPosition(1, endPoint);
        }
        Destroy(tracer, 0.2f);
    }
}
