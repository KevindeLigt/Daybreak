using UnityEngine;

/// <summary>
/// Shared entry points for health, reactions, Shoulder Ram and animation events.
/// Each concrete zombie owns its own movement and attack behaviour.
/// Keep only one concrete zombie AI on each root GameObject.
/// </summary>
[DisallowMultipleComponent]
public abstract class ZombieAIController : MonoBehaviour
{
    public abstract bool CanReceiveHitReaction { get; }
    public abstract void HitStun(float duration);
    public abstract void BeginDeathAnimation();
    public virtual void Die() => BeginDeathAnimation();

    public virtual void AnimationEvent_PrepareAttackPoseReached() { }
    public virtual void AnimationEvent_PrepareAttackComplete() { }
    public virtual void AnimationEvent_BeginLungeFlight() { }
    public virtual void AnimationEvent_NormalAttackHit() { }
    public virtual void AnimationEvent_AttackComplete() { }

    // Existing Shambler clips use the same event names. Their no-argument
    // receivers remain intact; the Pursuer also checks which state sent them.
    public virtual void ReceiveNormalAttackHit(AnimationEvent animationEvent)
        => AnimationEvent_NormalAttackHit();
    public virtual void ReceiveAttackComplete(AnimationEvent animationEvent)
        => AnimationEvent_AttackComplete();
    public virtual void ReceivePursuerStepStart(AnimationEvent animationEvent) { }
}
