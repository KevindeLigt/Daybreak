using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitReaction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private ZombieAIHybrid zombieAI;

    [Header("Hit Strength Thresholds")]
    [Tooltip("Hits below this value cause a light flinch.")]
    public float stumbleThreshold = 5f;

    [Tooltip("Hits at or above this value cause a heavy reaction.")]
    public float heavyHitThreshold = 10f;

    [Header("Reaction Settings")]
    public float lightStunDuration = 0.05f;
    public float stumbleStunDuration = 0.2f;
    public float heavyStunDuration = 0.4f;

    [Header("Debug")]
    public bool debugReactions = false;

    private EnemyHealth health;

    private static readonly int FlinchHash =
        Animator.StringToHash("Flinch");

    private static readonly int StumbleHash =
        Animator.StringToHash("Stumble");

    private static readonly int HeavyHitHash =
        Animator.StringToHash("HeavyHit");

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (zombieAI == null)
            zombieAI = GetComponent<ZombieAIHybrid>();

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: EnemyHitReaction could not find an Animator.",
                this
            );
        }
        else if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                $"{name}: Animator has no Runtime Animator Controller assigned.",
                animator
            );
        }
    }

    public void OnHit(Vector3 hitDirection, float hitStrength)
    {
        if (health != null && health.IsDead)
            return;

        if (hitStrength >= heavyHitThreshold)
        {
            PlayReaction(
                HeavyHitHash,
                "HeavyHit",
                heavyStunDuration
            );
        }
        else if (hitStrength >= stumbleThreshold)
        {
            PlayReaction(
                StumbleHash,
                "Stumble",
                stumbleStunDuration
            );
        }
        else
        {
            PlayReaction(
                FlinchHash,
                "Flinch",
                lightStunDuration
            );
        }
    }

    private void PlayReaction(
        int triggerHash,
        string triggerName,
        float stunDuration
    )
    {
        if (animator != null && animator.enabled)
        {
            animator.SetTrigger(triggerHash);

            if (debugReactions)
            {
                Debug.Log(
                    $"{name}: Triggered {triggerName}",
                    this
                );
            }
        }

        if (zombieAI != null && stunDuration > 0f)
        {
            zombieAI.HitStun(stunDuration);
        }
    }
}