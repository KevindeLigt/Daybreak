using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitReaction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private ZombieAIHybrid zombieAI;

    [Header("Hit Strength")]
    [FormerlySerializedAs("heavyHitThreshold")]
    [Tooltip("Accumulated force at or above this value triggers Hit instead of Flinch.")]
    public float hitThreshold = 8f;

    [Header("Reaction Settings")]
    [FormerlySerializedAs("lightStunDuration")]
    public float flinchStunDuration = 0.08f;

    [FormerlySerializedAs("heavyStunDuration")]
    public float hitStunDuration = 0.28f;

    [Header("Debug")]
    public bool debugReactions = false;

    private EnemyHealth health;
    private Coroutine bufferedReactionRoutine;
    private float accumulatedHitStrength;
    private Vector3 accumulatedHitDirection;

    private static readonly int FlinchHash =
        Animator.StringToHash("Flinch");

    private static readonly int HitHash =
        Animator.StringToHash("Hit");

    private bool hasFlinchParameter;
    private bool hasHitParameter;

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
        else
        {
            hasFlinchParameter = HasTriggerParameter(FlinchHash);
            hasHitParameter = HasTriggerParameter(HitHash);
        }
    }

    /// <summary>
    /// Multiple shotgun pellets arriving in one frame are combined into one reaction.
    /// This prevents one blast from rapidly firing many Animator triggers.
    /// </summary>
    public void OnHit(Vector3 hitDirection, float hitStrength)
    {
        if (health != null && health.IsDead)
            return;

        accumulatedHitStrength += Mathf.Max(0f, hitStrength);
        accumulatedHitDirection += hitDirection.normalized * Mathf.Max(0f, hitStrength);

        if (bufferedReactionRoutine == null)
        {
            bufferedReactionRoutine = StartCoroutine(
                ProcessBufferedReaction()
            );
        }
    }

    private IEnumerator ProcessBufferedReaction()
    {
        // Gather every pellet that struck during this rendered frame.
        yield return new WaitForEndOfFrame();

        float totalStrength = accumulatedHitStrength;
        Vector3 reactionDirection = accumulatedHitDirection.sqrMagnitude > 0.001f
            ? accumulatedHitDirection.normalized
            : -transform.forward;

        accumulatedHitStrength = 0f;
        accumulatedHitDirection = Vector3.zero;
        bufferedReactionRoutine = null;

        if (health != null && health.IsDead)
            yield break;

        if (totalStrength >= hitThreshold)
        {
            PlayReaction(
                HitHash,
                FlinchHash,
                "Hit",
                hitStunDuration,
                reactionDirection,
                totalStrength
            );
        }
        else
        {
            PlayReaction(
                FlinchHash,
                HitHash,
                "Flinch",
                flinchStunDuration,
                reactionDirection,
                totalStrength
            );
        }
    }

    private void PlayReaction(
        int triggerHash,
        int oppositeTriggerHash,
        string triggerName,
        float stunDuration,
        Vector3 hitDirection,
        float hitStrength)
    {
        if (animator != null && animator.enabled)
        {
            bool hasRequestedTrigger = triggerHash == HitHash
                ? hasHitParameter
                : hasFlinchParameter;

            bool hasOppositeTrigger = oppositeTriggerHash == HitHash
                ? hasHitParameter
                : hasFlinchParameter;

            if (hasOppositeTrigger)
                animator.ResetTrigger(oppositeTriggerHash);

            if (hasRequestedTrigger)
                animator.SetTrigger(triggerHash);
        }

        if (zombieAI != null && stunDuration > 0f)
            zombieAI.HitStun(stunDuration);

        if (debugReactions)
        {
            Debug.Log(
                $"{name}: {triggerName} from strength {hitStrength:0.00}, direction {hitDirection}",
                this
            );
        }
    }
    private bool HasTriggerParameter(int parameterHash)
    {
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

}
