using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Temporary, collision-checked displacement for a living zombie. ShoulderRam
/// adds this component on first contact; no enemy prefab changes are required.
/// The existing AI HitStun owns attack cancellation and resuming pursuit.
/// </summary>
[DisallowMultipleComponent]
public class EnemyRamPush : MonoBehaviour
{
    private EnemyHealth health;
    private ZombieAIController ai;
    private NavMeshAgent agent;
    private Animator animator;
    private ShoulderRam source;
    private int activation;
    private bool hasActivation;
    private bool pushing;
    private bool ownsRootMotion;
    private bool savedRootMotion;
    private Vector3 direction;
    private float distance;
    private float duration;
    private float elapsed;
    private float requestedDistance;
    private float interruptionEndsAt;
    private int advancedFrame = -1;
    private float advancedSeconds;
    private LayerMask obstacleMask;
    private Transform playerRoot;

    public bool IsPushing => pushing;

    private void CacheReferences()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (ai == null) ai = GetComponent<ZombieAIController>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
    }

    private bool CanPush => health != null && !health.IsDead && !health.IsTrainingDummy &&
        ai != null && ai.isActiveAndEnabled && ai.CanReceiveHitReaction &&
        agent != null && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink &&
        agent.updatePosition;

    /// <summary>
    /// Contact from one ram interrupts once and grants one displacement budget.
    /// Further contacts steer the ongoing push; they never refill that budget.
    /// Advance immediately so the player's next step sees the displaced body.
    /// </summary>
    public bool ApplyPressure(ShoulderRam ram, int ramActivation, Vector3 pushDirection,
        float pushDistance, float pushDuration, float interruptDuration,
        LayerMask worldMask, float stepSeconds)
    {
        CacheReferences();
        if (!isActiveAndEnabled || !CanPush) return false;

        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude < 0.0001f) return false;
        pushDirection.Normalize();

        bool firstContact = !hasActivation || source != ram || activation != ramActivation;
        if (firstContact)
        {
            FinishPush(true);
            source = ram;
            activation = ramActivation;
            hasActivation = true;
            playerRoot = ram.transform;
            obstacleMask = worldMask;
            distance = Mathf.Max(0f, pushDistance);
            duration = Mathf.Max(0.05f, pushDuration);
            elapsed = requestedDistance = 0f;
            advancedFrame = -1;
            advancedSeconds = 0f;
            interruptionEndsAt = Time.time + Mathf.Max(duration, interruptDuration);

            // Cancel preparation/strike immediately, before any queued contact
            // event can damage the player. Animation reactions happen separately.
            ai.HitStun(Mathf.Max(duration, interruptDuration));
            agent.isStopped = true;
            agent.ResetPath();

            if (animator != null && animator.enabled)
            {
                savedRootMotion = animator.applyRootMotion;
                animator.applyRootMotion = false;
                ownsRootMotion = true;
            }
            pushing = distance > 0f;
        }

        direction = pushDirection;
        if (pushing) AdvanceForFrame(Mathf.Max(0f, stepSeconds));
        else FinishPush();
        return true;
    }

    private void LateUpdate()
    {
        if (pushing)
        {
            ResetFrameBudget();
            // ShoulderRam may already have advanced this enemy in small steps.
            // Consume only the rest of the frame, so contact does not double speed.
            AdvanceForFrame(Mathf.Max(0f, Time.deltaTime - advancedSeconds));
        }
        if (ownsRootMotion && (Time.time >= interruptionEndsAt || !CanPush ||
            source == null || !source.isActiveAndEnabled)) FinishPush(true);
    }

    private void ResetFrameBudget()
    {
        if (advancedFrame == Time.frameCount) return;
        advancedFrame = Time.frameCount;
        advancedSeconds = 0f;
    }

    private void AdvanceForFrame(float seconds)
    {
        ResetFrameBudget();
        seconds = Mathf.Min(seconds, Mathf.Max(0f, Time.deltaTime - advancedSeconds));
        if (!CanPush || source == null || !source.isActiveAndEnabled)
        {
            FinishPush(true);
            return;
        }
        if (seconds <= 0f) return;
        advancedSeconds += seconds;

        float nextElapsed = Mathf.Min(duration, elapsed + seconds);
        float t = nextElapsed / duration;
        float nextRequested = distance * (1f - (1f - t) * (1f - t));
        float movement = Mathf.Max(0f, nextRequested - requestedDistance);
        elapsed = nextElapsed;
        requestedDistance = nextRequested;

        if (movement > 0.00001f)
        {
            Vector3 start = agent.nextPosition;
            float allowed = movement;
            if (agent.Raycast(start + direction * movement, out NavMeshHit edge))
                allowed = Mathf.Min(allowed, Mathf.Max(0f,
                    Vector3.Dot(edge.position - start, direction) - RamCollision.Skin));

            float radius = Mathf.Max(0.05f, agent.radius);
            float height = Mathf.Max(radius * 2f, agent.height);
            Vector3 bottom = transform.position + Vector3.up * (radius + RamCollision.Skin);
            Vector3 top = bottom + Vector3.up * Mathf.Max(0f,
                height - radius * 2f - RamCollision.Skin * 2f);
            allowed = RamCollision.LimitWorldMove(bottom, top, radius, direction,
                allowed, obstacleMask, transform, playerRoot);

            if (allowed > 0.00001f)
            {
                agent.Move(direction * allowed);
                // Follow the agent's constrained result. This makes the collider
                // available to the player's next substep in this same frame.
                transform.position = agent.nextPosition;
            }
            if (allowed < movement - 0.0001f)
            {
                FinishPush();
                return;
            }
        }
        if (elapsed >= duration) FinishPush();
    }

    private void FinishPush(bool releaseMotion = false)
    {
        pushing = false;
        if (!releaseMotion && Time.time < interruptionEndsAt) return;
        // Death/ragdoll now owns the Animator. Never restore it over that handoff.
        if (ownsRootMotion && animator != null && animator.enabled &&
            health != null && !health.IsDead)
            animator.applyRootMotion = savedRootMotion;
        ownsRootMotion = false;
        // Leave navigation stopped. The AI's stun state will resume it when ready.
    }

    private void OnDisable()
    {
        FinishPush(true);
        hasActivation = false;
        source = null;
    }
}

/// <summary>Shared physical wall checks; living enemy bodies are handled by the bubble.</summary>
internal static class RamCollision
{
    internal const float Skin = 0.03f;

    internal static bool IsWorldCollider(Collider collider, Transform self, Transform player)
    {
        if (collider == null || collider.isTrigger) return false;
        if (self != null && collider.transform.IsChildOf(self)) return false;
        if (player != null && collider.transform.IsChildOf(player)) return false;
        return collider.GetComponentInParent<EnemyHealth>() == null;
    }

    internal static float LimitWorldMove(Vector3 bottom, Vector3 top, float radius,
        Vector3 direction, float distance, LayerMask mask, Transform self, Transform player)
    {
        if (distance <= 0f) return 0f;
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, direction,
            distance, mask, QueryTriggerInteraction.Ignore))
        {
            if (!IsWorldCollider(hit.collider, self, player)) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - Skin));
        }
        return distance;
    }

    internal static bool HasWorldBetween(Vector3 from, Vector3 to, LayerMask mask,
        Transform player, Transform enemy)
    {
        Vector3 line = to - from;
        if (line.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(from, line.normalized,
            line.magnitude, mask, QueryTriggerInteraction.Ignore))
            if (IsWorldCollider(hit.collider, enemy, player)) return true;
        return false;
    }
}
