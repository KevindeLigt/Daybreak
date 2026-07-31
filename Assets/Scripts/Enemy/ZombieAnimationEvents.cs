using UnityEngine;

/// <summary>
/// Place this component on the same GameObject as the Animator.
/// Animation Events are received here and forwarded to scripts on ZombieRoot.
/// </summary>
public class ZombieAnimationEvents : MonoBehaviour
{
    [SerializeField] private ZombieAIHybrid zombieAI;
    [SerializeField] private EnemyHealth enemyHealth;

    private void Awake()
    {
        if (zombieAI == null)
            zombieAI = GetComponentInParent<ZombieAIHybrid>();

        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>();

        if (zombieAI == null)
        {
            Debug.LogError(
                $"{name}: ZombieAnimationEvents could not find ZombieAIHybrid in a parent.",
                this
            );
        }

        if (enemyHealth == null)
        {
            Debug.LogError(
                $"{name}: ZombieAnimationEvents could not find EnemyHealth in a parent.",
                this
            );
        }
    }

    // Add this event when PrepareAttack reaches its final threatening pose.
    public void PrepareAttackPoseReached()
    {
        zombieAI?.AnimationEvent_PrepareAttackPoseReached();
    }

    // Legacy compatibility. Remove the old event from the clip when possible.
    public void PrepareAttackComplete()
    {
        zombieAI?.AnimationEvent_PrepareAttackPoseReached();
    }

    // Add this event on the contact frame of Attack.
    public void NormalAttackHit()
    {
        zombieAI?.AnimationEvent_NormalAttackHit();
    }

    // Add this event on frame 1 or 2 of LungeAir.
    public void BeginLungeFlight()
    {
        zombieAI?.AnimationEvent_BeginLungeFlight();
    }

    // Add this event near the final frame of the normal Attack only.
    public void AttackComplete()
    {
        zombieAI?.AnimationEvent_AttackComplete();
    }

    // Add this event where LungeImpact should hand control to physics.
    public void BeginLungeRagdoll()
    {
        enemyHealth?.AnimationEvent_BeginLungeRagdoll();
    }

    // Add this event where StumbleFall should hand control to physics.
    public void BeginDeathRagdoll()
    {
        enemyHealth?.AnimationEvent_BeginDeathRagdoll();
    }
}
