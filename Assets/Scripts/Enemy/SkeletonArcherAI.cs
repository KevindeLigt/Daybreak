using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class SkeletonArcherAI : MonoBehaviour
{
    public Transform target;

    [Header("Detection")]
    public float detectionRadius = 25f;
    public float loseInterestRadius = 35f;

    [Header("Combat Distances")]
    public float idealMinDistance = 10f;
    public float idealMaxDistance = 18f;

    [Header("Line of Sight")]
    public LayerMask losMask;  // Set to: Default, Environment, etc.
    public float losMaxDistance = 60f;

    [Header("Attack Settings")]
    public float windupTime = 1.0f;
    public float recoveryTime = 0.6f;
    public float attackCooldown = 2.5f;
    public float arrowDamage = 12f;

    [Header("Projectile")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public float arrowSpeed = 40f;

    [Header("Telegraph")]
    public GameObject telegraphPrefab;   // The visual pre-shot warning
    private TelegraphBeam activeTelegraph;

    [Header("Animation")]
    public Animator anim;
    public string aimTrigger = "Aim";
    public string fireTrigger = "Fire";
    public string hurtTrigger = "Hurt";
    public string deathTrigger = "Death";

    private NavMeshAgent agent;

    private enum State { Idle, MaintainDistance, Strafe, AimWindup, FireShot, Recover, Stunned, Dead }
    private State state = State.Idle;

    private float stateTimer = 0f;
    private float lastAttackTime = -999f;
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (!target)
        {
            target = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        SetState(State.Idle);
    }

    void Update()
    {
        if (isDead || !target) return;

        stateTimer += Time.deltaTime;

        switch (state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.MaintainDistance: UpdateMaintainDistance(); break;
            case State.Strafe: UpdateStrafe(); break;
            case State.AimWindup: UpdateAimWindup(); break;
            case State.FireShot: UpdateFireShot(); break;
            case State.Recover: UpdateRecover(); break;
            case State.Stunned: UpdateStunned(); break;
            case State.Dead: break;
        }
    }

    // ---------------------------------------------------------------------------
    // STATE MACHINE
    // ---------------------------------------------------------------------------

    void SetState(State newState)
    {
        if (isDead) return;

        OnExitState(state);
        state = newState;
        stateTimer = 0f;
        OnEnterState(newState);
    }

    void OnEnterState(State s)
    {
        switch (s)
        {
            case State.Idle:
                agent.isStopped = true;
                break;

            case State.MaintainDistance:
                agent.isStopped = false;
                break;

            case State.Strafe:
                agent.isStopped = false;
                break;

            case State.AimWindup:
                agent.isStopped = true;
                if (anim) anim.SetTrigger(aimTrigger);

                // Spawn telegraph
                if (telegraphPrefab)
                {
                    GameObject go = Instantiate(telegraphPrefab, arrowSpawnPoint.position, Quaternion.identity);
                    activeTelegraph = go.GetComponent<TelegraphBeam>();
                    activeTelegraph.Initialize(arrowSpawnPoint, target, windupTime);
                }
                break;

            case State.FireShot:
                agent.isStopped = true;
                if (anim) anim.SetTrigger(fireTrigger);
                break;

            case State.Recover:
                agent.isStopped = true;
                break;

            case State.Stunned:
                agent.isStopped = true;
                if (anim) anim.SetTrigger(hurtTrigger);
                break;
        }
    }

    void OnExitState(State s)
    {
        if (s == State.AimWindup && activeTelegraph)
        {
            Destroy(activeTelegraph.gameObject);
        }
    }

    // ---------------------------------------------------------------------------
    // STATE LOGIC
    // ---------------------------------------------------------------------------

    void UpdateIdle()
    {
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist < detectionRadius)
        {
            SetState(State.MaintainDistance);
        }
    }

    void UpdateMaintainDistance()
    {
        float dist = Vector3.Distance(transform.position, target.position);

        // Lost the player?
        if (dist > loseInterestRadius)
        {
            SetState(State.Idle);
            return;
        }

        // Too close → move back
        if (dist < idealMinDistance)
        {
            Vector3 dir = (transform.position - target.position).normalized;
            agent.SetDestination(transform.position + dir * 5f);
        }
        // Too far → move closer
        else if (dist > idealMaxDistance)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            agent.SetDestination(transform.position + dir * 5f);
        }
        else
        {
            // At ideal distance → maybe start strafing
            if (Random.value < 0.003f)   // ~0.3% chance per frame → occasional strafe
            {
                SetState(State.Strafe);
                return;
            }

            // Try attacking
            if (Time.time > lastAttackTime + attackCooldown)
            {
                if (HasLineOfSight())
                    SetState(State.AimWindup);
            }
        }
    }

    private bool HasLineOfSight()
    {
        if (!arrowSpawnPoint || !target) return false;

        Vector3 dir = (target.position - arrowSpawnPoint.position);
        float dist = dir.magnitude;

        if (Physics.Raycast(
            arrowSpawnPoint.position,
            dir.normalized,
            out RaycastHit hit,
            losMaxDistance,
            losMask
        ))
        {
            return hit.collider.CompareTag("Player");
        }

        return false;
    }

    void UpdateStrafe()
    {
        Vector3 right = transform.right;
        Vector3 dir = (Random.value > 0.5f) ? right : -right;

        agent.SetDestination(transform.position + dir * 4f);

        if (stateTimer > 0.8f)
        {
            SetState(State.MaintainDistance);
        }
    }

    void UpdateAimWindup()
    {
        FaceTarget();

        if (stateTimer >= windupTime)
        {
            SetState(State.FireShot);
        }
    }

    void UpdateFireShot()
    {
        FireArrow();

        lastAttackTime = Time.time;

        SetState(State.Recover);
    }

    void UpdateRecover()
    {
        if (stateTimer >= recoveryTime)
        {
            SetState(State.MaintainDistance);
        }
    }

    void UpdateStunned()
    {
        if (stateTimer >= 0.3f)
        {
            SetState(State.MaintainDistance);
        }
    }

    // ---------------------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------------------
    void FaceTarget()
    {
        FaceTargetHorizontal();
        FaceTargetVertical();
    }

    void FaceTargetHorizontal()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f; // flatten for body rotation
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
        }
    }

    void FaceTargetVertical()
    {
        if (!arrowSpawnPoint) return;

        Vector3 dir = target.position - arrowSpawnPoint.position;
        Quaternion rot = Quaternion.LookRotation(dir);
        arrowSpawnPoint.rotation = Quaternion.Slerp(arrowSpawnPoint.rotation, rot, Time.deltaTime * 15f);
    }

    private void FireArrow()
    {
        if (!arrowPrefab || !arrowSpawnPoint) return;

        Vector3 dir = (target.position - arrowSpawnPoint.position).normalized;

        GameObject arrow = Instantiate(arrowPrefab, arrowSpawnPoint.position, Quaternion.LookRotation(dir));
        SkeletonArrow arr = arrow.GetComponent<SkeletonArrow>();

        if (arr)
        {
            arr.damage = arrowDamage;
            arr.speed = arrowSpeed;
            arr.SetDirection(dir);   // NEW
        }
    }

    public void HitStun(float duration = 0.3f)
    {
        if (isDead) return;

        SetState(State.Stunned);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        if (anim) anim.SetTrigger(deathTrigger);

        SetState(State.Dead);

        GetComponent<NavMeshAgent>().isStopped = true;
    }
}
