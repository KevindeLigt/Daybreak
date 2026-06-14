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

    // ---------- PERSONALITY RANDOMIZATION ----------
    [Header("Per-Zombie Personality")]
    public bool randomizePersonality = true;
    public bool appendPersonalityToName = false;

    [Tooltip("Small runtime speed variation so zombies do not move as clones.")]
    public Vector2 speedMultiplierRange = new Vector2(0.88f, 1.12f);

    [Tooltip("Runtime attack timing variation. Higher values make some zombies slower to swing.")]
    public Vector2 attackWindupMultiplierRange = new Vector2(0.85f, 1.25f);

    [Tooltip("Runtime cooldown variation so attacks do not happen in sync.")]
    public Vector2 attackCooldownMultiplierRange = new Vector2(0.85f, 1.25f);

    [Tooltip("Runtime drift amount variation. Makes some zombies direct and others more wobbly.")]
    public Vector2 driftAmountMultiplierRange = new Vector2(0.55f, 1.55f);

    [Tooltip("Runtime drift timing variation.")]
    public Vector2 driftFrequencyMultiplierRange = new Vector2(0.65f, 1.45f);

    [Tooltip("Runtime wander pause variation.")]
    public Vector2 wanderPauseMultiplierRange = new Vector2(0.75f, 1.35f);

    [Tooltip("How strongly some zombies prefer left/right approaches while chasing.")]
    public float sideBiasStrength = 0.45f;

    [Tooltip("How often chase destinations are refreshed. Small random differences reduce conga-line movement.")]
    public Vector2 destinationUpdateIntervalRange = new Vector2(0.08f, 0.22f);

    // ---------- SOFT SURROUND / APPROACH OFFSETS ----------
    [Header("Soft Surround Approach")]
    [Tooltip("When enabled, zombies aim for personal offset positions around the player instead of all targeting the player center.")]
    public bool enableApproachOffsets = true;

    [Tooltip("Personal preferred distance around the player while chasing. Keep near attack range so they still feel threatening.")]
    public Vector2 preferredApproachDistanceRange = new Vector2(1.75f, 2.75f);

    [Tooltip("How far left/right zombies try to bias their approach. Higher values create more wrapping, but can feel less direct.")]
    public Vector2 sideApproachOffsetRange = new Vector2(0.55f, 1.85f);

    [Tooltip("Some zombies should still be direct, otherwise the horde can feel too tactical.")]
    [Range(0f, 1f)] public float directApproachChance = 0.20f;

    [Tooltip("How close the zombie must be before offset behavior fades and it commits to the attack.")]
    public float closeOffsetFadeDistance = 3.0f;

    [Tooltip("Small moving offset so zombies do not hold perfectly static surround points.")]
    public float approachJitterAmount = 0.25f;

    [Tooltip("Runtime-only debug draw for the current approach destination.")]
    public bool debugDrawApproachTarget = false;

    [Tooltip("Only this percentage of zombies are allowed to lunge. Prevents synchronized lunge behavior.")]
    [Range(0f, 1f)] public float lungeEnabledChance = 0.30f;

    [Header("Zombie Hesitation")]
    public bool enableChaseHesitation = true;

    [Tooltip("Base chance that a zombie briefly hesitates when its hesitation timer fires.")]
    [Range(0f, 1f)] public float baseHesitationChance = 0.12f;

    public Vector2 hesitationIntervalRange = new Vector2(2.0f, 4.5f);
    public Vector2 hesitationDurationRange = new Vector2(0.12f, 0.38f);

    [Header("NavMesh Desync")]
    [Range(0, 99)] public int avoidancePriorityMin = 20;
    [Range(0, 99)] public int avoidancePriorityMax = 80;
    public Vector2 agentAccelerationMultiplierRange = new Vector2(0.85f, 1.2f);
    public Vector2 agentAngularSpeedMultiplierRange = new Vector2(0.8f, 1.25f);

    private enum ZombiePersonality
    {
        Shambler,
        Hungry,
        Drifter,
        Surger,
        Clumsy
    }

    private ZombiePersonality personality;

    // Runtime personality values. Public inspector values stay as the base tuning.
    private float pWalkSpeed;
    private float pFocusSpeed;
    private float pSurgeSpeed;
    private float pSurgeIntervalMin;
    private float pSurgeIntervalMax;
    private float pSurgeDuration;
    private float pFocusDistance;
    private float pDriftAmount;
    private float pDriftFrequency;
    private float pAttackRange;
    private float pAttackWindupTime;
    private float pAttackRecoveryTime;
    private float pAttackCooldown;
    private float pWanderRadius;
    private float pWanderPauseMin;
    private float pWanderPauseMax;
    private float pLungeCooldown;
    private float pLungeSpeed;
    private float pLungeDamage;
    private float pHesitationChance;
    private float pDestinationUpdateInterval;
    private float pPreferredApproachDistance;
    private float pSideApproachOffset;
    private float pApproachJitterSeed;
    private float pApproachJitterFrequency;

    private Vector3 lastApproachDestination;

    private float sideBias = 0f;
    private float visualWobbleSeed = 0f;
    private bool personalityCanLunge = true;

    private float nextDestinationUpdateTime = 0f;
    private bool isHesitating = false;
    private float hesitationEndTime = 0f;
    private float nextHesitationTime = 0f;

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

        ApplyPersonality();

        if (!target)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) target = playerObj.transform;
        }

        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<Renderer>();

        SetState(ZombieState.Wander);
        ScheduleNextSurge();
        ScheduleNextHesitation();

        if (idleAudioSource && idleLoopClip)
        {
            idleAudioSource.clip = idleLoopClip;
            idleAudioSource.loop = true;
            idleAudioSource.pitch = Random.Range(0.88f, 1.14f);
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
            case ZombieState.LungeAttack: UpdateLungeAttack(); break;
            case ZombieState.Dead: break;
        }
    }

    // ===================== PERSONALITY =====================

    void ApplyPersonality()
    {
        pWalkSpeed = walkSpeed;
        pFocusSpeed = focusSpeed;
        pSurgeSpeed = surgeSpeed;
        pSurgeIntervalMin = surgeIntervalMin;
        pSurgeIntervalMax = surgeIntervalMax;
        pSurgeDuration = surgeDuration;
        pFocusDistance = focusDistance;
        pDriftAmount = driftAmount;
        pDriftFrequency = driftFrequency;
        pAttackRange = attackRange;
        pAttackWindupTime = attackWindupTime;
        pAttackRecoveryTime = attackRecoveryTime;
        pAttackCooldown = attackCooldown;
        pWanderRadius = wanderRadius;
        pWanderPauseMin = wanderPauseMin;
        pWanderPauseMax = wanderPauseMax;
        pLungeCooldown = lungeCooldown;
        pLungeSpeed = lungeSpeed;
        pLungeDamage = lungeDamage;
        pHesitationChance = baseHesitationChance;
        pDestinationUpdateInterval = RandomRange(destinationUpdateIntervalRange);

        driftPhase = Random.Range(0f, Mathf.PI * 2f);
        visualWobbleSeed = Random.Range(0f, 100f);
        sideBias = Random.value < 0.5f ? -1f : 1f;
        sideBias *= Random.Range(0.25f, 1f);
        personalityCanLunge = Random.value <= lungeEnabledChance;

        if (!randomizePersonality)
        {
            InitializeApproachOffsetPersonality();
            ApplyAgentDesync();
            return;
        }

        personality = (ZombiePersonality)Random.Range(0, System.Enum.GetValues(typeof(ZombiePersonality)).Length);

        float speedMult = RandomRange(speedMultiplierRange);
        float attackWindupMult = RandomRange(attackWindupMultiplierRange);
        float attackCooldownMult = RandomRange(attackCooldownMultiplierRange);
        float driftAmountMult = RandomRange(driftAmountMultiplierRange);
        float driftFrequencyMult = RandomRange(driftFrequencyMultiplierRange);
        float wanderPauseMult = RandomRange(wanderPauseMultiplierRange);

        pWalkSpeed *= speedMult;
        pFocusSpeed *= speedMult;
        pSurgeSpeed *= speedMult;
        pAttackWindupTime *= attackWindupMult;
        pAttackCooldown *= attackCooldownMult;
        pDriftAmount *= driftAmountMult;
        pDriftFrequency *= driftFrequencyMult;
        pWanderPauseMin *= wanderPauseMult;
        pWanderPauseMax *= wanderPauseMult;

        switch (personality)
        {
            case ZombiePersonality.Shambler:
                pWalkSpeed *= 0.86f;
                pFocusSpeed *= 0.92f;
                pAttackWindupTime *= 1.18f;
                pAttackCooldown *= 1.12f;
                pDriftAmount *= 1.15f;
                pHesitationChance += 0.08f;
                personalityCanLunge = false;
                break;

            case ZombiePersonality.Hungry:
                pWalkSpeed *= 1.10f;
                pFocusSpeed *= 1.12f;
                pAttackWindupTime *= 0.88f;
                pAttackCooldown *= 0.88f;
                pDriftAmount *= 0.65f;
                pHesitationChance *= 0.55f;
                sideBias *= 0.35f;
                break;

            case ZombiePersonality.Drifter:
                pWalkSpeed *= 0.96f;
                pFocusSpeed *= 0.96f;
                pDriftAmount *= 1.55f;
                pDriftFrequency *= 1.15f;
                pFocusDistance += 0.35f;
                pHesitationChance += 0.04f;
                personalityCanLunge = false;
                break;

            case ZombiePersonality.Surger:
                pWalkSpeed *= 1.02f;
                pFocusSpeed *= 1.03f;
                pSurgeSpeed *= 1.22f;
                pSurgeIntervalMin *= 0.72f;
                pSurgeIntervalMax *= 0.82f;
                pSurgeDuration *= 1.08f;
                pDriftAmount *= 0.85f;
                break;

            case ZombiePersonality.Clumsy:
                pWalkSpeed *= 0.91f;
                pFocusSpeed *= 0.94f;
                pAttackWindupTime *= 1.25f;
                pAttackRecoveryTime *= 1.18f;
                pDriftAmount *= 1.35f;
                pHesitationChance += 0.14f;
                personalityCanLunge = false;
                break;
        }

        // Clamp so extreme inspector values do not create broken zombies.
        pWalkSpeed = Mathf.Max(0.2f, pWalkSpeed);
        pFocusSpeed = Mathf.Max(0.2f, pFocusSpeed);
        pSurgeSpeed = Mathf.Max(pFocusSpeed, pSurgeSpeed);
        pAttackWindupTime = Mathf.Max(0.1f, pAttackWindupTime);
        pAttackCooldown = Mathf.Max(0.25f, pAttackCooldown);
        pDestinationUpdateInterval = Mathf.Max(0.02f, pDestinationUpdateInterval);
        pHesitationChance = Mathf.Clamp01(pHesitationChance);

        // Desync first available attacks/surges so a group does not act together.
        lastAttackTime = Time.time - Random.Range(0f, pAttackCooldown);
        lastLungeTime = Time.time - Random.Range(0f, pLungeCooldown);
        nextDestinationUpdateTime = Time.time + Random.Range(0f, pDestinationUpdateInterval);

        InitializeApproachOffsetPersonality();

        if (appendPersonalityToName)
            gameObject.name = $"{gameObject.name}_{personality}";

        ApplyAgentDesync();
    }

    void ApplyAgentDesync()
    {
        if (!agent) return;

        int min = Mathf.Min(avoidancePriorityMin, avoidancePriorityMax);
        int max = Mathf.Max(avoidancePriorityMin, avoidancePriorityMax);
        agent.avoidancePriority = Random.Range(min, max + 1);

        agent.acceleration *= RandomRange(agentAccelerationMultiplierRange);
        agent.angularSpeed *= RandomRange(agentAngularSpeedMultiplierRange);
    }

    float RandomRange(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }

    void InitializeApproachOffsetPersonality()
    {
        pPreferredApproachDistance = RandomRange(preferredApproachDistanceRange);
        pSideApproachOffset = RandomRange(sideApproachOffsetRange);

        float sideSign = Mathf.Abs(sideBias) > 0.01f ? Mathf.Sign(sideBias) : (Random.value < 0.5f ? -1f : 1f);
        pSideApproachOffset *= sideSign;

        // A few zombies should remain direct. This stops the horde from feeling too clever or too neatly spaced.
        if (Random.value <= directApproachChance)
        {
            pSideApproachOffset *= Random.Range(0.1f, 0.35f);
            pPreferredApproachDistance *= Random.Range(0.9f, 1.05f);
        }

        // Personality flavor: still the same enemy, but different approach instincts.
        if (randomizePersonality)
        {
            switch (personality)
            {
                case ZombiePersonality.Hungry:
                    pPreferredApproachDistance *= 0.88f;
                    pSideApproachOffset *= 0.45f;
                    break;

                case ZombiePersonality.Drifter:
                    pPreferredApproachDistance *= 1.08f;
                    pSideApproachOffset *= 1.35f;
                    break;

                case ZombiePersonality.Surger:
                    pPreferredApproachDistance *= 0.95f;
                    pSideApproachOffset *= 0.75f;
                    break;

                case ZombiePersonality.Shambler:
                    pPreferredApproachDistance *= 1.05f;
                    pSideApproachOffset *= 1.05f;
                    break;

                case ZombiePersonality.Clumsy:
                    pPreferredApproachDistance *= 1.12f;
                    pSideApproachOffset *= 1.15f;
                    break;
            }
        }

        pPreferredApproachDistance = Mathf.Max(pAttackRange + 0.15f, pPreferredApproachDistance);
        pSideApproachOffset = Mathf.Clamp(pSideApproachOffset, -3.0f, 3.0f);

        pApproachJitterSeed = Random.Range(0f, 100f);
        pApproachJitterFrequency = Random.Range(0.35f, 0.9f);
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
                isHesitating = false;
                agent.isStopped = false;
                agent.speed = pWalkSpeed * 0.7f;
                PickNewWanderTarget();
                break;

            case ZombieState.Chase:
                isHesitating = false;
                agent.isStopped = false;
                agent.speed = pWalkSpeed;
                ScheduleNextHesitation();
                break;

            case ZombieState.AttackWindup:
                isHesitating = false;
                agent.isStopped = true;
                if (visualRenderer)
                {
                    visualRenderer.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
                    visualRenderer.material.color = attackColor;
                }
                break;

            case ZombieState.LungeAttack:
                isHesitating = false;
                agent.isStopped = true;
                Animator anim = GetComponent<Animator>();
                if (anim)
                    anim.SetTrigger("LungeTrigger");
                break;

            case ZombieState.Stunned:
                isHesitating = false;
                agent.isStopped = true;
                if (visualRenderer)
                    visualRenderer.material.color = stunColor;
                break;

            case ZombieState.Dead:
                isHesitating = false;
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
                currentWanderPause = Random.Range(pWanderPauseMin, pWanderPauseMax);
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
        Vector2 rnd = Random.insideUnitCircle * pWanderRadius;
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

        bool inFocusZone = distance <= pFocusDistance;

        if (TryHandleChaseHesitation(distance, inFocusZone))
            return;

        bool canLunge =
            personalityCanLunge &&
            Time.time >= lastLungeTime + pLungeCooldown &&
            distance >= minLungeRange &&
            distance <= maxLungeRange &&
            isSurging;

        if (canLunge)
        {
            SetState(ZombieState.LungeAttack);
            return;
        }

        if (distance <= pAttackRange)
        {
            if (Time.time >= lastAttackTime + pAttackCooldown)
            {
                SetState(ZombieState.AttackWindup);
                return;
            }
        }

        MoveChaseWithPersonality(inFocusZone);
        HandleSurgeLayered(distance, inFocusZone);
    }

    bool TryHandleChaseHesitation(float distance, bool inFocusZone)
    {
        if (!enableChaseHesitation)
            return false;

        // Do not randomly pause when already close enough to threaten the player.
        if (inFocusZone || distance <= pAttackRange + 0.5f || isSurging)
            return false;

        if (isHesitating)
        {
            agent.isStopped = true;

            if (visualRenderer)
            {
                visualRenderer.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin((Time.time + visualWobbleSeed) * 5f) * 4f,
                    0f,
                    Mathf.Sin((Time.time + visualWobbleSeed) * 4f) * 4f
                );
            }

            if (Time.time >= hesitationEndTime)
            {
                isHesitating = false;
                agent.isStopped = false;
                ScheduleNextHesitation();
            }

            return true;
        }

        if (Time.time >= nextHesitationTime && Random.value <= pHesitationChance)
        {
            isHesitating = true;
            hesitationEndTime = Time.time + RandomRange(hesitationDurationRange);
            agent.isStopped = true;
            return true;
        }

        if (Time.time >= nextHesitationTime)
            ScheduleNextHesitation();

        return false;
    }

    void ScheduleNextHesitation()
    {
        nextHesitationTime = Time.time + RandomRange(hesitationIntervalRange);
    }

    // ===================== LUNGE ATTACK =====================

    void UpdateLungeAttack()
    {
        Vector3 toPlayer = (target.position - transform.position);
        toPlayer.y = 0;
        Vector3 dir = toPlayer.normalized;

        float step = pLungeSpeed * Time.deltaTime;
        transform.position += dir * step;

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist < pAttackRange + 0.5f)
        {
            if (target.TryGetComponent(out PlayerHealth p))
                p.TakeDamage(pLungeDamage);

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
        if (distanceToPlayer <= pAttackRange * 0.9f) return;

        if (Time.time >= nextSurgeTime)
        {
            StartSurge();
        }
    }

    void StartSurge()
    {
        isSurging = true;
        surgeEndTime = Time.time + pSurgeDuration;
        ScheduleNextSurge();
    }

    void ScheduleNextSurge()
    {
        nextSurgeTime = Time.time + Random.Range(pSurgeIntervalMin, pSurgeIntervalMax);
    }

    // ===================== ATTACK WINDUP =====================

    void UpdateAttackWindup()
    {
        FaceTarget();

        if (stateTimer >= pAttackWindupTime)
        {
            PerformAttack();
            SetState(ZombieState.AttackRecover);
        }
    }

    void PerformAttack()
    {
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= pAttackRange + 0.2f)
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
        if (stateTimer >= pAttackRecoveryTime)
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

        if (collision.collider.TryGetComponent(out ZombieAIHybrid other))
        {
            Vector3 dir = (other.transform.position - transform.position).normalized;
            transform.position -= dir * shoveForce * 0.075f;
            other.transform.position += dir * shoveForce * 0.075f;

            lastShoveTime = Time.time;
            return;
        }

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

        if (toPlayer.sqrMagnitude < 0.001f)
            return;

        float distanceToPlayer = toPlayer.magnitude;
        Vector3 dirToPlayer = toPlayer.normalized;

        Vector3 approachDestination = GetApproachDestination(dirToPlayer, distanceToPlayer, inFocusZone);
        Vector3 toApproachDestination = approachDestination - transform.position;
        toApproachDestination.y = 0f;

        Vector3 dirToApproachDestination = toApproachDestination.sqrMagnitude > 0.001f
            ? toApproachDestination.normalized
            : dirToPlayer;

        Vector3 moveDir = dirToApproachDestination;
        Vector3 extraDrift = Vector3.zero;

        if (!inFocusZone)
        {
            driftPhase += Time.deltaTime * pDriftFrequency;

            Vector3 sideways = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
            float wavyDrift = Mathf.Sin(driftPhase + visualWobbleSeed) * pDriftAmount;
            float biasedDrift = sideBias * sideBiasStrength;

            extraDrift = sideways * (wavyDrift + biasedDrift);
            moveDir = (dirToApproachDestination + extraDrift).normalized;

            if (visualRenderer)
            {
                visualRenderer.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin((Time.time + visualWobbleSeed) * 3f) * 5f,
                    0f,
                    Mathf.Sin((Time.time + visualWobbleSeed) * 2f) * 5f
                );
            }
        }
        else
        {
            FaceTarget();
            if (visualRenderer)
                visualRenderer.transform.localRotation = Quaternion.identity;
        }

        agent.speed = isSurging ? pSurgeSpeed : (inFocusZone ? pFocusSpeed : pWalkSpeed);
        agent.isStopped = false;

        Vector3 destination;

        if (enableApproachOffsets)
        {
            // Aim for the personal surround point, with a little of the old drift layered on top.
            // This makes the group wrap and stagger instead of all targeting the same player center.
            destination = approachDestination + extraDrift;
        }
        else
        {
            // Original steering behavior.
            destination = transform.position + moveDir * 2f;
        }

        SetChaseDestination(destination);
    }

    Vector3 GetApproachDestination(Vector3 dirToPlayer, float distanceToPlayer, bool inFocusZone)
    {
        if (!enableApproachOffsets || target == null)
            return target.position;

        Vector3 sideways = Vector3.Cross(Vector3.up, dirToPlayer).normalized;

        // Fade offsets close to attack range. Zombies should wrap while approaching,
        // but commit when they are close enough to actually threaten the player.
        float fadeStart = Mathf.Max(pAttackRange + 0.15f, closeOffsetFadeDistance);
        float offsetWeight = Mathf.InverseLerp(pAttackRange + 0.1f, fadeStart, distanceToPlayer);

        float jitter = Mathf.Sin((Time.time + pApproachJitterSeed) * pApproachJitterFrequency) * approachJitterAmount;

        Vector3 desired = target.position
            - dirToPlayer * (pPreferredApproachDistance * offsetWeight)
            + sideways * ((pSideApproachOffset + jitter) * offsetWeight);

        // Keep the target point valid on the NavMesh. If this fails, fall back to the player position.
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            lastApproachDestination = hit.position;
            return hit.position;
        }

        lastApproachDestination = target.position;
        return target.position;
    }

    void SetChaseDestination(Vector3 destination)
    {
        if (Time.time < nextDestinationUpdateTime)
            return;

        if (!agent || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.SetDestination(destination);
        nextDestinationUpdateTime = Time.time + pDestinationUpdateInterval;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseInterestRadius);

        if (!debugDrawApproachTarget || !Application.isPlaying)
            return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(lastApproachDestination, 0.25f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, lastApproachDestination + Vector3.up * 0.2f);
    }
}
