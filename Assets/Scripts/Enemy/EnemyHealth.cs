using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Stats")]
    public string enemyType = "Unknown";
    public float maxHealth = 100f;

    [Header("Death Settings")]
    [Tooltip("Multiplier applied to the final hit force when entering death ragdoll.")]
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
    private ZombieAIHybrid zombieAI;

    private Coroutine flashRoutine;
    private bool isDead;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;

        ragdoll = GetComponent<EnemyRagdollController>();
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

        // Lethal damage goes directly into permanent death ragdoll.
        if (currentHealth <= 0f)
        {
            Die(force);
            return;
        }

        // Non-lethal hits stay animated.
        if (zombieAI != null)
            zombieAI.HitStun(hitStunDuration);

        if (enemyRenderer != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(Flash());
        }

        // No temporary ragdoll here.
        // EnemyHitReaction handles Flinch / Stumble / HeavyHit animations.
    }

    public void Eviscerate(Vector3 force)
    {
        if (isDead)
            return;

        currentHealth = 0f;
        Die(force);
    }

    private IEnumerator Flash()
    {
        enemyRenderer.material.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        if (!isDead && enemyRenderer != null)
            enemyRenderer.material.color = originalColor;

        flashRoutine = null;
    }

    private void Die(Vector3 force)
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

        // Cleans up AI state, including attack-slot ownership.
        if (zombieAI != null)
            zombieAI.Die();

        GameFlowManager.Instance?.EnemyDied();
        KillComboSystem.Instance?.OnEnemyKilled();

        TryDropHealthOrb();
        PlayerStatsManager.Instance?.AddKill(enemyType);

        if (ragdoll != null)
        {
            // Uses the corrected ragdoll controller that applies one impulse
            // to the central pelvis instead of every individual body.
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