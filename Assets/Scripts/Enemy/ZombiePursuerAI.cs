using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Grounded Pursuer prototype: approach, moving preparation, committed strike,
/// contact and recovery. EnemyHealth owns health, rewards and ragdoll handoff.
/// Use the supplied editor setup command to create its private Animator/clips.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class ZombiePursuerAI : ZombieAIController
{
    [Header("References")]
    public Transform target;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip preparationSound;

    [Header("Awareness")]
    [Min(0f)] public float detectionRadius = 18f;
    [Min(0f)] public float loseInterestRadius = 24f;

    [Header("Approach")]
    [Tooltip("Prototype starting point for a player walking at 4 m/s. No automatic scaling with player speed.")]
    [Min(0f)] public float chaseSpeed = 5.2f;
    [Min(0.1f)] public float acceleration = 20f;
    [Min(1f)] public float turnSpeed = 360f;
    [Min(0.02f)] public float pathUpdateInterval = 0.1f;

    [Header("Moving Preparation")]
    [Tooltip("Begin the visible wind-up here, while still approaching.")]
    [Min(0.1f)] public float preparationRange = 3f;
    [Tooltip("Separate close-range gate for committing to the strike.")]
    [Min(0.1f)] public float commitmentRange = 1.75f;
    [Min(0f)] public float preparationSpeed = 5.2f;
    [Min(0.05f)] public float minimumPreparationTime = 0.45f;
    [Tooltip("Lower the attack and resume approach if commitment is not possible within this time.")]
    [Min(0.1f)] public float maximumPreparationTime = 1.4f;
    [Range(1f, 89f)] public float commitmentHalfAngle = 30f;

    [Header("Strike and Recovery")]
    [Min(0f)] public float attackDamage = 20f;
    [Tooltip("Maximum forward travel between PursuerStepStart and NormalAttackHit in the Attack clip.")]
    [Min(0f)] public float strikeDistance = 1.5f;
    [Min(0.1f)] public float hitReach = 1.6f;
    [Range(1f, 89f)] public float hitHalfAngle = 50f;
    [Min(0f)] public float verticalTolerance = 1.25f;
    [Min(0f)] public float recoveryTime = 0.6f;
    [Tooltip("Minimum time between commitments. Stun also consumes this cooldown.")]
    [Min(0f)] public float attackCooldown = 1.4f;
    [Tooltip("Cancel a missing contact/completion event without applying fallback damage.")]
    [Min(0.1f)] public float animationEventTimeout = 2f;

    [Header("Contact and Interruption")]
    [Tooltip("Solid environment and player layers. Triggers are ignored. Living zombies block the step but do not shield melee contact.")]
    public LayerMask obstacleMask = ~0;
    [Min(0f)] public float defaultStunTime = 0.2f;
    public bool debugAttackLogs;

    private enum Phase { Idle, Approach, Prepare, Strike, Recovery, Stunned, Dead }
    [SerializeField] private Phase phase = Phase.Idle;
    public string CurrentPhase => phase.ToString();
    // Training mode must also reject animation events that arrive between
    // Updates, even if a component was accidentally left enabled.
    public override bool CanReceiveHitReaction => !dead &&
        (health == null || (!health.IsDead && !health.IsTrainingDummy));

    private NavMeshAgent agent;
    private EnemyHealth health;
    private bool initialized;
    private bool dead;
    private bool configuredAnimator;
    private bool warnedNavigation;
    private bool warnedEvents;
    private bool ownsMovement;
    private bool savedRotation;
    private bool savedRootMotion;
    private float savedSpeed, savedAcceleration, savedStoppingDistance, savedAngularSpeed;
    private float phaseStartedAt, nextPathAt, nextTargetSearchAt, nextAttackAt, prepareRetryAt;
    private float stunEndsAt, recoveryEndsAt;
    private Vector3 strikeDirection;
    private bool stepStarted, stepBlocked, contactResolved, completionReceived;
    private float stepStartNormalized, stepEndNormalized, stepProgress;
    private int attackNumber;

    private static readonly int MoveHash = Animator.StringToHash("MoveSpeed");
    private static readonly int PrepareHash = Animator.StringToHash("PrepareAttack");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int CancelHash = Animator.StringToHash("CancelAttack");
    private static readonly int DeathHash = Animator.StringToHash("StumbleFall");
    private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Attack");
    private bool hasMove, hasPrepare, hasAttack, hasCancel, hasDeath;
    private bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;
    private float PhaseAge => Time.time - phaseStartedAt;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == MoveHash) hasMove = true;
                if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
                if (parameter.nameHash == PrepareHash) hasPrepare = true;
                if (parameter.nameHash == AttackHash) hasAttack = true;
                if (parameter.nameHash == CancelHash) hasCancel = true;
                if (parameter.nameHash == DeathHash) hasDeath = true;
            }
            configuredAnimator = hasMove && hasPrepare && hasAttack && hasCancel && animator.HasState(0, AttackStateHash);
        }
        initialized = true;
        if (!configuredAnimator && (health == null || !health.IsTrainingDummy))
            Debug.LogWarning($"{name}: Configure the Pursuer Animator using the ZombiePursuerAI component menu before testing attacks.", this);
    }

    private void OnEnable()
    {
        Initialize();
        if (!CanReceiveHitReaction) return;
        TakeMovementControl();
        FindTarget();
        Enter(Phase.Idle);
    }

    private void OnDisable()
    {
        if (!initialized) return;
        StopAgent();
        ResetAttackTriggers();
        contactResolved = true;
        stepStarted = false;
        nextAttackAt = Time.time + Mathf.Max(0f, attackCooldown);
        if (!dead) phase = Phase.Idle;
        RestoreMovementControl();
    }

    private void Update()
    {
        if (dead) return;
        if (health != null && health.IsDead) { BeginDeathAnimation(); return; }
        if (target == null) FindTarget();
        if (health != null && health.IsTrainingDummy) { StopAgent(); return; }
        if (!CanNavigate || target == null || !target.gameObject.activeInHierarchy)
        {
            if (phase == Phase.Prepare || phase == Phase.Strike || phase == Phase.Recovery)
                CancelAttack("target/navigation unavailable");
            StopAgent();
            UpdateAnimation();
            if (!CanNavigate && !warnedNavigation)
            {
                Debug.LogWarning($"{name}: Pursuer needs an enabled agent on a baked NavMesh. AI is paused.", this);
                warnedNavigation = true;
            }
            return;
        }
        warnedNavigation = false;
        switch (phase)
        {
            case Phase.Idle:
                if (EncounterMustHunt || (DistanceToTarget() <= Mathf.Max(0f, detectionRadius) && !HasCover()))
                    Enter(Phase.Approach);
                else if (GatherAtEncounterFocus(agent, chaseSpeed))
                    FaceDirection(agent.desiredVelocity);
                break;
            case Phase.Approach: UpdateApproach(); break;
            case Phase.Prepare: UpdatePreparation(); break;
            case Phase.Strike:
                UpdateStrikeStep();
                if (PhaseAge >= Mathf.Max(0.1f, animationEventTimeout))
                {
                    WarnEvents("NormalAttackHit did not arrive; no fallback damage was applied.");
                    BeginRecovery();
                }
                break;
            case Phase.Recovery:
                if (Time.time >= recoveryEndsAt && completionReceived) Enter(Phase.Approach);
                else if (PhaseAge >= Mathf.Max(recoveryTime, animationEventTimeout))
                {
                    WarnEvents("AttackComplete did not arrive; resuming approach after recovery.");
                    Enter(Phase.Approach);
                }
                break;
            case Phase.Stunned:
                if (Time.time >= stunEndsAt) Enter(Phase.Approach);
                break;
        }
        UpdateAnimation();
    }

    private void Enter(Phase next)
    {
        if (dead && next != Phase.Dead) return;
        phase = next;
        phaseStartedAt = Time.time;
        nextPathAt = 0f;
        switch (next)
        {
            case Phase.Idle:
            case Phase.Approach:
                CancelAttackAnimation();
                if (next == Phase.Idle) StopAgent();
                break;
            case Phase.Prepare:
                ResetAttackTriggers();
                animator.SetTrigger(PrepareHash);
                attackNumber++;
                if (audioSource != null && preparationSound != null) audioSource.PlayOneShot(preparationSound);
                LogAttack($"preparing at {DistanceToTarget():F2} m");
                break;
            case Phase.Strike:
                StopAgent();
                strikeDirection = transform.forward;
                strikeDirection.y = 0f;
                strikeDirection.Normalize();
                stepStarted = stepBlocked = contactResolved = completionReceived = false;
                stepProgress = 0f;
                nextAttackAt = Time.time + Mathf.Max(0f, attackCooldown);
                ResetAttackTriggers();
                animator.SetTrigger(AttackHash);
                LogAttack($"committed at {DistanceToTarget():F2} m; direction is now fixed");
                break;
            case Phase.Recovery:
                StopAgent();
                break;
            case Phase.Stunned:
            case Phase.Dead:
                StopAgent();
                ResetAttackTriggers();
                break;
        }
    }

    private void UpdateApproach()
    {
        float distance = DistanceToTarget();
        if (!EncounterMustHunt && distance > Mathf.Max(detectionRadius, loseInterestRadius)) { Enter(Phase.Idle); return; }
        FollowTarget(chaseSpeed);
        // Face along the path around corners instead of sliding sideways while
        // staring through a wall. Preparation uses direct, visible facing.
        FaceDirection(agent.desiredVelocity.sqrMagnitude > 0.01f
            ? agent.desiredVelocity : target.position - transform.position);
        if (configuredAnimator && animator.enabled && Time.time >= nextAttackAt && Time.time >= prepareRetryAt &&
            distance <= Mathf.Max(preparationRange, commitmentRange) && WithinHeight() && !HasCover())
            Enter(Phase.Prepare);
    }

    private void UpdatePreparation()
    {
        FaceTarget();
        FollowTarget(preparationSpeed);
        if (DistanceToTarget() > Mathf.Max(preparationRange, commitmentRange) + 0.75f ||
            !WithinHeight() || HasCover() || !animator.enabled)
        { CancelAttack("preparation lost its target or clear approach"); return; }

        float minimum = Mathf.Max(0.05f, minimumPreparationTime);
        if (PhaseAge >= minimum && DistanceToTarget() <= Mathf.Max(0.1f, commitmentRange) &&
            WithinFacing(transform.forward, commitmentHalfAngle))
        { Enter(Phase.Strike); return; }

        if (PhaseAge >= Mathf.Max(minimum, maximumPreparationTime))
            CancelAttack("preparation timed out before commitment was possible");
    }

    private void CancelAttack(string reason)
    {
        LogAttack(reason);
        contactResolved = true;
        stepStarted = false;
        prepareRetryAt = Time.time + 0.35f;
        Enter(Phase.Approach);
    }

    public override void ReceivePursuerStepStart(AnimationEvent animationEvent)
    {
        if (!CanAcceptAttackEvent(animationEvent) || stepStarted || contactResolved) return;
        AnimationClip clip = animationEvent.animatorClipInfo.clip;
        if (clip == null || clip.length <= 0f) return;
        float start = animationEvent.time / clip.length;
        float end = animationEvent.floatParameter;
        if (end <= start || end > 1f)
        { WarnEvents("PursuerStepStart has invalid contact timing. Run the Animator setup again."); return; }
        stepStartNormalized = start;
        stepEndNormalized = end;
        stepStarted = true;
        UpdateStrikeStep();
    }

    private bool CanAcceptAttackEvent(AnimationEvent animationEvent)
    {
        return isActiveAndEnabled && CanReceiveHitReaction && CanNavigate && phase == Phase.Strike &&
            animator != null && animator.enabled && IsAttackEvent(animationEvent);
    }

    private static bool IsAttackEvent(AnimationEvent animationEvent)
    {
        return animationEvent != null && animationEvent.isFiredByAnimator &&
            animationEvent.animatorStateInfo.fullPathHash == AttackStateHash &&
            animationEvent.animatorStateInfo.normalizedTime < 1.05f;
    }

    private void UpdateStrikeStep()
    {
        if (!stepStarted || stepBlocked || !CanNavigate || animator == null || !animator.enabled) return;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.fullPathHash == AttackStateHash) info = next;
        }
        if (info.fullPathHash != AttackStateHash) return;
        AdvanceStep(Mathf.InverseLerp(stepStartNormalized, stepEndNormalized, info.normalizedTime));
    }

    private void AdvanceStep(float progress)
    {
        if (stepBlocked || !CanNavigate) return;
        float next = Mathf.Max(stepProgress, Mathf.Clamp01(progress));
        float distance = Mathf.Max(0f, strikeDistance) * (next - stepProgress);
        stepProgress = next;
        if (distance <= 0.0001f) return;

        float allowed = distance;
        Vector3 start = agent.nextPosition;
        if (agent.Raycast(start + strikeDirection * distance, out NavMeshHit edge))
            allowed = Mathf.Min(allowed, Mathf.Max(0f, Vector3.Distance(start, edge.position) - 0.03f));
        float radius = Mathf.Max(0.05f, agent.radius * 0.95f);
        Vector3 bottom = transform.position + Vector3.up * (agent.baseOffset + radius + 0.03f);
        Vector3 top = bottom + Vector3.up * Mathf.Max(0f, agent.height - 2f * radius - 0.06f);
        foreach (Collider overlap in Physics.OverlapCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (overlap != null && !overlap.transform.IsChildOf(transform)) { allowed = 0f; break; }
        }
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, strikeDirection, distance,
            obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - 0.03f));
        }
        if (allowed < distance)
        {
            stepBlocked = true;
            LogAttack("strike movement stopped by a body, wall or NavMesh edge");
        }
        if (allowed > 0f) agent.Move(strikeDirection * allowed);
    }

    public override void ReceiveNormalAttackHit(AnimationEvent animationEvent)
    {
        if (!CanAcceptAttackEvent(animationEvent) || contactResolved) return;
        contactResolved = true;
        // The clip event closes the movement window. A low frame rate cannot
        // leave movement continuing after the contact, or bypass collision checks.
        if (stepStarted) AdvanceStep(1f);
        else WarnEvents("PursuerStepStart was missing. Contact is checked in place; no fallback movement was applied.");

        string result;
        if (target == null) result = "miss: no target";
        else if (!WithinHeight()) result = "miss: height";
        else if (DistanceToTarget() > Mathf.Max(0.1f, hitReach)) result = "miss: range";
        else if (!WithinFacing(strikeDirection, hitHalfAngle)) result = "miss: committed facing";
        else if (HasCover()) result = "miss: cover";
        else
        {
            PlayerHealth player = target.GetComponentInParent<PlayerHealth>();
            if (player == null) result = "miss: no PlayerHealth";
            else { player.TakeDamage(Mathf.Max(0f, attackDamage)); result = $"hit: {attackDamage:F0} damage"; }
        }
        LogAttack($"{result}; contact {PhaseAge:F2} s after commitment");
        BeginRecovery();
    }

    public override void ReceiveAttackComplete(AnimationEvent animationEvent)
    {
        if (!isActiveAndEnabled || !CanReceiveHitReaction || !IsAttackEvent(animationEvent)) return;
        if (phase == Phase.Strike || phase == Phase.Recovery) completionReceived = true;
    }

    private void BeginRecovery()
    {
        contactResolved = true;
        stepStarted = false;
        recoveryEndsAt = Time.time + Mathf.Max(0f, recoveryTime);
        Enter(Phase.Recovery);
    }

    public override void HitStun(float duration)
    {
        if (!isActiveAndEnabled || !CanReceiveHitReaction) return;
        Initialize();
        float end = Time.time + Mathf.Max(0f, duration > 0f ? duration : defaultStunTime);
        stunEndsAt = phase == Phase.Stunned ? Mathf.Max(stunEndsAt, end) : end;
        nextAttackAt = Time.time + Mathf.Max(0f, attackCooldown);
        contactResolved = true;
        stepStarted = false;
        LogAttack("interrupted");
        Enter(Phase.Stunned);
    }

    public override void BeginDeathAnimation()
    {
        if (dead) return;
        Initialize();
        if (health != null && health.IsTrainingDummy) return;
        dead = true;
        contactResolved = true;
        stepStarted = false;
        Enter(Phase.Dead);
        if (audioSource != null) audioSource.Stop();
        if (animator != null && animator.enabled && hasDeath) animator.SetTrigger(DeathHash);
    }

    private void FollowTarget(float speed)
    {
        agent.speed = Mathf.Max(0f, speed);
        agent.isStopped = false;
        if (Time.time < nextPathAt) return;
        agent.SetDestination(target.position);
        nextPathAt = Time.time + Mathf.Max(0.02f, pathUpdateInterval);
    }

    private void FaceTarget()
    {
        FaceDirection(target.position - transform.position);
    }

    private void FaceDirection(Vector3 delta)
    {
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta),
                Mathf.Max(1f, turnSpeed) * Time.deltaTime);
    }

    private float DistanceToTarget()
    {
        if (target == null) return float.PositiveInfinity;
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private bool WithinHeight() => target != null && Mathf.Abs(target.position.y - transform.position.y) <= Mathf.Max(0f, verticalTolerance);

    private bool WithinFacing(Vector3 facing, float halfAngle)
    {
        if (target == null) return false;
        Vector3 delta = target.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude < 0.0001f || Vector3.Dot(facing, delta.normalized) >=
            Mathf.Cos(Mathf.Clamp(halfAngle, 1f, 89f) * Mathf.Deg2Rad);
    }

    private bool HasCover()
    {
        if (target == null) return true;
        Vector3 chest = Vector3.up * (agent != null ? agent.height * 0.5f : 1f);
        Vector3 from = transform.position + chest;
        Vector3 line = target.position + chest - from;
        if (line.sqrMagnitude < 0.0001f) return false;
        PlayerHealth player = target.GetComponentInParent<PlayerHealth>();
        foreach (RaycastHit hit in Physics.RaycastAll(from, line.normalized, line.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(target)) continue;
            if (player != null && hit.collider.GetComponentInParent<PlayerHealth>() == player) continue;
            ZombieAIController other = hit.collider.GetComponentInParent<ZombieAIController>();
            if (other != null && other.CanReceiveHitReaction) continue;
            return true;
        }
        return false;
    }

    private void FindTarget()
    {
        if (target != null || Time.time < nextTargetSearchAt) return;
        nextTargetSearchAt = Time.time + 1f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    private void StopAgent()
    {
        if (!CanNavigate) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    private void TakeMovementControl()
    {
        if (ownsMovement || agent == null) return;
        ownsMovement = true;
        savedSpeed = agent.speed;
        savedAcceleration = agent.acceleration;
        savedAngularSpeed = agent.angularSpeed;
        savedStoppingDistance = agent.stoppingDistance;
        savedRotation = agent.updateRotation;
        agent.updateRotation = false;
        agent.acceleration = Mathf.Max(0.1f, acceleration);
        agent.angularSpeed = Mathf.Max(1f, turnSpeed);
        agent.stoppingDistance = Mathf.Min(0.9f, Mathf.Max(0.1f, commitmentRange) * 0.7f);
        if (animator != null)
        {
            savedRootMotion = animator.applyRootMotion;
            if (savedRootMotion) animator.applyRootMotion = false;
        }
    }

    private void RestoreMovementControl()
    {
        if (!ownsMovement) return;
        if (agent != null)
        {
            agent.speed = savedSpeed;
            agent.acceleration = savedAcceleration;
            agent.angularSpeed = savedAngularSpeed;
            agent.stoppingDistance = savedStoppingDistance;
            agent.updateRotation = savedRotation;
        }
        if (animator != null && animator.applyRootMotion != savedRootMotion) animator.applyRootMotion = savedRootMotion;
        ownsMovement = false;
    }

    private void ResetAttackTriggers()
    {
        if (animator == null) return;
        if (hasPrepare) animator.ResetTrigger(PrepareHash);
        if (hasAttack) animator.ResetTrigger(AttackHash);
        if (hasCancel) animator.ResetTrigger(CancelHash);
    }

    private void CancelAttackAnimation()
    {
        ResetAttackTriggers();
        if (animator != null && animator.enabled && hasCancel) animator.SetTrigger(CancelHash);
    }

    private void UpdateAnimation()
    {
        if (animator != null && animator.enabled && hasMove)
            animator.SetFloat(MoveHash, CanNavigate && !agent.isStopped ? agent.velocity.magnitude : 0f, 0.1f, Time.deltaTime);
    }

    private void WarnEvents(string message)
    {
        if (!warnedEvents) Debug.LogWarning($"{name}: {message}", this);
        warnedEvents = true;
        LogAttack(message);
    }

    private void LogAttack(string message)
    {
        if (debugAttackLogs) Debug.Log($"{name} Pursuer #{attackNumber} [{phase}]: {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preparationRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, commitmentRange);
    }
}
