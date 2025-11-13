using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    [Header("References")]
    public Transform target;

    [Header("Settings")]
    public float attackRange = 1.5f;
    public float moveSpeed = 3f;
    public float rotationSpeed = 5f;
    public float circleDistance = 2.5f;
    public float circleSpeed = 2f;
    public float attackCooldown = 1.5f;

    private NavMeshAgent agent;
    private float lastAttackTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        // Fallback: auto-find player if not assigned
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
        }
    }

    void Update()
    {
        if (!target) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance > attackRange)
        {
            MoveAroundPlayer();
        }
        else
        {
            TryAttack();
        }
    }

    void MoveAroundPlayer()
    {
        // Calculate a circling offset to spread zombies around
        float angle = Mathf.Atan2(transform.position.z - target.position.z, transform.position.x - target.position.x);
        angle += Mathf.Sin(Time.time * circleSpeed + GetInstanceID() * 0.1f) * 0.5f; // Add variation

        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * circleDistance;
        Vector3 destination = target.position + offset;

        agent.SetDestination(destination);
    }

    void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;
        // Simple placeholder for attack
        Debug.Log($"{name} attacks the player!");
    }
}
