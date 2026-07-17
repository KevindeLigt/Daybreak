using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyRagdollController))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Stats")]
    public string enemyType = "Unknown";
    public float maxHealth = 100f;

    [Header("Death Settings")]
    [Tooltip("Multiplier applied to the final hit force on a normal ragdoll death.")]
    public float deathForceMultiplier = 2f;

    public float destroyDelay = 10f;

    [Header("Drop Settings")]
    public GameObject healthOrbPrefab;

    [Range(0f, 1f)]
    public float healthOrbDropChance = 0.10f;

    [Header("Hit Feedback")]
    public Renderer enemyRenderer;
    public Color hitColor = Color.white;
    public float flashDuration = 0.1f;
    public float hitStunDuration = 0.15f;

    private float currentHealth;
    private Color originalColor;

    private EnemyRagdollController ragdoll;
    private EnemyEviscerationController eviscerationController;
    private ZombieAIHybrid zombieAI;

    private Coroutine flashRoutine;
    private bool isDead;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;

        ragdoll = GetComponent<EnemyRagdollController>();
        eviscerationController = GetComponent<EnemyEviscerationController>();
        zombieAI = GetComponent<ZombieAIHybrid>();

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
            originalColor = enemyRenderer.material.color;
    }

    public void TakeDamage(float damage, Vector3 force)
    {
        if (isDead || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (currentHealth <= 0f)
        {
            Kill(force, false);
            return;
        }

        // Non-lethal damage stays animated.
        if (zombieAI != null)
            zombieAI.HitStun(hitStunDuration);

        if (enemyRenderer != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(Flash());
        }
    }

    /// <summary>
    /// Called by the shotgun's close-range evisceration check.
    /// </summary>
    public void Eviscerate(Vector3 force)
    {
        if (isDead)
            return;

        currentHealth = 0f;
        Kill(force, true);
    }

    private void Kill(Vector3 force, bool wasEviscerated)
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0f;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        // Cleans up the active attack slot and AI state.
        if (zombieAI != null)
            zombieAI.Die();

        // Shared kill bookkeeping: exactly once.
        GameFlowManager.Instance?.EnemyDied();
        KillComboSystem.Instance?.OnEnemyKilled();
        PlayerStatsManager.Instance?.AddKill(enemyType);
        TryDropHealthOrb();

        if (wasEviscerated && eviscerationController != null)
        {
            if (ragdoll != null)
                ragdoll.DisableBodyForEvisceration();

            eviscerationController.Eviscerate(force);
        }
        else if (ragdoll != null)
        {
            // Safe fallback: if no evisceration component is assigned,
            // use a normal full-body death ragdoll.
            ragdoll.EnableRagdoll(force * deathForceMultiplier);
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Enemy died without an EnemyRagdollController.",
                this
            );
        }

        Destroy(gameObject, destroyDelay);
    }

    private IEnumerator Flash()
    {
        if (enemyRenderer == null)
            yield break;

        enemyRenderer.material.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        if (!isDead && enemyRenderer != null)
            enemyRenderer.material.color = originalColor;

        flashRoutine = null;
    }

    public Transform GetClosestRagdollBone(Vector3 hitPoint)
    {
        if (ragdoll == null || ragdoll.ragdollBodies == null)
            return null;

        float closestDistance = float.MaxValue;
        Transform closestBone = null;

        foreach (Rigidbody body in ragdoll.ragdollBodies)
        {
            if (body == null)
                continue;

            float distance = Vector3.Distance(
                body.transform.position,
                hitPoint
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestBone = body.transform;
            }
        }

        return closestBone;
    }

    private void TryDropHealthOrb()
    {
        if (healthOrbPrefab == null)
            return;

        if (Random.value > healthOrbDropChance)
            return;

        Instantiate(
            healthOrbPrefab,
            transform.position + Vector3.up * 0.5f,
            Quaternion.identity
        );
    }
}
