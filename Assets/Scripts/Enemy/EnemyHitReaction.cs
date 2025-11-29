using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitReaction : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public EnemyRagdollController ragdoll;
    public Rigidbody hipRigidbody;   // optional: main body rigidbody
    public MonoBehaviour aiBehaviour;  // e.g. ZombieAIHybrid
                                       // must have a HitStun(float) method

    [Header("Hit Strength Thresholds")]
    [Tooltip("Below this = light flinch only.")]
    public float stumbleThreshold = 5f;

    [Tooltip("Above this = full temporary ragdoll blast.")]
    public float ragdollThreshold = 10f;

    [Header("Reaction Settings")]
    public float lightStunDuration = 0.05f;
    public float stumbleStunDuration = 0.2f;
    public float heavyStunDuration = 0.4f;

    [Tooltip("Impulse applied on medium hits, before full ragdoll territory.")]
    public float stumbleImpulseMultiplier = 1.5f;

    [Tooltip("Impulse multiplier for heavy hits when triggering TemporaryRagdollBlast.")]
    public float ragdollImpulseMultiplier = 2.5f;

    private EnemyHealth health;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    /// <summary>
    /// Call this when the enemy is hit.
    /// hitDirection: normalized direction away from the attacker.
    /// hitStrength: arbitrary 'power' value (you decide the scale).
    /// </summary>
    public void OnHit(Vector3 hitDirection, float hitStrength)
    {
        if (health != null && health.IsDead)
            return;

        if (hitStrength >= ragdollThreshold)
        {
            HandleHeavyHit(hitDirection, hitStrength);
        }
        else if (hitStrength >= stumbleThreshold)
        {
            HandleStumble(hitDirection, hitStrength);
        }
        else
        {
            HandleLightHit(hitDirection, hitStrength);
        }
    }

    void HandleLightHit(Vector3 hitDirection, float hitStrength)
    {
        // Small animation + micro stun
        if (animator != null)
        {
            animator.SetTrigger("Flinch");
        }

        CallHitStun(lightStunDuration);
    }

    void HandleStumble(Vector3 hitDirection, float hitStrength)
    {
        // Stumble animation + small physical shove + longer stun
        if (animator != null)
        {
            animator.SetTrigger("Stumble");
        }

        if (hipRigidbody != null)
        {
            Vector3 force = hitDirection * hitStrength * stumbleImpulseMultiplier;
            hipRigidbody.AddForce(force, ForceMode.Impulse);
        }

        CallHitStun(stumbleStunDuration);
    }

    void HandleHeavyHit(Vector3 hitDirection, float hitStrength)
    {
        if (ragdoll != null)
        {
            Vector3 force = hitDirection * hitStrength * ragdollImpulseMultiplier;
            // You already have this coroutine in your ragdoll controller
            ragdoll.StartCoroutine(ragdoll.TemporaryRagdollBlast(force));
        }
        else if (hipRigidbody != null)
        {
            // Fallback: big shove without full ragdoll if ragdoll ref is missing
            Vector3 force = hitDirection * hitStrength * ragdollImpulseMultiplier;
            hipRigidbody.AddForce(force, ForceMode.Impulse);
        }

        if (animator != null)
        {
            animator.SetTrigger("HeavyHit");
        }

        CallHitStun(heavyStunDuration);
    }

    void CallHitStun(float duration)
    {
        if (aiBehaviour == null || duration <= 0f)
            return;

        // We assume your AI script has a public HitStun(float) method.
        var method = aiBehaviour.GetType().GetMethod("HitStun");
        if (method != null)
        {
            method.Invoke(aiBehaviour, new object[] { duration });
        }
    }
}
