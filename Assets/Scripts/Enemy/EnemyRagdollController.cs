using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class EnemyRagdollController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private ZombieAIController zombieAI;

    private Collider mainCollider;
    private Rigidbody rootRigidbody;

    [Header("Ragdoll Parts")]
    [Tooltip("Assign the single central hips/pelvis Rigidbody.")]
    public Rigidbody pelvisRigidbody;

    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;

    private bool isDead;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
        zombieAI = GetComponent<ZombieAIController>();

        mainCollider = GetComponent<Collider>();
        rootRigidbody = GetComponent<Rigidbody>();

        // Do not include the living prefab root Rigidbody.
        ragdollBodies = GetComponentsInChildren<Rigidbody>(true)
            .Where(rb => rb != null && rb != rootRigidbody)
            .ToArray();

        // Do not include the living navigation capsule.
        ragdollColliders = GetComponentsInChildren<Collider>(true)
            .Where(col => col != null && col != mainCollider)
            .ToArray();

        ConfigureJointStability();
    }

    private void OnEnable()
    {
        if (!isDead)
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

    /// <summary>
    /// Clears motion only while Unity still considers the body dynamic, then
    /// returns it to the non-physical animated state. Assigning velocity to an
    /// already-kinematic Rigidbody is unsupported and produces one warning per
    /// ragdoll bone.
    /// </summary>
    private static void MakeKinematicAndDisablePhysics(Rigidbody rb)
    {
        if (rb == null)
            return;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
    }

    /// <summary>
    /// Animated living state. Ragdoll physics are disabled.
    /// </summary>
    public void DisableRagdoll()
    {
        if (isDead)
            return;

        foreach (Rigidbody rb in ragdollBodies)
        {
            MakeKinematicAndDisablePhysics(rb);
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        MakeKinematicAndDisablePhysics(rootRigidbody);

        if (mainCollider != null)
            mainCollider.enabled = true;

        if (animator != null)
        {
            animator.enabled = true;
            animator.speed = 1f;
        }

        // Read the serialized mode even if EnemyHealth.Awake has not run yet.
        // Prevent the reset path from re-enabling AI on a stationary dummy.
        EnemyHealth health = GetComponent<EnemyHealth>();
        bool isTrainingDummy = health != null && health.IsTrainingDummy;
        if (agent != null)
            agent.enabled = !isTrainingDummy;

        if (zombieAI != null)
            zombieAI.enabled = !isTrainingDummy;

        if (isTrainingDummy && animator != null)
        {
            animator.applyRootMotion = false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == "MoveSpeed" &&
                    parameter.type == AnimatorControllerParameterType.Float)
                    animator.SetFloat("MoveSpeed", 0f);
            }
        }
    }

    /// <summary>
    /// Permanent full-body ragdoll used for normal death.
    /// </summary>
    public void EnableRagdoll(Vector3 impulse)
    {
        if (isDead)
            return;

        isDead = true;

        StopLivingSystems();

        // Enable physical colliders before making bodies dynamic.
        foreach (Collider col in ragdollColliders)
        {
            if (col == null)
                continue;

            col.isTrigger = false;
            col.enabled = true;
        }

        foreach (Rigidbody rb in ragdollBodies)
        {
            if (rb == null)
                continue;

            rb.detectCollisions = true;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        Physics.SyncTransforms();
        ApplyImpulse(impulse);
    }

    public void EnableRagdoll()
    {
        EnableRagdoll(Vector3.zero);
    }

    /// <summary>
    /// Permanently disables the original animated and physical body.
    /// Separate gib prefabs are spawned by EnemyEviscerationController.
    /// </summary>
    public void DisableBodyForEvisceration()
    {
        if (isDead)
            return;

        isDead = true;

        StopAllCoroutines();
        StopLivingSystems();

        MakeKinematicAndDisablePhysics(rootRigidbody);

        foreach (Rigidbody rb in ragdollBodies)
        {
            MakeKinematicAndDisablePhysics(rb);
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        Physics.SyncTransforms();
    }

    private void StopLivingSystems()
    {
        if (zombieAI != null)
            zombieAI.enabled = false;

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        if (animator != null)
            animator.enabled = false;

        if (mainCollider != null)
            mainCollider.enabled = false;

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
            rootRigidbody.useGravity = false;
            rootRigidbody.detectCollisions = false;
        }
    }

    private void ApplyImpulse(Vector3 impulse)
    {
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
