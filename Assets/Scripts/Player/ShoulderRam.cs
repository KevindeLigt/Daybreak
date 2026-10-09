using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[DefaultExecutionOrder(-100)]
public class ShoulderRam : MonoBehaviour
{
    [Header("Directional Ram")]
    [Tooltip("Maximum horizontal travel in open space, independent of frame rate and the feel curve.")]
    [Min(0f)] public float ramDistance = 3.5f;
    [Min(0.05f)] public float ramDuration = 0.4f;
    [Min(0f)] public float ramCooldown = 6f;
    public float groundedStickForce = -8f;
    [Tooltip("Extra reuse lock after movement. Normal walking resumes immediately.")]
    [Min(0f)] public float recoveryTime = 0.12f;
    [Min(0f)] public float minMoveInputForRam = 0.15f;

    [Header("Ram Feel Curve")]
    [Tooltip("Relative speed through the ram. Normalized so changing its shape preserves Ram Distance.")]
    public AnimationCurve ramSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.65f, 0.55f), new Keyframe(1f, 0f));

    [Header("Moving Push Bubble")]
    [Tooltip("360 degree bubble radius. Automatically kept at least 0.25 m wider than the player's capsule.")]
    [Min(0.1f)] public float hitRadius = 1.2f;
    [Tooltip("Small bias toward the movement direction; the bubble still surrounds the player.")]
    [Min(0f)] public float hitForwardOffset = 0.15f;
    [Tooltip("Layers containing the enemies' colliders, including their hitboxes.")]
    public LayerMask enemyMask;
    [Tooltip("Maximum path length each enemy can be displaced by one ram. Multiple colliders do not multiply it.")]
    [Min(0f)] public float enemyPushDistance = 2f;
    [Min(0.05f)] public float enemyPushDuration = 0.3f;
    [Tooltip("Additional interruption after ram movement and enemy displacement finish. Existing AI attack cooldowns also apply.")]
    [Min(0f)] public float enemyStunTime = 0.35f;
    [Tooltip("Ahead of the player, pushes favour the sides of the path. Behind the player, pushes are radial.")]
    [Range(0f, 1f)] public float sidewaysPushBias = 0.75f;

    [Header("Optional Impact Damage")]
    [Tooltip("Applied once per enemy per ram. Use 0 for a space-making skill.")]
    [Min(0f)] public float ramDamage = 0f;
    [Tooltip("Strength sent to existing hit reactions and, if damage kills, the death ragdoll. Live displacement uses Enemy Push Distance.")]
    [Min(0f)] public float ramKnockbackForce = 16f;

    [Header("World Collision")]
    [Tooltip("Solid level geometry. If empty, checks all layers except Ignore Raycast; enemies and the player are filtered out.")]
    public LayerMask wallMask;

    [Header("Feedback")]
    public Camera playerCamera;
    public float fovKick = 4f;
    public float fovKickInTime = 0.04f;
    public float fovReturnTime = 0.12f;
    public float cameraShakeStrength = 0.07f;
    public float cameraShakeDuration = 0.07f;

    private CharacterController controller;
    private PlayerInput playerInput;
    private Vector2 moveInput;
    private Vector3 ramDirection;
    private float lastRamTime = -999f;
    private float ramCooldownTimer;
    private bool ramRequested;
    private bool isRamming;
    private bool isRecovering;
    private int activation;
    private int movementFrame = -1;
    private bool warnedMask;
    private bool impactFeedbackPlayed;
    private float defaultFov;
    private bool ownsFov;
    private bool ownsShake;
    private Vector3 shakeOrigin;
    private Coroutine fovRoutine;
    private Coroutine shakeRoutine;
    private Collider[] overlapBuffer = new Collider[64];
    private readonly HashSet<EnemyHealth> enemiesHitThisRam = new HashSet<EnemyHealth>();
    private readonly HashSet<EnemyHealth> enemiesInStep = new HashSet<EnemyHealth>();
    private readonly float[] cumulativeSpeed = new float[65];
    private const float MaxStepDistance = 0.15f;

    public bool IsRamming => isRamming;
    public bool IsRecovering => isRecovering;
    public bool OwnsMovementThisFrame => isRamming || movementFrame == Time.frameCount;
    private LayerMask WorldMask => wallMask.value == 0 ? Physics.DefaultRaycastLayers : wallMask;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (ramCooldownTimer > 0f)
        {
            ramCooldownTimer = Mathf.Max(0f, ramCooldownTimer - Time.deltaTime);
            UIManager.Instance?.UpdateRamCooldown(ramCooldown > 0f
                ? Mathf.Clamp01(ramCooldownTimer / ramCooldown) : 0f);
        }
        if (!ramRequested) return;
        ramRequested = false;
        if (CanRam()) StartCoroutine(RamRoutine());
    }

    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public void OnRam(InputAction.CallbackContext ctx)
    {
        // Process in Update, after every enemy has run Start. No movement in an
        // Input System callback and no first-frame Shambler initialization race.
        if (ctx.started && isActiveAndEnabled) ramRequested = true;
    }

    private bool CanRam()
    {
        if (isRamming || isRecovering || Time.time < lastRamTime + Mathf.Max(0f, ramCooldown)) return false;
        if (controller == null || !controller.enabled || !controller.isGrounded) return false;
        return TryGetRamDirection(out ramDirection);
    }

    private bool TryGetRamDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        Vector2 input = moveInput;
        if (playerInput != null && playerInput.actions != null)
        {
            InputAction action = playerInput.actions.FindAction("Move", false);
            if (action != null) input = action.ReadValue<Vector2>();
        }
        if (input.magnitude < Mathf.Max(0f, minMoveInputForRam)) return false;
        input = Vector2.ClampMagnitude(input, 1f);
        direction = transform.right * input.x + transform.forward * input.y;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();
        return true;
    }

    private IEnumerator RamRoutine()
    {
        isRamming = true;
        isRecovering = false;
        lastRamTime = Time.time;
        ramCooldownTimer = Mathf.Max(0f, ramCooldown);
        unchecked { activation++; }
        enemiesHitThisRam.Clear();
        impactFeedbackPlayed = false;
        BuildDistanceCurve();
        StartFovKick();
        UIManager.Instance?.UpdateRamCooldown(ramCooldownTimer > 0f ? 1f : 0f);
        if (enemyMask.value == 0 && !warnedMask)
        {
            Debug.LogWarning("ShoulderRam: Enemy Mask is empty. Assign the enemies' collider layers to enable the shove.", this);
            warnedMask = true;
        }

        float elapsed = 0f;
        float requested = 0f;
        float duration = Mathf.Max(0.05f, ramDuration);
        bool blocked = false;
        while (elapsed < duration && controller.enabled && !blocked)
        {
            float seconds = Mathf.Min(Time.deltaTime, duration - elapsed);
            if (seconds <= 0f) { yield return null; continue; }
            movementFrame = Time.frameCount;
            elapsed += seconds;
            float nextRequested = Mathf.Max(0f, ramDistance) * DistanceProgress(elapsed / duration);
            float frameDistance = Mathf.Max(0f, nextRequested - requested);
            requested = nextRequested;
            int steps = Mathf.Max(1, Mathf.CeilToInt(frameDistance / MaxStepDistance));
            float stepDistance = frameDistance / steps;
            float stepSeconds = seconds / steps;

            for (int step = 0; step < steps; step++)
            {
                GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out float radius);
                float allowed = RamCollision.LimitWorldMove(bottom, top, Mathf.Max(0.05f, radius - RamCollision.Skin),
                    ramDirection, stepDistance, WorldMask, transform, transform);

                // Test the current bubble and the next small step. Displace bodies
                // before moving the player; do not turn off their colliders.
                enemiesInStep.Clear();
                PushBubble(Vector3.zero, stepSeconds, duration - elapsed);
                PushBubble(ramDirection * allowed, stepSeconds, duration - elapsed);
                Physics.SyncTransforms();

                Vector3 motion = ramDirection * allowed;
                motion.y = groundedStickForce * stepSeconds;
                controller.Move(motion);
                Physics.SyncTransforms();
                blocked = allowed < stepDistance - 0.0001f || !controller.isGrounded;
                if (blocked) break;
            }
            if (elapsed < duration && !blocked) yield return null;
        }

        isRamming = false;
        ReturnFov();
        if (recoveryTime > 0f)
        {
            isRecovering = true;
            yield return new WaitForSeconds(recoveryTime);
            isRecovering = false;
        }
    }

    private void PushBubble(Vector3 nextStep, float seconds, float remainingRamTime)
    {
        if (enemyMask.value == 0) return;
        GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out float playerRadius);
        float radius = Mathf.Max(playerRadius + 0.25f, hitRadius);
        // Keep the offset smaller than the bubble's extra width, so the current
        // player capsule remains enclosed even when using old prefab values.
        Vector3 offset = ramDirection * Mathf.Clamp(hitForwardOffset, 0f,
            Mathf.Max(0f, radius - playerRadius - 0.25f)) + nextStep;
        int count;
        while (true)
        {
            count = Physics.OverlapCapsuleNonAlloc(bottom + offset, top + offset,
                radius, overlapBuffer, enemyMask, QueryTriggerInteraction.Collide);
            if (count < overlapBuffer.Length) break;
            System.Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
        }

        Vector3 from = (bottom + top) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null) continue;
            EnemyHealth health = hit.GetComponentInParent<EnemyHealth>();
            if (health == null || health.IsDead || health.IsTrainingDummy || !enemiesInStep.Add(health)) continue;
            ZombieAIController ai = health.GetComponent<ZombieAIController>();
            NavMeshAgent agent = health.GetComponent<NavMeshAgent>();
            if (ai == null || !ai.isActiveAndEnabled || !ai.CanReceiveHitReaction ||
                agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink || !agent.updatePosition) continue;

            Vector3 to = health.transform.position + Vector3.up * (agent.height * 0.5f);
            if (RamCollision.HasWorldBetween(from, to, WorldMask, transform, health.transform)) continue;

            EnemyRamPush push = health.GetComponent<EnemyRamPush>();
            if (push == null) push = health.gameObject.AddComponent<EnemyRamPush>();
            Vector3 direction = PushDirection(health.transform.position, from, health.GetInstanceID());
            float interrupt = Mathf.Max(0f, remainingRamTime) +
                Mathf.Max(0.05f, enemyPushDuration) + Mathf.Max(0f, enemyStunTime);
            if (!push.ApplyPressure(this, activation, direction, enemyPushDistance,
                enemyPushDuration, interrupt, WorldMask, seconds)) continue;

            if (!enemiesHitThisRam.Add(health)) continue;
            if (ramDamage > 0f) health.TakeDamage(ramDamage, direction * Mathf.Max(0f, ramKnockbackForce));
            if (!health.IsDead)
            {
                EnemyHitReaction reaction = health.GetComponent<EnemyHitReaction>();
                if (reaction != null) reaction.OnHit(direction, Mathf.Max(0f, ramKnockbackForce));
            }
            if (!impactFeedbackPlayed)
            {
                impactFeedbackPlayed = true;
                PlayImpactFeedback();
            }
        }
    }

    private Vector3 PushDirection(Vector3 enemyPosition, Vector3 bubbleCenter, int identity)
    {
        Vector3 radial = enemyPosition - bubbleCenter;
        radial.y = 0f;
        Vector3 right = Vector3.Cross(Vector3.up, ramDirection).normalized;
        float side = Vector3.Dot(radial, right);
        float sign = Mathf.Abs(side) > 0.05f ? Mathf.Sign(side) : ((identity & 1) == 0 ? 1f : -1f);
        Vector3 lateral = right * sign;
        if (radial.sqrMagnitude < 0.0001f) return lateral;
        // A body directly ahead goes to a side of the corridor, rather than
        // staying directly in front of the player for the entire ram.
        if (Vector3.Dot(radial, ramDirection) >= 0f)
            return Vector3.Lerp(radial.normalized, lateral, Mathf.Clamp01(sidewaysPushBias)).normalized;
        return radial.normalized;
    }

    private void GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        radius = Mathf.Max(0.05f, controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float halfSegment = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * 0.5f - radius);
        Vector3 center = transform.TransformPoint(controller.center);
        bottom = center - Vector3.up * halfSegment;
        top = center + Vector3.up * halfSegment;
    }

    private void BuildDistanceCurve()
    {
        cumulativeSpeed[0] = 0f;
        int segments = cumulativeSpeed.Length - 1;
        for (int i = 1; i <= segments; i++)
        {
            float value = ramSpeedCurve != null && ramSpeedCurve.length > 0
                ? Mathf.Max(0f, ramSpeedCurve.Evaluate((i - 0.5f) / segments)) : 1f;
            cumulativeSpeed[i] = cumulativeSpeed[i - 1] + value;
        }
        float total = cumulativeSpeed[segments];
        for (int i = 1; i <= segments; i++)
            cumulativeSpeed[i] = total > 0.0001f ? cumulativeSpeed[i] / total : (float)i / segments;
    }

    private float DistanceProgress(float normalizedTime)
    {
        float sample = Mathf.Clamp01(normalizedTime) * (cumulativeSpeed.Length - 1);
        int index = Mathf.Min(cumulativeSpeed.Length - 2, Mathf.FloorToInt(sample));
        return Mathf.Lerp(cumulativeSpeed[index], cumulativeSpeed[index + 1], sample - index);
    }

    [ContextMenu("Apply Path-Clearing Prototype Settings")]
    private void ApplyPrototypeSettings()
    {
        // Unity keeps old serialized field values after replacing a script.
        // This explicit preset updates tuning only; it keeps all references/masks.
        ramDistance = 3.5f;
        ramDuration = 0.4f;
        ramCooldown = 6f;
        hitRadius = 1.2f;
        hitForwardOffset = 0.15f;
        enemyPushDistance = 2f;
        enemyPushDuration = 0.3f;
        enemyStunTime = 0.35f;
        sidewaysPushBias = 0.75f;
        ramDamage = 0f;
        ramKnockbackForce = 16f;
        ramSpeedCurve = new AnimationCurve(new Keyframe(0f, 1f),
            new Keyframe(0.65f, 0.55f), new Keyframe(1f, 0f));
    }

    private void StartFovKick()
    {
        if (playerCamera == null) return;
        if (!ownsFov) defaultFov = playerCamera.fieldOfView;
        ownsFov = true;
        if (fovRoutine != null) StopCoroutine(fovRoutine);
        fovRoutine = StartCoroutine(FovRoutine(defaultFov + fovKick, fovKickInTime));
    }

    private void ReturnFov()
    {
        if (playerCamera == null || !ownsFov) return;
        if (fovRoutine != null) StopCoroutine(fovRoutine);
        fovRoutine = StartCoroutine(FovRoutine(defaultFov, fovReturnTime, true));
    }

    private IEnumerator FovRoutine(float target, float duration, bool returning = false)
    {
        float start = playerCamera.fieldOfView;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerCamera.fieldOfView = Mathf.Lerp(start, target, elapsed / Mathf.Max(0.001f, duration));
            yield return null;
        }
        playerCamera.fieldOfView = target;
        if (returning) ownsFov = false;
    }

    private void PlayImpactFeedback()
    {
        if (playerCamera == null) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        RestoreShake();
        shakeOrigin = playerCamera.transform.localPosition;
        ownsShake = true;
        shakeRoutine = StartCoroutine(CameraShake());
    }

    private IEnumerator CameraShake()
    {
        float elapsed = 0f;
        while (elapsed < cameraShakeDuration && playerCamera != null)
        {
            elapsed += Time.deltaTime;
            playerCamera.transform.localPosition = shakeOrigin + Random.insideUnitSphere * cameraShakeStrength;
            yield return null;
        }
        RestoreShake();
    }

    private void RestoreShake()
    {
        if (ownsShake && playerCamera != null) playerCamera.transform.localPosition = shakeOrigin;
        ownsShake = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isRamming = isRecovering = ramRequested = false;
        movementFrame = -1;
        if (ownsFov && playerCamera != null) playerCamera.fieldOfView = defaultFov;
        ownsFov = false;
        RestoreShake();
        fovRoutine = shakeRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController capsule = controller != null ? controller : GetComponent<CharacterController>();
        if (capsule == null) return;
        controller = capsule;
        GetPlayerCapsule(out Vector3 bottom, out Vector3 top, out float playerRadius);
        Vector3 direction = isRamming ? ramDirection : transform.forward;
        float radius = Mathf.Max(playerRadius + 0.25f, hitRadius);
        Vector3 offset = direction * Mathf.Clamp(hitForwardOffset, 0f, radius - playerRadius - 0.25f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(bottom + offset, radius);
        Gizmos.DrawWireSphere(top + offset, radius);
        Gizmos.DrawLine(bottom + offset + transform.right * radius, top + offset + transform.right * radius);
        Gizmos.DrawLine(bottom + offset - transform.right * radius, top + offset - transform.right * radius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + direction * Mathf.Max(0f, ramDistance));
    }
}
