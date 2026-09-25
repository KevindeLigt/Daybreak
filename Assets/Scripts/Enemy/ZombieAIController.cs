using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared entry points for health, reactions, Shoulder Ram and animation events.
/// Each concrete zombie owns its own movement and attack behaviour.
/// Keep only one concrete zombie AI on each root GameObject.
/// </summary>
[DisallowMultipleComponent]
public abstract class ZombieAIController : MonoBehaviour
{
    private Transform encounterFocus;
    private Vector3 encounterOffset;
    private bool encounterAssigned;
    private bool encounterHuntPlayer;
    private bool hasGatherDestination;
    private Vector3 gatherDestination;
    private Vector3 lastFocusPosition;
    private float nextGatherPathAt;
    private NavMeshPath gatherPath;

    // Orders are assigned only to enemies created by the current wave.
    // The concrete controller still owns all movement, attacks and interruption.
    protected bool EncounterMustHunt => encounterAssigned && encounterHuntPlayer;

    public void AssignEncounterFocus(Transform focus, Vector3 offset)
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null && (health.IsDead || health.IsTrainingDummy)) return;
        encounterFocus = focus;
        encounterOffset = offset;
        encounterOffset.y = 0f;
        encounterAssigned = focus != null;
        encounterHuntPlayer = false;
        hasGatherDestination = false;
        nextGatherPathAt = 0f;
    }

    public void PursuePlayerForEncounter()
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        if (!encounterAssigned || (health != null && (health.IsDead || health.IsTrainingDummy))) return;
        encounterHuntPlayer = true;
    }

    public void ClearEncounterOrders()
    {
        encounterAssigned = encounterHuntPlayer = hasGatherDestination = false;
        encounterFocus = null;
    }

    /// <summary>
    /// Used only in the controller's idle/wander state. A stable offset spreads
    /// the group around the heart. Validates reachability before issuing a path.
    /// Returns true when gathering owns this idle frame.
    /// </summary>
    protected bool GatherAtEncounterFocus(NavMeshAgent agent, float movementSpeed)
    {
        if (!encounterAssigned || encounterFocus == null || encounterHuntPlayer) return false;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return true;

        Vector3 focusPosition = encounterFocus.position;
        if ((focusPosition - lastFocusPosition).sqrMagnitude > 0.25f)
            hasGatherDestination = false;

        float arrivalDistance = Mathf.Max(0.25f, agent.stoppingDistance + 0.15f);
        if (hasGatherDestination && !agent.pathPending &&
            Vector3.Distance(agent.nextPosition, gatherDestination) <= arrivalDistance &&
            (!agent.hasPath || agent.remainingDistance <= arrivalDistance))
        {
            agent.isStopped = true;
            if (agent.hasPath) agent.ResetPath();
            return true;
        }

        if (Time.time < nextGatherPathAt) return true;
        nextGatherPathAt = Time.time + 1f;
        if (gatherPath == null) gatherPath = new NavMeshPath();

        // First try the individual spot, then the centre if the offset is in a
        // pew, wall or disconnected patch. Never teleport or accept a partial path.
        if (!FindGatherPath(agent, focusPosition + encounterOffset, out Vector3 destination) &&
            !FindGatherPath(agent, focusPosition, out destination))
        {
            encounterHuntPlayer = true;
            Debug.LogWarning($"{name}: Cannot reach the Root Heart gathering area. " +
                "Switching to player pursuit. Check the NavMesh or assign a Gathering Point on the objective.", this);
            return true;
        }

        agent.speed = Mathf.Max(0f, movementSpeed);
        agent.isStopped = false;
        if (!agent.SetPath(gatherPath))
        {
            encounterHuntPlayer = true;
            Debug.LogWarning($"{name}: Root Heart gathering path could not be assigned. Switching to player pursuit.", this);
            return true;
        }
        gatherDestination = destination;
        lastFocusPosition = focusPosition;
        hasGatherDestination = true;
        return true;
    }

    private bool FindGatherPath(NavMeshAgent agent, Vector3 candidate, out Vector3 destination)
    {
        destination = Vector3.zero;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        // Keep the sample local so a heart on a different floor does not select
        // a distant navigation surface. An optional Gathering Point handles raised props.
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, filter)) return false;
        if (!NavMesh.CalculatePath(agent.nextPosition, hit.position, filter, gatherPath) ||
            gatherPath.status != NavMeshPathStatus.PathComplete) return false;
        destination = hit.position;
        return true;
    }

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
