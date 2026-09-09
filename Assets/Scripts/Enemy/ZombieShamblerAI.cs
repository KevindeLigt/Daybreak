using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shambler: wander, pursue, prepare, strike, recover, react, die.
/// Renamed from ZombieAIHybrid; animation entry points are unchanged.
/// EnemyHealth owns damage bookkeeping, kill rewards and ragdoll handoff.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieShamblerAI : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public Animator animator;
    public AudioSource idleAudioSource;
    public AudioClip idleLoopClip;

    [Header("Detection")]
    [Min(0f)] public float detectionRadius = 15f;
    [Min(0f)] public float loseInterestRadius = 20f;

    [Header("Wandering")]
    [Min(0f)] public float wanderRadius = 8f;
    [Min(0f)] public float wanderPauseMin = 1.5f;
    [Min(0f)] public float wanderPauseMax = 3.5f;

    [Header("Pursuit")]
    [Min(0f)] public float walkSpeed = 2f;
    [Min(0f)] public float focusSpeed = 2.5f;
    [Min(0f)] public float focusDistance = 3.5f;
    [Tooltip("One speed multiplier per zombie. Set both values to 1 for identical speeds. No hidden personality modifiers.")]
    public Vector2 speedMultiplierRange = new Vector2(0.88f, 1.12f);
    [Min(0.02f)] public float pathUpdateInterval = 0.15f;
    [Min(0f)] public float moveAnimationDampTime = 0.1f;

    [Header("Normal Melee")]
    [Tooltip("Root-to-root distance at which preparation begins. This is a close Shambler attack, not the Pursuer's early wind-up.")]
    [Min(0.1f)] public float attackRange = 1.6f;
    [Min(0f)] public float attackDamage = 20f;
    [Tooltip("Minimum time between commitments. An interruption also consumes the cooldown.")]
    [Min(0f)] public float attackCooldown = 1.2f;
    [Tooltip("Total moving preparation time; there is no additional pose hold.")]
    [Min(0.05f)] public float normalAttackPrepareDuration = 0.6f;
    [Min(0f)] public float normalAttackPrepareSpeed = 3.5f;
    [Min(0f)] public float normalAttackPrepareStopDistance = 1f;
    [Min(0f)] public float normalAttackStepDistance = 1f;
    [Tooltip("Step duration from commitment. Match this to the Attack clip's forward movement.")]
    [Min(0.01f)] public float normalAttackStepDuration = 0.2f;
    [Tooltip("Minimum recovery after the contact event, including a missed hit.")]
    [Min(0f)] public float normalAttackRecoveryDuration = 0.35f;

    [Header("Melee Contact")]
    [Range(1f, 89f)] public float normalAttackHalfAngle = 55f;
    [Min(0f)] public float normalAttackVerticalTolerance = 1.5f;
    [Tooltip("Extra reach at contact, in addition to Attack Range.")]
    [Min(0f)] public float normalAttackRangeAllowance = 0.2f;
    [Tooltip("Solid layers blocking the step or hit. Include environment and player. Triggers are ignored. Living zombies block movement but do not shield the player from the contact check.")]
    public LayerMask normalAttackObstacleMask = ~0;

    [Header("Animation Timing")]
    [Tooltip("On: NormalAttackHit/AttackComplete events own contact/completion. Off: timing-only blockout. Both use the same melee behaviour.")]
    public bool normalAttackUseAnimationEvents = true;
    [Min(0f)] public float normalAttackHitFallbackTime = 0.18f;
    [Tooltip("Missing attack events cancel the attack without applying invisible fallback damage.")]
    [Min(0.1f)] public float normalAttackEventTimeout = 2f;

    [Header("Hit Reactions and Diagnostics")]
    [Min(0f)] public float defaultStunTime = 0.2f;
    [Tooltip("Log preparation, commitment, contact outcomes and blocked steps on this test zombie. Off for normal play.")]
    public bool debugAttackLogs;

    private enum State { Wander, Chase, PrepareAttack, Attack, Recovery, Stunned, Dead }
    private State state = State.Wander;
    private float stateStartedAt;
    private NavMeshAgent agent;
    private EnemyHealth health;
    private bool initialized;
    private bool isDead;
    private Vector3 spawnPosition;
    private float speedMultiplier = 1f;
    private float nextPathUpdateAt;
    private float nextTargetSearchAt;
    private float wanderResumeAt = -1f;
    private float nextAttackAt;
    private float stunEndsAt;
    private float recoveryEndsAt;
    private bool hitResolved;
    private bool completionReceived;
    private Vector3 strikeDirection;
    private float stepRequested;
    private bool stepBlocked;
    private bool ownsAttackMovement;
    private bool savedUpdateRotation;
    private float savedStoppingDistance;
    private bool savedRootMotion;
    private bool warnedNavMesh;
    private bool warnedEvents;
    private int attackNumber;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int PrepareHash = Animator.StringToHash("PrepareAttack");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeathHash = Animator.StringToHash("StumbleFall");
    private bool hasMoveSpeed;
    private bool hasPrepare;
    private bool hasAttack;
    private bool hasDeath;

    private bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;
    private float StateAge => Time.time - stateStartedAt;
    private bool AttackInProgress => state == State.PrepareAttack || state == State.Attack || state == State.Recovery;
    public bool CanReceiveHitReaction => !isDead && (health == null || !health.IsDead);

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        spawnPosition = transform.position;
        speedMultiplier = Random.Range(
            Mathf.Max(0.1f, Mathf.Min(speedMultiplierRange.x, speedMultiplierRange.y)),
            Mathf.Max(0.1f, Mathf.Max(speedMultiplierRange.x, speedMultiplierRange.y)));
        CacheAnimatorParameters();
        initialized = true;
        FindTarget();
        if (health != null && health.IsDead)
        {
            BeginDeathAnimation();
            return;
        }
        ChangeState(State.Wander);
        PlayIdleAudio();
    }

    private void OnEnable()
    {
        if (!initialized || isDead || (health != null && health.IsDead)) return;
        // Enabling a living component resumes pursuit, never an interrupted swing.
        ChangeState(target != null ? State.Chase : State.Wander);
        PlayIdleAudio();
    }

    private void OnDisable()
    {
        StopAgent();
        ReleaseAttackMovement();
        ResetAttackTriggers();
        hitResolved = true;
        if (initialized && !isDead)
        {
            state = State.Chase;
            nextAttackAt = Time.time + Mathf.Max(0f, attackCooldown);
        }
        if (idleAudioSource != null) idleAudioSource.Stop();
    }

    private void Update()
    {
        if (isDead) return;
        if (health != null && health.IsDead)
        {
            BeginDeathAnimation();
            return;
        }
        if (target == null) FindTarget();
        if (!CanNavigate || target == null)
        {
            if (AttackInProgress) ChangeState(State.Chase);
            StopAgent();
            UpdateAnimation();
            if (!CanNavigate && !warnedNavMesh)
            {
                Debug.LogWarning($"{name}: Shambler needs an enabled agent on a baked NavMesh. AI is paused.", this);
                warnedNavMesh = true;
            }
            return;
        }
        warnedNavMesh = false;
        switch (state)
        {
            case State.Wander: UpdateWander(); break;
            case State.Chase: UpdateChase(); break;
            case State.PrepareAttack: UpdatePreparation(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Recovery:
                if (Time.time >= recoveryEndsAt) ChangeState(State.Chase);
                break;
            case State.Stunned:
                if (Time.time >= stunEndsAt) ChangeState(State.Chase);
                break;
        }
        UpdateAnimation();
    }

    private void ChangeState(State next)
    {
        if (isDead && next != State.Dead) return;
        bool enteringMelee = next == State.PrepareAttack || next == State.Attack || next == State.Recovery;
        if (!enteringMelee) ReleaseAttackMovement();
        state = next;
        stateStartedAt = Time.time;
        nextPathUpdateAt = 0f;
        switch (next)
        {
            case State.Wander:
                wanderResumeAt = -1f;
                StopAgent();
                break;
            case State.Chase:
                ResetAttackTriggers();
                break;
            case State.PrepareAttack:
                BeginAttackMovement();
                ResetAttackTriggers();
                if (animator != null && hasPrepare) animator.SetTrigger(PrepareHash);
                attackNumber++;
                LogAttack($"prepare; distance {TargetDistance():F2} m");
                break;
            case State.Attack:
                StopAgent();
                strikeDirection = transform.forward;
                strikeDirection.y = 0f;
                strikeDirection.Normalize();
                hitResolved = false;
                completionReceived = false;
                stepRequested = 0f;
                stepBlocked = false;
                recoveryEndsAt = float.PositiveInfinity;
                nextAttackAt = Time.time + Mathf.Max(0f, attackCooldown);
                if (animator != null && hasAttack) animator.SetTrigger(AttackHash);
                LogAttack($"commit; distance {TargetDistance():F2} m");
                break;
            case State.Recovery:
            case State.Stunned:
            case State.Dead:
                StopAgent();
                ResetAttackTriggers();
                break;
        }
    }

    private void UpdateWander()
    {
        if (TargetDistance() <= Mathf.Max(0f, detectionRadius))
        {
            ChangeState(State.Chase);
            return;
        }
        if (agent.pathPending) return;
        if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.15f) return;
        if (wanderResumeAt < 0f)
        {
            StopAgent();
            wanderResumeAt = Time.time + Random.Range(
                Mathf.Max(0f, Mathf.Min(wanderPauseMin, wanderPauseMax)),
                Mathf.Max(0f, Mathf.Max(wanderPauseMin, wanderPauseMax)));
        }
        if (Time.time < wanderResumeAt) return;
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, wanderRadius);
        Vector3 candidate = spawnPosition + new Vector3(offset.x, 0f, offset.y);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, filter))
        {
            agent.speed = Mathf.Max(0f, walkSpeed) * speedMultiplier * 0.7f;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
        wanderResumeAt = -1f;
    }

    private void UpdateChase()
    {
        float distance = TargetDistance();
        if (distance > Mathf.Max(detectionRadius, loseInterestRadius))
        {
            ChangeState(State.Wander);
            return;
        }
        if (distance <= Mathf.Max(0.1f, attackRange) && Time.time >= nextAttackAt)
        {
            ChangeState(State.PrepareAttack);
            return;
        }
        agent.speed = Mathf.Max(0f, distance <= focusDistance ? focusSpeed : walkSpeed) * speedMultiplier;
        agent.isStopped = false;
        FollowTarget();
    }

    private void UpdatePreparation()
    {
        FaceTarget();
        agent.speed = Mathf.Max(0f, normalAttackPrepareSpeed);
        agent.isStopped = false;
        FollowTarget();
        if (StateAge >= Mathf.Max(0.05f, normalAttackPrepareDuration)) ChangeState(State.Attack);
    }

    private void UpdateAttack()
    {
        MoveAttackStep();
        if (!normalAttackUseAnimationEvents && !hitResolved && StateAge >= Mathf.Max(0f, normalAttackHitFallbackTime))
            ResolveContact();
        if (hitResolved && StateAge >= Mathf.Max(0.01f, normalAttackStepDuration) &&
            (!normalAttackUseAnimationEvents || completionReceived))
        {
            ChangeState(State.Recovery);
            return;
        }
        if (normalAttackUseAnimationEvents && StateAge >= Mathf.Max(0.1f, normalAttackEventTimeout))
        {
            if (!warnedEvents)
            {
                Debug.LogWarning($"{name}: Melee timed out (contact={hitResolved}, completion={completionReceived}). " +
                    "Check NormalAttackHit/AttackComplete and the transition into Attack. No fallback damage was applied.", this);
                warnedEvents = true;
            }
            LogAttack("cancelled: missing/late animation event");
            if (!hitResolved) recoveryEndsAt = Time.time + Mathf.Max(0f, normalAttackRecoveryDuration);
            ChangeState(State.Recovery);
        }
    }

    private void ResolveContact()
    {
        if (!isActiveAndEnabled || !CanReceiveHitReaction || state != State.Attack || hitResolved || !CanNavigate) return;
        hitResolved = true;
        recoveryEndsAt = Time.time + Mathf.Max(0f, normalAttackRecoveryDuration);
        MoveAttackStep();
        if (target == null) { LogAttack("miss: target missing"); return; }
        float distance = TargetDistance();
        if (distance > Mathf.Max(0.1f, attackRange) + Mathf.Max(0f, normalAttackRangeAllowance))
        {
            LogAttack($"miss: out of range ({distance:F2} m); contact at {StateAge:F2} s");
            return;
        }
        Vector3 delta = target.position - transform.position;
        if (Mathf.Abs(delta.y) > Mathf.Max(0f, normalAttackVerticalTolerance))
        { LogAttack("miss: height difference"); return; }
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.0001f && Vector3.Dot(strikeDirection, delta.normalized) <
            Mathf.Cos(Mathf.Clamp(normalAttackHalfAngle, 1f, 89f) * Mathf.Deg2Rad))
        { LogAttack("miss: outside committed facing"); return; }
        PlayerHealth player = target.GetComponentInParent<PlayerHealth>();
        if (player == null) { LogAttack("miss: target has no PlayerHealth"); return; }
        if (HasSolidCover(player)) { LogAttack("miss: solid cover"); return; }
        player.TakeDamage(Mathf.Max(0f, attackDamage));
        LogAttack($"hit: {attackDamage:F0} damage; contact at {StateAge:F2} s");
    }

    private void MoveAttackStep()
    {
        if (stepBlocked || !CanNavigate) return;
        float requested = Mathf.Max(0f, normalAttackStepDistance) *
            Mathf.Clamp01(StateAge / Mathf.Max(0.01f, normalAttackStepDuration));
        float distance = Mathf.Max(0f, requested - stepRequested);
        stepRequested = requested;
        if (distance <= 0.0001f) return;
        float allowed = distance;
        Vector3 start = agent.nextPosition;
        if (agent.Raycast(start + strikeDirection * distance, out NavMeshHit edge))
            allowed = Mathf.Min(allowed, Mathf.Max(0f, Vector3.Distance(start, edge.position) - 0.03f));
        float radius = Mathf.Max(0.05f, agent.radius * 0.95f);
        Vector3 bottom = transform.position + Vector3.up * (agent.baseOffset + radius + 0.03f);
        Vector3 top = bottom + Vector3.up * Mathf.Max(0f, agent.height - 2f * radius - 0.06f);
        foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, strikeDirection, distance,
            normalAttackObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - 0.03f));
        }
        if (allowed < distance)
        {
            stepBlocked = true;
            LogAttack("forward step stopped by a collider or NavMesh edge");
        }
        if (allowed > 0f) agent.Move(strikeDirection * allowed);
    }

    private bool HasSolidCover(PlayerHealth player)
    {
        Vector3 chest = Vector3.up * (agent != null ? agent.height * 0.5f : 1f);
        Vector3 from = transform.position + chest;
        Vector3 line = target.position + chest - from;
        if (line.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(from, line.normalized, line.magnitude,
            normalAttackObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null) continue;
            Transform obstacle = hit.collider.transform;
            if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(target)) continue;
            if (hit.collider.GetComponentInParent<PlayerHealth>() == player) continue;
            // Living Shamblers retain body separation, but do not act as invisible
            // shields against an adjacent Shambler's reach. Walls still block hits.
            ZombieShamblerAI other = hit.collider.GetComponentInParent<ZombieShamblerAI>();
            if (other != null && other.CanReceiveHitReaction) continue;
            return true;
        }
        return false;
    }

    private void BeginAttackMovement()
    {
        if (ownsAttackMovement) return;
        ownsAttackMovement = true;
        savedUpdateRotation = agent.updateRotation;
        savedStoppingDistance = agent.stoppingDistance;
        agent.updateRotation = false;
        agent.stoppingDistance = Mathf.Clamp(normalAttackPrepareStopDistance, 0f, Mathf.Max(0.1f, attackRange));
        if (animator != null)
        {
            savedRootMotion = animator.applyRootMotion;
            if (savedRootMotion) animator.applyRootMotion = false;
        }
        StopAgent();
    }

    private void ReleaseAttackMovement()
    {
        if (!ownsAttackMovement) return;
        StopAgent();
        if (agent != null)
        {
            agent.updateRotation = savedUpdateRotation;
            agent.stoppingDistance = savedStoppingDistance;
        }
        if (animator != null && animator.applyRootMotion != savedRootMotion)
            animator.applyRootMotion = savedRootMotion;
        ownsAttackMovement = false;
    }

    // Entry points retained for EnemyHitReaction, ShoulderRam, EnemyHealth,
    // and the existing ZombieAnimationEvents component.
    public void HitStun(float duration)
    {
        if (!isActiveAndEnabled || !CanReceiveHitReaction) return;
        stunEndsAt = Mathf.Max(Time.time + Mathf.Max(0f, duration > 0f ? duration : defaultStunTime),
            state == State.Stunned ? stunEndsAt : 0f);
        nextAttackAt = Time.time + Mathf.Max(0f, attackCooldown);
        if (AttackInProgress) LogAttack("interrupted by hit reaction");
        ChangeState(State.Stunned);
    }

    public void BeginDeathAnimation()
    {
        if (isDead) return;
        isDead = true;
        ChangeState(State.Dead);
        if (idleAudioSource != null) idleAudioSource.Stop();
        if (animator != null && animator.enabled && hasDeath) animator.SetTrigger(DeathHash);
    }

    public void Die() => BeginDeathAnimation();
    public void AnimationEvent_NormalAttackHit()
    {
        // Timing-only mode intentionally ignores animation contact events.
        if (normalAttackUseAnimationEvents) ResolveContact();
    }
    public void AnimationEvent_AttackComplete()
    {
        if (isActiveAndEnabled && !isDead && state == State.Attack) completionReceived = true;
    }
    // Safe compatibility receivers: old clips/bridge compile, but cannot start
    // another preparation timer or re-enable the removed terminal lunge.
    public void AnimationEvent_PrepareAttackPoseReached() { }
    public void AnimationEvent_PrepareAttackComplete() { }
    public void AnimationEvent_BeginLungeFlight() { }

    private void FindTarget()
    {
        if (target != null || Time.time < nextTargetSearchAt) return;
        nextTargetSearchAt = Time.time + 1f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }
    private float TargetDistance() => target == null ? float.PositiveInfinity : Vector3.Distance(transform.position, target.position);
    private void FollowTarget()
    {
        if (!CanNavigate || target == null || Time.time < nextPathUpdateAt) return;
        agent.SetDestination(target.position);
        nextPathUpdateAt = Time.time + Mathf.Max(0.02f, pathUpdateInterval);
    }
    private void StopAgent()
    {
        if (!CanNavigate) return;
        agent.isStopped = true;
        agent.ResetPath();
    }
    private void FaceTarget()
    {
        if (target == null) return;
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
    }
    private void UpdateAnimation()
    {
        if (animator == null || !animator.enabled || !hasMoveSpeed) return;
        float speed = CanNavigate && !agent.isStopped ? agent.velocity.magnitude : 0f;
        animator.SetFloat(MoveSpeedHash, speed, Mathf.Max(0f, moveAnimationDampTime), Time.deltaTime);
    }
    private void PlayIdleAudio()
    {
        if (idleAudioSource == null || idleLoopClip == null) return;
        idleAudioSource.clip = idleLoopClip;
        idleAudioSource.loop = true;
        if (!idleAudioSource.isPlaying) idleAudioSource.Play();
    }
    private void ResetAttackTriggers()
    {
        if (animator == null) return;
        if (hasPrepare) animator.ResetTrigger(PrepareHash);
        if (hasAttack) animator.ResetTrigger(AttackHash);
    }
    private void CacheAnimatorParameters()
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name}: No Animator assigned. Use timing-only melee for a deliberate animation-free test.", this);
            return;
        }
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == MoveSpeedHash) hasMoveSpeed = true;
            if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
            if (parameter.nameHash == PrepareHash) hasPrepare = true;
            if (parameter.nameHash == AttackHash) hasAttack = true;
            if (parameter.nameHash == DeathHash) hasDeath = true;
        }
        if (!hasPrepare || !hasAttack)
            Debug.LogWarning($"{name}: Animator needs PrepareAttack and Attack triggers for the normal melee sequence.", this);
    }
    private void LogAttack(string message)
    {
        if (debugAttackLogs) Debug.Log($"{name} melee #{attackNumber}: {message}", this);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
