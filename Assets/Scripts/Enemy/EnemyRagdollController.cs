using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyRagdollController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private ZombieAI zombieAI;
    private Collider mainCollider;
    private Rigidbody rootRB;

    [Header("Ragdoll Parts")]
    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;

    private bool isDead = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        zombieAI = GetComponent<ZombieAI>();
        mainCollider = GetComponent<Collider>();
        rootRB = GetComponent<Rigidbody>();

        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        DisableRagdoll();
    }

    public void DisableRagdoll()
    {
        // Ragdoll OFF: AI / animator / main collider ON, bones kinematic & colliders disabled
        if (agent) agent.enabled = true;
        if (zombieAI) zombieAI.enabled = true;
        if (animator) animator.enabled = true;
        if (mainCollider) mainCollider.enabled = true;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            // Keep rootRB kinematic as well (should already be)
            rb.isKinematic = true;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            // Keep only the main collider enabled in non-ragdoll mode
            if (col != mainCollider)
                col.enabled = false;
        }
    }

    public void EnableRagdoll()
    {
        isDead = true;

        if (agent) agent.enabled = false;
        if (zombieAI) zombieAI.enabled = false;
        if (animator) animator.enabled = false;
        if (mainCollider) mainCollider.enabled = false;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = true;
        }
    }

    public IEnumerator TemporaryRagdollBlast(Vector3 force)
    {
        if (isDead) yield break; // don't recover if already dead

        // Turn AI off
        if (agent) agent.enabled = false;
        if (zombieAI) zombieAI.enabled = false;
        if (animator) animator.enabled = false;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
            rb.AddForce(force, ForceMode.Impulse);
        }

        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = true;
        }

        yield return new WaitForSeconds(0.25f);

        // If enemy died during this time, do not restore
        if (isDead) yield break;

        // Restore non-ragdoll state
        DisableRagdoll();
    }
}
