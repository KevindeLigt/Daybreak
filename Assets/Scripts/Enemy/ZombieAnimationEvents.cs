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

    // Add this event at the final frame of PrepareAttack.
    public void PrepareAttackComplete()
    {
        zombieAI?.AnimationEvent_PrepareAttackComplete();
    }

    // Add this event on the contact frame of Attack.
    public void NormalAttackHit()
    {
        zombieAI?.AnimationEvent_NormalAttackHit();
    }

    // Add this event on the contact frame of LungeAttack.
    public void LungeAttackHit()
    {
        zombieAI?.AnimationEvent_LungeAttackHit();
    }

    // Add this event near the final frame of both Attack and LungeAttack.
    public void AttackComplete()
    {
        zombieAI?.AnimationEvent_AttackComplete();
    }

    // Add this event where StumbleFall should hand control to physics.
    public void BeginDeathRagdoll()
    {
        enemyHealth?.AnimationEvent_BeginDeathRagdoll();
    }
}
