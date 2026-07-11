using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

public class EnemyRagdollController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private ZombieAIHybrid zombieAI;

    private Collider mainCollider;
    private Rigidbody rootRigidbody;

    [Header("Ragdoll Parts")]
    [Tooltip("Assign the single central hips/pelvis Rigidbody.")]
    public Rigidbody pelvisRigidbody;

    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;

    [Header("Temporary Ragdoll")]
    public float temporaryRagdollDuration = 0.25f;

    private bool isDead;
    private bool isTemporarilyRagdolled;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
        zombieAI = GetComponent<ZombieAIHybrid>();

        mainCollider = GetComponent<Collider>();
        rootRigidbody = GetComponent<Rigidbody>();

        // Never include the prefab root Rigidbody in the ragdoll.
        ragdollBodies = GetComponentsInChildren<Rigidbody>(true)
            .Where(rb => rb != null && rb != rootRigidbody)
            .ToArray();

        // Never include the main navigation capsule in ragdoll colliders.
        ragdollColliders = GetComponentsInChildren<Collider>(true)
            .Where(col => col != null && col != mainCollider)
            .ToArray();

        ConfigureJointStability();
        DisableRagdoll();
    }

    private void ConfigureJointStability()
    {
        foreach (CharacterJoint joint in GetComponentsInChildren<CharacterJoint>(true))
        {
            joint.enableCollision = false;
            joint.enableProjection = true;
        }

        foreach (ConfigurableJoint joint in GetComponentsInChildren<ConfigurableJoint>(true))
        {
            joint.enableCollision = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 5f;
        }
    }

    public void DisableRagdoll()
    {
        isTemporarilyRagdolled = false;

        // Disable ragdoll physics first.
        foreach (Rigidbody rb in ragdollBodies)
        {
            if (rb == null)
                continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        // Root Rigidbody must never become a loose ragdoll body.
        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
            rootRigidbody.useGravity = false;
        }

        if (mainCollider != null)
            mainCollider.enabled = true;

        if (animator != null)
            animator.enabled = true;

        if (agent != null)
            agent.enabled = true;

        if (zombieAI != null)
            zombieAI.enabled = true;
    }

    public void EnableRagdoll(Vector3 impulse)
    {
        if (isDead)
            return;

        isDead = true;
        isTemporarilyRagdolled = false;

        EnableRagdollPhysics();
        ApplyImpulse(impulse);
    }

    // Keeps compatibility with any old calls.
    public void EnableRagdoll()
    {
        EnableRagdoll(Vector3.zero);
    }

    private void EnableRagdollPhysics()
    {
        if (zombieAI != null)
            zombieAI.enabled = false;

        if (agent != null)
            agent.enabled = false;

        if (animator != null)
            animator.enabled = false;

        if (mainCollider != null)
            mainCollider.enabled = false;

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
            rootRigidbody.useGravity = false;
        }

        foreach (Rigidbody rb in ragdollBodies)
        {
            if (rb == null)
                continue;

            rb.isKinematic = false;
            rb.useGravity = true;
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
                col.enabled = true;
        }
    }

    public IEnumerator TemporaryRagdollBlast(Vector3 impulse)
    {
        // Prevent every shotgun pellet from starting another blast.
        if (isDead || isTemporarilyRagdolled)
            yield break;

        isTemporarilyRagdolled = true;

        EnableRagdollPhysics();
        ApplyImpulse(impulse);

        yield return new WaitForSeconds(temporaryRagdollDuration);

        if (isDead)
            yield break;

        DisableRagdoll();
    }

    private void ApplyImpulse(Vector3 impulse)
    {
        // Apply the impulse once to the central body.
        // Do not apply the full force independently to every limb.
        if (pelvisRigidbody != null)
        {
            pelvisRigidbody.AddForce(impulse, ForceMode.Impulse);
            return;
        }

        Debug.LogWarning(
            $"{name}: No pelvis Rigidbody assigned on EnemyRagdollController.",
            this
        );
    }
}