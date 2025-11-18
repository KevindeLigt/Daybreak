using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ZombieAIHybrid : MonoBehaviour
{
    public Transform target;

    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float surgeSpeed = 4f;
    public float surgeIntervalMin = 2f;
    public float surgeIntervalMax = 5f;
    public float surgeDuration = 0.7f;

    public float driftAmount = 0.6f;
    public float driftSpeed = 0.6f;

    [Header("Attack Settings")]
    public float attackRange = 1.6f;
    public float attackWindup = 0.4f;
    public float attackCooldown = 1.2f;
    public float attackDamage = 10f;

    [Header("Debug Visual")]
    public Renderer cubeVisual;     // assign your cube MeshRenderer

    private NavMeshAgent agent;
    private float lastAttackTime = -999f;
    private float nextSurgeTime = 0f;
    private bool isSurging = false;
    private bool isWindup = false;
    private bool isHitStunned = false;
    private bool isFocusedMode = false;

    private float driftValue = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;

        if (!target)
            target = GameObject.FindGameObjectWithTag("Player")?.transform;

        ScheduleNextSurge();
    }

    void Update()
    {
        if (!target || isHitStunned) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= attackRange + 0.3f)
        {
            EnterFocusedMode();
            TryAttack();
            StopMovement();
            return;
        }

        ExitFocusedMode();
        MoveWithPersonality();
        HandleSurgeLogic();
    }


    private void MoveWithPersonality()
    {
        Vector3 dirToPlayer = (target.position - transform.position).normalized;

        Vector3 finalDir = dirToPlayer;

        if (!isFocusedMode)
        {
            // drifting sideways noise only when NOT close
            driftValue += Time.deltaTime * driftSpeed;
            Vector3 drift = transform.right * Mathf.Sin(driftValue) * driftAmount;
            finalDir = (dirToPlayer + drift).normalized;

            // personality tilt
            if (cubeVisual)
            {
                cubeVisual.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(Time.time * 3f) * 5f,
                    0,
                    Mathf.Sin(Time.time * 2f) * 5f
                );
            }
        }

        agent.SetDestination(transform.position + finalDir * 3f);
    }


    private void HandleSurgeLogic()
    {
        if (Time.time >= nextSurgeTime && !isSurging)
        {
            StartCoroutine(SurgeRoutine());
        }
    }

    private IEnumerator SurgeRoutine()
    {
        isSurging = true;
        agent.speed = surgeSpeed;

        // visual tilt forward (temporary)
        if (cubeVisual)
            cubeVisual.transform.localRotation = Quaternion.Euler(25f, 0, 0);

        yield return new WaitForSeconds(surgeDuration);

        agent.speed = walkSpeed;
        isSurging = false;
        ScheduleNextSurge();
    }

    private void ScheduleNextSurge()
    {
        nextSurgeTime = Time.time + Random.Range(surgeIntervalMin, surgeIntervalMax);
    }

    private void StopMovement()
    {
        agent.SetDestination(transform.position);
    }

    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        StartCoroutine(AttackWindupRoutine());
    }

    private IEnumerator AttackWindupRoutine()
    {
        isWindup = true;

        // telegraph: tilt cube forward & flash color
        if (cubeVisual)
        {
            cubeVisual.transform.localRotation = Quaternion.Euler(35f, 0, 0);
            cubeVisual.material.color = Color.red;
        }

        yield return new WaitForSeconds(attackWindup);

        if (cubeVisual)
            cubeVisual.material.color = Color.white;

        // deal damage
        if (Vector3.Distance(transform.position, target.position) <= attackRange + 0.3f)
        {
            if (target.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(attackDamage);
            }
            yield return new WaitForSeconds(0.25f);
            ExitFocusedMode();
        }

        lastAttackTime = Time.time;
        isWindup = false;
    }

    private void EnterFocusedMode()
    {
        if (!isFocusedMode)
        {
            isFocusedMode = true;
            driftValue = 0f; // Reset drift
        }

        // lock visual upright
        if (cubeVisual)
            cubeVisual.transform.localRotation = Quaternion.identity;

        agent.speed = walkSpeed * 1.2f; // slight aggression bump
    }

    private void ExitFocusedMode()
    {
        if (isFocusedMode)
        {
            isFocusedMode = false;
            agent.speed = walkSpeed;
        }
    }


    public void HitStun(float time)
    {
        StartCoroutine(HitStunRoutine(time));
    }

    private IEnumerator HitStunRoutine(float time)
    {
        isHitStunned = true;
        agent.isStopped = true;
        if (cubeVisual)
            cubeVisual.transform.localRotation = Quaternion.Euler(35f, 0, 0);

        if (cubeVisual)
            cubeVisual.material.color = Color.yellow;

        yield return new WaitForSeconds(time);

        if (cubeVisual)
            cubeVisual.material.color = Color.white;

        agent.isStopped = false;
        isHitStunned = false;
    }
}
