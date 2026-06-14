using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class ShoulderRam : MonoBehaviour
{
    [Header("Directional Ram Settings")]
    [Tooltip("Short burst speed. This is a grounded shove/dodge, not a long dash.")]
    public float ramSpeed = 10f;

    [Tooltip("Short duration keeps it snappy and grounded.")]
    public float ramDuration = 0.20f;

    [Tooltip("Cooldown should make this a commitment, not a spam dodge.")]
    public float ramCooldown = 6f;

    [Tooltip("Small damage only. Shotgun should remain the main killer.")]
    public float ramDamage = 8f;

    [Tooltip("Main purpose of the ram: create space.")]
    public float ramKnockbackForce = 16f;

    [Tooltip("How long enemies are interrupted/stunned.")]
    public float enemyStunTime = 0.30f;

    [Tooltip("Small downward force to keep the ram grounded.")]
    public float groundedStickForce = -8f;

    [Tooltip("Recovery after the ram finishes. Keeps it from feeling like a perfect escape.")]
    public float recoveryTime = 0.12f;

    [Tooltip("Minimum movement input required to ram. Prevents accidental forward ram.")]
    public float minMoveInputForRam = 0.15f;

    [Header("Ram Feel Curve")]
    [Tooltip("High at the start, low at the end. Creates a BAM -> stop feeling.")]
    public AnimationCurve ramSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.65f, 0.55f),
        new Keyframe(1f, 0f)
    );

    [Header("Hit Detection")]
    [Tooltip("Radius of the impact zone.")]
    public float hitRadius = 0.85f;

    [Tooltip("How far in the ram direction the hit sphere appears.")]
    public float hitForwardOffset = 0.9f;

    [Tooltip("Assign your Enemy layer here.")]
    public LayerMask enemyMask;

    [Header("Wall Detection")]
    [Tooltip("Assign Default / Environment / Level Geometry layers here.")]
    public LayerMask wallMask;

    public float wallCheckDistance = 0.65f;

    [Header("Feedback")]
    public Camera playerCamera;

    [Tooltip("Small FOV punch. Too much makes it feel floaty.")]
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
    private float ramCooldownTimer = 0f;

    private bool isRamming = false;
    private bool isRecovering = false;

    private float defaultFov;

    private readonly HashSet<EnemyHealth> enemiesHitThisRam = new HashSet<EnemyHealth>();

    private Coroutine fovRoutine;
    private Coroutine shakeRoutine;

    public bool IsRamming => isRamming;
    public bool IsRecovering => isRecovering;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera != null)
            defaultFov = playerCamera.fieldOfView;
    }

    private void Update()
    {
        UpdateCooldownUI();
    }

    private void UpdateCooldownUI()
    {
        if (ramCooldownTimer <= 0f)
            return;

        ramCooldownTimer = Mathf.Max(0f, ramCooldownTimer - Time.deltaTime);

        float normalized = Mathf.Clamp01(ramCooldownTimer / ramCooldown);
        UIManager.Instance?.UpdateRamCooldown(normalized);
    }

    // Optional Input System callback.
    // This helps if PlayerInput uses Send Messages / Broadcast Messages.
    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    // Input System callback.
    public void OnRam(InputAction.CallbackContext ctx)
    {
        if (!ctx.started)
            return;

        if (!CanRam())
            return;

        StartCoroutine(RamRoutine());
    }

    private bool CanRam()
    {
        if (isRamming) return false;
        if (isRecovering) return false;

        if (Time.time < lastRamTime + ramCooldown)
            return false;

        if (!controller.isGrounded)
            return false;

        if (!TryGetRamDirection(out ramDirection))
            return false;

        return true;
    }

    private bool TryGetRamDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        Vector2 currentMoveInput = moveInput;

        // More reliable if the PlayerInput has an action called "Move".
        if (playerInput != null && playerInput.actions != null)
        {
            InputAction moveAction = playerInput.actions["Move"];
            if (moveAction != null)
            {
                currentMoveInput = moveAction.ReadValue<Vector2>();
            }
        }

        if (currentMoveInput.magnitude < minMoveInputForRam)
            return false;

        currentMoveInput = Vector2.ClampMagnitude(currentMoveInput, 1f);

        direction =
            transform.right * currentMoveInput.x +
            transform.forward * currentMoveInput.y;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        direction.Normalize();
        return true;
    }

    private IEnumerator RamRoutine()
    {
        isRamming = true;
        isRecovering = false;

        lastRamTime = Time.time;
        ramCooldownTimer = ramCooldown;

        UIManager.Instance?.UpdateRamCooldown(1f);

        enemiesHitThisRam.Clear();

        StartFovKick();

        float timer = 0f;

        while (timer < ramDuration)
        {
            timer += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(timer / ramDuration);
            float curveMultiplier = ramSpeedCurve.Evaluate(normalizedTime);

            Vector3 frameMove =
                ramDirection * ramSpeed * curveMultiplier * Time.deltaTime;

            // Keeps the CharacterController stuck to the floor.
            frameMove.y = groundedStickForce * Time.deltaTime;

            CollisionFlags flags = controller.Move(frameMove);

            DetectHits();

            bool hitWallWithController = (flags & CollisionFlags.Sides) != 0;
            bool hitWallWithRaycast = HitWallAhead();

            if (hitWallWithController || hitWallWithRaycast)
                break;

            yield return null;
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

    private void DetectHits()
    {
        Vector3 hitCenter = transform.position + ramDirection * hitForwardOffset;

        Collider[] hits = Physics.OverlapSphere(
            hitCenter,
            hitRadius,
            enemyMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            EnemyHealth health = hit.GetComponentInParent<EnemyHealth>();
            if (health == null)
                continue;

            if (enemiesHitThisRam.Contains(health))
                continue;

            enemiesHitThisRam.Add(health);

            Vector3 force = ramDirection * ramKnockbackForce;

            health.TakeDamage(ramDamage, force);

            ZombieAIHybrid ai = hit.GetComponentInParent<ZombieAIHybrid>();
            if (ai != null)
            {
                ai.HitStun(enemyStunTime);
            }

            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null)
            {
                rb.AddForce(force, ForceMode.Impulse);
            }

            PlayImpactFeedback();
        }
    }

    private bool HitWallAhead()
    {
        if (wallMask.value == 0)
            return false;

        Vector3 origin = transform.position + Vector3.up * 0.6f;

        return Physics.Raycast(
            origin,
            ramDirection,
            wallCheckDistance,
            wallMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void StartFovKick()
    {
        if (playerCamera == null)
            return;

        if (fovRoutine != null)
            StopCoroutine(fovRoutine);

        fovRoutine = StartCoroutine(FovRoutine(defaultFov + fovKick, fovKickInTime));
    }

    private void ReturnFov()
    {
        if (playerCamera == null)
            return;

        if (fovRoutine != null)
            StopCoroutine(fovRoutine);

        fovRoutine = StartCoroutine(FovRoutine(defaultFov, fovReturnTime));
    }

    private IEnumerator FovRoutine(float targetFov, float duration)
    {
        float startFov = playerCamera.fieldOfView;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            playerCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);

            yield return null;
        }

        playerCamera.fieldOfView = targetFov;
    }

    private void PlayImpactFeedback()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(CameraShake(cameraShakeDuration));
    }

    private IEnumerator CameraShake(float duration)
    {
        if (playerCamera == null)
            yield break;

        float timer = 0f;
        Vector3 originalLocalPos = playerCamera.transform.localPosition;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            playerCamera.transform.localPosition =
                originalLocalPos + Random.insideUnitSphere * cameraShakeStrength;

            yield return null;
        }

        playerCamera.transform.localPosition = originalLocalPos;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 debugDirection = ramDirection.sqrMagnitude > 0.001f
            ? ramDirection
            : transform.forward;

        debugDirection.y = 0f;
        debugDirection.Normalize();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + debugDirection * hitForwardOffset, hitRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            transform.position + Vector3.up * 0.6f,
            transform.position + Vector3.up * 0.6f + debugDirection * wallCheckDistance
        );
    }
}