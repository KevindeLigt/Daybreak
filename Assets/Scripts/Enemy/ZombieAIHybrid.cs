using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAIHybrid : MonoBehaviour
{
    public Transform target;

    // ---------- GENERAL / DETECTION ----------
    [Header("Detection")]
    [Tooltip("If player is within this radius, zombie will start chasing.")]
    public float detectionRadius = 15f;

    [Tooltip("If player goes beyond this radius, zombie goes back to wandering.")]
    public float loseInterestRadius = 20f;

    // ---------- WANDER ----------
    [Header("Wander Settings")]
    [Tooltip("Radius around spawn position to wander in.")]
    public float wanderRadius = 8f;

    [Tooltip("Min time to pause between wander moves.")]
    public float wanderPauseMin = 1.5f;

    [Tooltip("Max time to pause between wander moves.")]
    public float wanderPauseMax = 3.5f;

    // ---------- MOVEMENT ----------
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    [Tooltip("Speed when zombie is in close focus mode.")]
    public float focusSpeed = 2.5f;
    [Tooltip("Speed during chase surge bursts (layered, not its own state).")]
    public float surgeSpeed = 4.2f;

    [Tooltip("Min time between surges.")]
    public float surgeIntervalMin = 2f;
    [Tooltip("Max time between surges.")]
    public float surgeIntervalMax = 4f;
    [Tooltip("How long a surge lasts.")]
    public float surgeDuration = 0.6f;

    [Tooltip("Distance at which zombie stops drifting and tightens up.")]
    public float focusDistance = 3.5f;

    [Header("Drift Personality (only when NOT focused)")]
    public float driftAmount = 0.8f;
    public float driftFrequency = 0.7f;

    // ---------- ATTACK ----------
    [Header("Attack Settings")]
    public float attackRange = 1.6f;
    public float attackWindupTime = 0.4f;
    public float attackRecoveryTime = 0.4f;
    public float attackCooldown = 1.2f;
    public float attackDamage = 20f;

    // ---------- STUN ----------
    [Header("Hit / Stun")]
    [Tooltip("Default stun time if caller doesn't specify.")]
    public float defaultStunTime = 0.2f;
    public Color stunColor = Color.yellow;
    public Color attackColor = Color.red;
    public Color normalColor = Color.white;

    [Header("Visual")]
    public Renderer visualRenderer; // assign your cube or zombie MeshRenderer here

    private NavMeshAgent agent;

    // ---------- STATE MACHINE ----------
    private enum ZombieState
    {
        Idle,
        Wander,
        Chase,
        AttackWindup,
        AttackRecover,
        Stunned,
        Dead
    }

    private ZombieState state = ZombieState.Idle;
    private float stateTimer = 0f;

    // wander
    private Vector3 spawnPosition;
    private Vector3 currentWanderTarget;
    private float currentWanderPause = 0f;

    // attack
    private float lastAttackTime = -999f;

    // surge (layered on chase)
    private bool isSurging = false;
    private float surgeEndTime = 0f;
    private float nextSurgeTime = 0f;

    // stun
    private float currentStunDuration = 0f;

    // drift
    private float driftPhase = 0f;

    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        spawnPosition = transform.position;

        if (!target)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) target = playerObj.transform;
        }

        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<Renderer>();

        SetState(ZombieState.Wander);
        ScheduleNextSurge();
    }

    void Update()
    {
        if (isDead || !target) return;

        stateTimer += Time.deltaTime;

        switch (state)
        {
            case ZombieState.Idle:
                UpdateIdle();
                break;
            case ZombieState.Wander:
                UpdateWander();
                break;
            case ZombieState.Chase:
                UpdateChase();
                break;
            case ZombieState.AttackWindup:
                UpdateAttackWindup();
                break;
            case ZombieState.AttackRecover:
                UpdateAttackRecover();
                break;
            case ZombieState.Stunned:
                UpdateStunned();
                break;
            case ZombieState.Dead:
                // handled by other systems (ragdoll, destroy, etc.)
                break;
        }
    }

    // ===================== STATE MACHINE CORE =====================

    void SetState(ZombieState newState)
    {
        if (isDead) return;
        if (state == newState) return;

        OnExitState(state);
        state = newState;
        stateTimer = 0f;
        OnEnterState(state);
    }

    void OnEnterState(ZombieState newState)
    {
        switch (newState)
        {
            case ZombieState.Idle:
                agent.isStopped = true;
                break;

            case ZombieState.Wander:
                agent.isStopped = false;
                agent.speed = walkSpeed * 0.7f;
                PickNewWanderTarget();
                break;

            case ZombieState.Chase:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                break;

            case ZombieState.AttackWindup:
                agent.isStopped = true;
                if (visualRenderer)
                {
                    visualRenderer.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
                    visualRenderer.material.color = attackColor;
                }
                break;

            case ZombieState.AttackRecover:
                agent.isStopped = true;
                if (visualRenderer)
                    visualRenderer.transform.localRotation = Quaternion.identity;
                break;

            case ZombieState.Stunned:
                agent.isStopped = true;
                if (visualRenderer)
                    visualRenderer.material.color = stunColor;
                break;

            case ZombieState.Dead:
                agent.isStopped = true;
                // visual handled by ragdoll / EnemyHealth
                break;
        }
    }

    void OnExitState(ZombieState oldState)
    {
        switch (oldState)
        {
            case ZombieState.AttackWindup:
                if (visualRenderer)
                    visualRenderer.material.color = normalColor;
                break;

            case ZombieState.Stunned:
                if (visualRenderer)
                    visualRenderer.material.color = normalColor;
                agent.isStopped = false;
                break;

            case ZombieState.AttackRecover:
                agent.isStopped = false;
                if (visualRenderer)
                    visualRenderer.transform.localRotation = Quaternion.identity;
                break;
        }
    }

    // ===================== IDLE =====================

    void UpdateIdle()
    {
        // you can use this later if zombies spawn inactive
        // for now, go straight to Wander
        SetState(ZombieState.Wander);
    }

    // ===================== WANDER =====================

    void UpdateWander()
    {
        float distToPlayer = Vector3.Distance(transform.position, target.position);

        // Player detected? go chase
        if (distToPlayer <= detectionRadius)
        {
            SetState(ZombieState.Chase);
            return;
        }

        // wander logic
        if (!agent.hasPath || agent.remainingDistance <= 0.3f)
        {
            // standing still, wait a bit before picking next wander target
            if (currentWanderPause <= 0f)
            {
                currentWanderPause = Random.Range(wanderPauseMin, wanderPauseMax);
            }

            if (stateTimer >= currentWanderPause)
            {
                PickNewWanderTarget();
                currentWanderPause = 0f;
                stateTimer = 0f;
            }
        }
    }

    void PickNewWanderTarget()
    {
        Vector2 rnd = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = spawnPosition + new Vector3(rnd.x, 0, rnd.y);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas))
        {
            currentWanderTarget = hit.position;
            agent.SetDestination(currentWanderTarget);
        }
        else
        {
            currentWanderTarget = transform.position;
        }
    }

    // ===================== CHASE (with drift + surge + focus) =====================

    void UpdateChase()
    {
        float distance = Vector3.Distance(transform.position, target.position);

        // lost the player? back to wander
        if (distance > loseInterestRadius)
        {
            SetState(ZombieState.Wander);
            return;
        }

        // inside attack range? go into windup
        if (distance <= attackRange)
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                SetState(ZombieState.AttackWindup);
                return;
            }
        }

        bool inFocusZone = distance <= focusDistance;

        MoveChaseWithPersonality(inFocusZone);
        HandleSurgeLayered(distance, inFocusZone);
    }

    void MoveChaseWithPersonality(bool inFocusZone)
    {
        Vector3 toPlayer = (target.position - transform.position);
        toPlayer.y = 0f;
        Vector3 dirToPlayer = toPlayer.normalized;
        Vector3 moveDir = dirToPlayer;

        if (!inFocusZone)
        {
            // drifting/sway only when NOT close
            driftPhase += Time.deltaTime * driftFrequency;
            Vector3 drift = transform.right * Mathf.Sin(driftPhase) * driftAmount;
            moveDir = (dirToPlayer + drift).normalized;

            if (visualRenderer)
            {
                visualRenderer.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(Time.time * 3f) * 5f,
                    0f,
                    Mathf.Sin(Time.time * 2f) * 5f
                );
            }
        }
        else
        {
            // lock in: face player, no drift, no tilt
            FaceTarget();
            if (visualRenderer)
                visualRenderer.transform.localRotation = Quaternion.identity;
        }

        // speed
        if (isSurging)
        {
            agent.speed = surgeSpeed;
        }
        else
        {
            agent.speed = inFocusZone ? focusSpeed : walkSpeed;
        }

        agent.isStopped = false;
        agent.SetDestination(transform.position + moveDir * 2f);
    }

    void HandleSurgeLayered(float distanceToPlayer, bool inFocusZone)
    {
        // end surge if time over
        if (isSurging && Time.time >= surgeEndTime)
        {
            isSurging = false;
        }

        if (isSurging) return;

        // only consider surging if not in immediate attack range
        if (distanceToPlayer <= attackRange * 0.9f) return;

        // surge on timer to help close distance
        if (Time.time >= nextSurgeTime)
        {
            StartSurge();
        }
    }

    void StartSurge()
    {
        isSurging = true;
        surgeEndTime = Time.time + surgeDuration;
        ScheduleNextSurge();
    }

    void ScheduleNextSurge()
    {
        nextSurgeTime = Time.time + Random.Range(surgeIntervalMin, surgeIntervalMax);
    }

    void FaceTarget()
    {
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
    }

    // ===================== ATTACK: WINDUP =====================

    void UpdateAttackWindup()
    {
        // shotgun can interrupt: if stunned, state will change externally

        // keep facing the player
        FaceTarget();

        if (stateTimer >= attackWindupTime)
        {
            // time to actually strike
            PerformAttack();
            SetState(ZombieState.AttackRecover);
        }
    }

    void PerformAttack()
    {
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= attackRange + 0.2f)
        {
            if (target.TryGetComponent(out PlayerHealth playerHealth))
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        lastAttackTime = Time.time;
    }

    // ===================== ATTACK: RECOVER =====================

    void UpdateAttackRecover()
    {
        // slight overcommit/recovery before going back to chase
        if (stateTimer >= attackRecoveryTime)
        {
            SetState(ZombieState.Chase);
        }
    }

    // ===================== STUN =====================

    void UpdateStunned()
    {
        // shotgun should fully interrupt attacks: we never reach PerformAttack while stunned
        if (stateTimer >= currentStunDuration)
        {
            SetState(ZombieState.Chase);
        }
    }

    private void OnDrawGizmos()
    {
        // only draw when object is selected? → use OnDrawGizmosSelected instead.
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.15f);  // yellow, transparent
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);     // red, transparent
        Gizmos.DrawSphere(transform.position, loseInterestRadius);
    }


    /// <summary>
    /// Called externally (e.g. from EnemyHealth when hit by shotgun) to briefly stun/interrupt.
    /// </summary>
    public void HitStun(float stunTime)
    {
        if (isDead) return;

        // fully interrupt attacks
        if (state == ZombieState.AttackWindup || state == ZombieState.AttackRecover)
        {
            // attack will NOT complete
        }

        currentStunDuration = stunTime > 0f ? stunTime : defaultStunTime;
        SetState(ZombieState.Stunned);
    }

    // ===================== PUBLIC DEATH HOOK =====================

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        SetState(ZombieState.Dead);
        // Ragdoll / destroy handled by EnemyHealth or another script
    }

    // optional: helper to visualize detection/wander radii
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseInterestRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? spawnPosition : transform.position, wanderRadius);
    }
}
