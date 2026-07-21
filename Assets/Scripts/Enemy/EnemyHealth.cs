using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyRagdollController))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Stats")]
    public string enemyType = "Unknown";
    public float maxHealth = 100f;

    [Header("Death Settings")]
    [Tooltip("Multiplier applied to the final hit force when the death animation becomes a ragdoll.")]
    public float deathForceMultiplier = 2f;

    [Tooltip("Safety delay used when the StumbleFall animation event is missing.")]
    public float deathRagdollFallbackDelay = 0.65f;

    public float destroyDelay = 10f;

    [Header("Drop Settings")]
    public GameObject healthOrbPrefab;

    [Range(0f, 1f)]
    public float healthOrbDropChance = 0.10f;

    [Header("Hit Feedback")]
    public Renderer enemyRenderer;
    public Color hitColor = Color.white;
    public float flashDuration = 0.1f;

    private float currentHealth;
    private Color originalColor;

    private EnemyRagdollController ragdoll;
    private EnemyEviscerationController eviscerationController;
    private ZombieAIHybrid zombieAI;

    private Coroutine flashRoutine;
    private Coroutine deathFallbackRoutine;

    private bool isDead;
    private bool deathRagdollStarted;
    private Vector3 pendingDeathForce;

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

        // Flinch/Hit animation and hit stun are controlled by EnemyHitReaction.
        // EnemyHealth only owns health, death, flash, and kill bookkeeping.
        StartHitFlash();
    }

    /// <summary>
    /// Called by the shotgun's close-range evisceration check.
    /// Evisceration bypasses StumbleFall and immediately replaces the body with gibs.
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
        pendingDeathForce = force * deathForceMultiplier;

        StopHitFlash();

        // Shared kill bookkeeping: exactly once for both death presentations.
        GameFlowManager.Instance?.EnemyDied();
        KillComboSystem.Instance?.OnEnemyKilled();
        PlayerStatsManager.Instance?.AddKill(enemyType);
        TryDropHealthOrb();

        // Stop navigation, attacks, and idle audio. Normal death keeps the
        // Animator active long enough to play StumbleFall; evisceration
        // disables it immediately in EnemyRagdollController.
        if (zombieAI != null)
            zombieAI.BeginDeathAnimation();

        if (wasEviscerated &&
            ragdoll != null &&
            eviscerationController != null)
        {
            // This also stops AI, navigation, Animator, and the original colliders.
            ragdoll.DisableBodyForEvisceration();
            eviscerationController.Eviscerate(force);
        }
        else
        {
            BeginAnimatedDeath();
        }

        Destroy(gameObject, destroyDelay);
    }

    private void BeginAnimatedDeath()
    {
        if (zombieAI == null)
        {
            BeginDeathRagdoll();
            return;
        }

        if (deathRagdollFallbackDelay > 0f)
        {
            deathFallbackRoutine = StartCoroutine(
                DeathRagdollFallback()
            );
        }
        else
        {
            BeginDeathRagdoll();
        }
    }

    /// <summary>
    /// Called by ZombieAnimationEvents at the chosen transition frame in StumbleFall.
    /// </summary>
    public void AnimationEvent_BeginDeathRagdoll()
    {
        if (!isDead)
            return;

        BeginDeathRagdoll();
    }

    private IEnumerator DeathRagdollFallback()
    {
        yield return new WaitForSeconds(deathRagdollFallbackDelay);
        BeginDeathRagdoll();
    }

    private void BeginDeathRagdoll()
    {
        if (deathRagdollStarted)
            return;

        deathRagdollStarted = true;

        if (deathFallbackRoutine != null)
        {
            StopCoroutine(deathFallbackRoutine);
            deathFallbackRoutine = null;
        }

        if (ragdoll != null)
        {
            ragdoll.EnableRagdoll(pendingDeathForce);
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Cannot begin death ragdoll because EnemyRagdollController is missing.",
                this
            );
        }
    }

    private void StartHitFlash()
    {
        if (enemyRenderer == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(Flash());
    }

    private void StopHitFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (enemyRenderer != null)
            enemyRenderer.material.color = originalColor;
    }

    private IEnumerator Flash()
    {
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
