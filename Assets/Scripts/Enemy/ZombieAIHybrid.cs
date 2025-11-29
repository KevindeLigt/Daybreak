using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAIHybrid : MonoBehaviour
{
    public Transform target;

    // ---------- GENERAL / DETECTION ----------
    [Header("Detection")]
    public float detectionRadius = 15f;
    public float loseInterestRadius = 20f;

    // ---------- WANDER ----------
    [Header("Wander Settings")]
    public float wanderRadius = 8f;
    public float wanderPauseMin = 1.5f;
    public float wanderPauseMax = 3.5f;

    // ---------- MOVEMENT ----------
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float focusSpeed = 2.5f;
    public float surgeSpeed = 4.2f;

    public float surgeIntervalMin = 2f;
    public float surgeIntervalMax = 4f;
    public float surgeDuration = 0.6f;

    public float focusDistance = 3.5f;

    [Header("Drift Personality")]
    public float driftAmount = 0.8f;
    public float driftFrequency = 0.7f;

    // ---------- ATTACK ----------
    [Header("Attack Settings")]
    public float attackRange = 1.6f;
    public float attackWindupTime = 0.4f;
    public float attackRecoveryTime = 0.4f;
    public float attackCooldown = 1.2f;
    public float attackDamage = 20f;

    // ---------- NEW LUNGE ATTACK ----------
    [Header("Lunge Attack")]
    public float lungeDistance = 3.5f;
    public float lungeSpeed = 12f;
    public float lungeCooldown = 4f;
    public float lungeDamage = 35f;
    public float minLungeRange = 2f;
    public float maxLungeRange = 6f;
    private float lastLungeTime = -999f;

    // ---------- STUN ----------
    [Header("Hit / Stun")]
    public float defaultStunTime = 0.2f;
    public Color stunColor = Color.yellow;
    public Color attackColor = Color.red;
    public Color normalColor = Color.white;

    // ---------- VISUAL ----------
    [Header("Visual")]
    public Renderer visualRenderer;

    // ---------- AUDIO ----------
    [Header("Audio")]
    public AudioSource idleAudioSource;
    public AudioClip idleLoopClip;

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

        // === NEW: LUNGE ATTACK STATE ===
        LungeAttack,

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

    // surge
    private bool isSurging = false;
    private float surgeEndTime = 0f;
    private float nextSurgeTime = 0f;

    // stun
    private float currentStunDuration = 0f;

    // drift
    private float driftPhase = 0f;

    private bool isDead = false;

    // ---------- COLLISION SHOVE ----------
    [Header("Collision Shove")]
    public float shoveForce = 2f;
    public float shoveCooldown = 0.5f;
    private float lastShoveTime = 0f;

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

        if (idleAudioSource && idleLoopClip)
        {
            idleAudioSource.clip = idleLoopClip;
            idleAudioSource.loop = true;
            idleAudioSource.pitch = Random.Range(0.9f, 1.1f);
            idleAudioSource.Play();
        }
    }

    void Update()
    {
        if (isDead || !target) return;

        stateTimer += Time.deltaTime;

        switch (state)
        {
            case ZombieState.Idle: UpdateIdle(); break;
            case ZombieState.Wander: UpdateWander(); break;
            case ZombieState.Chase: UpdateChase(); break;
            case ZombieState.AttackWindup: UpdateAttackWindup(); break;
            case ZombieState.AttackRecover: UpdateAttackRecover(); break;
            case ZombieState.Stunned: UpdateStunned(); break;

            // === NEW ===
            case ZombieState.LungeAttack: UpdateLungeAttack(); break;

            case ZombieState.Dead: break;
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

            // === NEW LUNGE ENTER ===
            case ZombieState.LungeAttack:
                agent.isStopped = true;
                Animator anim = GetComponent<Animator>();
                if (anim)
                    anim.SetTrigger("LungeTrigger");
                break;

            case ZombieState.Stunned:
                agent.isStopped = true;
                if (visualRenderer)
                    visualRenderer.material.color = stunColor;
                break;

            case ZombieState.Dead:
                agent.isStopped = true;
                if (idleAudioSource) idleAudioSource.Stop();
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

            // === RESET AFTER LUNGE ===
            case ZombieState.LungeAttack:
                if (visualRenderer)
                    visualRenderer.transform.localRotation = Quaternion.identity;
                agent.isStopped = false;
                break;
        }
    }

    // ===================== IDLE =====================

    void UpdateIdle()
    {
        SetState(ZombieState.Wander);
    }

    // ===================== WANDER =====================

    void UpdateWander()
    {
        float distToPlayer = Vector3.Distance(transform.position, target.position);

        if (distToPlayer <= detectionRadius)
        {
            SetState(ZombieState.Chase);
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= 0.3f)
        {
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

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            currentWanderTarget = hit.position;
            agent.SetDestination(currentWanderTarget);
        }
    }

    // ===================== CHASE =====================

    void UpdateChase()
    {
        float distance = Vector3.Distance(transform.position, target.position);

        if (distance > loseInterestRadius)
        {
            SetState(ZombieState.Wander);
            return;
        }

        // === NEW: LUNGE CONDITIONS ===
        bool canLunge =
            Time.time >= lastLungeTime + lungeCooldown &&
            distance >= minLungeRange &&
            distance <= maxLungeRange &&
            isSurging;

        if (canLunge)
        {
            SetState(ZombieState.LungeAttack);
            return;
        }

        // normal melee attack
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

    // ===================== LUNGE ATTACK =====================

    void UpdateLungeAttack()
    {
        Vector3 toPlayer = (target.position - transform.position);
        toPlayer.y = 0;
        Vector3 dir = toPlayer.normalized;

        float step = lungeSpeed * Time.deltaTime;
        transform.position += dir * step;

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist < attackRange + 0.5f)
        {
            if (target.TryGetComponent(out PlayerHealth p))
                p.TakeDamage(lungeDamage);

            lastLungeTime = Time.time;
            SetState(ZombieState.AttackRecover);
            return;
        }

        if (stateTimer >= 0.35f)
        {
            lastLungeTime = Time.time;
            SetState(ZombieState.Chase);
        }
    }

    // ===================== SURGE =====================

    void HandleSurgeLayered(float distanceToPlayer, bool inFocusZone)
    {
        if (isSurging && Time.time >= surgeEndTime)
        {
            isSurging = false;
        }

        if (isSurging) return;
        if (distanceToPlayer <= attackRange * 0.9f) return;

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

    // ===================== ATTACK WINDUP =====================

    void UpdateAttackWindup()
    {
        FaceTarget();

        if (stateTimer >= attackWindupTime)
        {
            PerformAttack();
            SetState(ZombieState.AttackRecover);
        }
    }

    void PerformAttack()
    {
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= attackRange + 0.2f)
        {
            if (target.TryGetComponent(out PlayerHealth p))
            {
                p.TakeDamage(attackDamage);
            }
        }

        lastAttackTime = Time.time;
    }

    // ===================== ATTACK RECOVER =====================

    void UpdateAttackRecover()
    {
        if (stateTimer >= attackRecoveryTime)
        {
            SetState(ZombieState.Chase);
        }
    }

    // ===================== STUN =====================

    void UpdateStunned()
    {
        if (stateTimer >= currentStunDuration)
        {
            SetState(ZombieState.Chase);
        }
    }

    // ===================== COLLISIONS (ZOMBIE SHOVE) =====================

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time < lastShoveTime + shoveCooldown)
            return;

        // shove other zombies
        if (collision.collider.TryGetComponent(out ZombieAIHybrid other))
        {
            Vector3 dir = (other.transform.position - transform.position).normalized;
            transform.position -= dir * 0.15f;
            other.transform.position += dir * 0.15f;

            lastShoveTime = Time.time;
            return;
        }

        // bumped by ragdoll
        if (collision.collider.attachedRigidbody != null)
        {
            Vector3 shove = collision.relativeVelocity * 0.05f;
            agent.Move(shove * Time.deltaTime);
        }
    }

    // ===================== SUPPORT =====================

    void MoveChaseWithPersonality(bool inFocusZone)
    {
        Vector3 toPlayer = (target.position - transform.position);
        toPlayer.y = 0f;
        Vector3 dirToPlayer = toPlayer.normalized;
        Vector3 moveDir = dirToPlayer;

        if (!inFocusZone)
        {
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
            FaceTarget();
            if (visualRenderer)
                visualRenderer.transform.localRotation = Quaternion.identity;
        }

        agent.speed = isSurging ? surgeSpeed : (inFocusZone ? focusSpeed : walkSpeed);
        agent.isStopped = false;
        agent.SetDestination(transform.position + moveDir * 2f);
    }

    void FaceTarget()
    {
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
    }

    public void HitStun(float stunTime)
    {
        if (isDead) return;
        currentStunDuration = stunTime > 0f ? stunTime : defaultStunTime;
        SetState(ZombieState.Stunned);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        SetState(ZombieState.Dead);
    }
}
