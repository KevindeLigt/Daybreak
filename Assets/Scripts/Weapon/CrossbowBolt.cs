using UnityEngine;

public class CrossbowBolt : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 80f;
    public float force = 25f;     // how hard the bolt knocks enemies over

    [Header("Lifetime")]
    public float maxLifetime = 6f;

    [Header("Hit VFX")]
    public GameObject hitVFX;

    [HideInInspector]
    public CrossbowController shooter;

    private bool hasHit = false;

    void Start()
    {
        Destroy(gameObject, maxLifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        Vector3 hitPoint = collision.contacts[0].point;
        Vector3 hitNormal = collision.contacts[0].normal;

        // Enemy hit?
        EnemyHealth enemy = collision.collider.GetComponentInParent<EnemyHealth>();
        PlayerStatsManager.Instance.AddCrossbowHit();
        PlayerStatsManager.Instance.AddDamageDealt(damage);
        if (enemy != null)
        {
            OnHitEnemy(enemy, collision, hitPoint, hitNormal);
            return;
        }

        // Environment hit
        StickInSurface(hitPoint, hitNormal);
    }

    private void OnHitEnemy(EnemyHealth enemy, Collision collision, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Apply modified damage
        float finalDamage = PlayerWeaponStats.Instance != null
            ? PlayerWeaponStats.Instance.CalculatePelletDamage(damage)
            : damage;

        // Directional force
        Vector3 hitDir = collision.relativeVelocity.normalized;

        enemy.TakeDamage(finalDamage, hitDir * force);

        // Hit VFX
        if (hitVFX)
            Instantiate(hitVFX, hitPoint, Quaternion.LookRotation(hitNormal));

        // Trigger hit reaction
        var react = enemy.GetComponent<EnemyHitReaction>();
        if (react)
            react.OnHit(hitDir, force * 0.5f);

        // Stick bolt into ragdoll
        StickInRagdoll(enemy, hitPoint);

        // Prevent further collisions
        GetComponent<Rigidbody>().isKinematic = true;
        GetComponent<Collider>().enabled = false;

        // Bolt stays stuck until ragdoll is destroyed
        Destroy(gameObject, 8f);
    }

    private void StickInRagdoll(EnemyHealth enemy, Vector3 hitPoint)
    {
        Transform closestBone = enemy.GetClosestRagdollBone(hitPoint);
        if (closestBone)
        {
            transform.position = hitPoint;
            transform.forward = -enemy.transform.forward; // looks good on impact
            transform.SetParent(closestBone, true);
        }
        else
        {
            transform.position = hitPoint;
            transform.SetParent(enemy.transform, true);
        }
    }

    private void StickInSurface(Vector3 hitPoint, Vector3 hitNormal)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
            rb.isKinematic = true;

        Collider col = GetComponent<Collider>();
        if (col)
            col.enabled = false;

        transform.position = hitPoint;
        transform.rotation = Quaternion.LookRotation(-hitNormal);

        Destroy(gameObject, 6f);
    }
}
