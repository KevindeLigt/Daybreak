using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;    // for your input system

public class ShoulderRam : MonoBehaviour
{
    [Header("Ram Settings")]
    public float ramSpeed = 14f;         // burst movement speed
    public float ramDuration = 0.35f;    // how long player surges
    public float ramCooldown = 5f;       // adjustable cooldown
    public float ramDamage = 30f;        // damage to each enemy
    public float ramKnockbackForce = 15f;  // push force

    private float ramCooldownTimer = 0f;

    [Header("Hit Detection")]
    public float hitRadius = 1f;         // radius of "shoulder"
    public LayerMask enemyMask;          // assign "Enemy" layer

    [Header("Feedback")]
    public Camera playerCamera;
    public float fovKick = 10f;          // temporary FOV increase
    public float cameraShakeStrength = 0.2f;

    private float lastRamTime = -999f;
    private bool isRamming = false;

    private Vector3 ramDirection;

    private CharacterController controller;  // if using CC
    private PlayerInput input;               // input link

    private void Update()
    {
        if (ramCooldownTimer > 0f)
        {
            ramCooldownTimer -= Time.deltaTime;

            float normalized = Mathf.Clamp01(ramCooldownTimer / ramCooldown);
            UIManager.Instance?.UpdateRamCooldown(normalized);
        }
    }


    void Start()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInput>();

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    // --------- INPUT CALLBACK ---------
    public void OnRam(InputAction.CallbackContext ctx)
    {
        if (!ctx.started) return;

        if (!CanRam()) return;

        StartCoroutine(RamRoutine());
    }

    private bool CanRam()
    {
        if (isRamming) return false;
        if (Time.time < lastRamTime + ramCooldown) return false;

        return true;
    }

    // --------- CORE RAM ROUTINE ---------
    private IEnumerator RamRoutine()
    {
        isRamming = true;
        lastRamTime = Time.time;
        ramCooldownTimer = ramCooldown;  // start the timer
        UIManager.Instance?.UpdateRamCooldown(1f); // show full cooldown

        ramDirection = transform.forward;

        // Camera feedback
        float defaultFOV = playerCamera.fieldOfView;
        playerCamera.fieldOfView = defaultFOV + fovKick;

        float timer = 0f;

        while (timer < ramDuration)
        {
            timer += Time.deltaTime;

            // Move player forward aggressively
            Vector3 move = ramDirection * ramSpeed * Time.deltaTime;

            // If CharacterController:
            if (controller)
            {
                controller.Move(move);
            }
            else
            {
                transform.position += move;
            }

            DetectHits();

            // If we hit a wall → stop early
            if (HitWallAhead())
                break;

            yield return null;
        }

        // Reset camera
        playerCamera.fieldOfView = defaultFOV;

        isRamming = false;
    }

    // --------- HIT DETECTION ---------
    private void DetectHits()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position + ramDirection * 0.8f,
            hitRadius,
            enemyMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider c in hits)
        {
            if (c.TryGetComponent(out EnemyHealth health))
            {
                // FIXED: now sends force properly
                Vector3 force = ramDirection * ramKnockbackForce;
                health.TakeDamage(ramDamage, force);

                // extra physical shove (optional)
                if (c.TryGetComponent(out Rigidbody rb))
                {
                    rb.AddForce(force, ForceMode.Impulse);
                }

                // stun interrupt
                if (c.TryGetComponent(out ZombieAIHybrid ai))
                {
                    ai.HitStun(0.25f);
                }

                // camera shake
                StartCoroutine(CameraShake(0.1f));
            }
        }
    }


    private bool HitWallAhead()
    {
        return Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            ramDirection,
            1f,
            LayerMask.GetMask("Default", "Environment")
        );
    }

    // --------- CAMERA SHAKE ---------
    private IEnumerator CameraShake(float duration)
    {
        float t = 0f;
        Vector3 originalPos = playerCamera.transform.localPosition;

        while (t < duration)
        {
            t += Time.deltaTime;
            playerCamera.transform.localPosition =
                originalPos + Random.insideUnitSphere * cameraShakeStrength;

            yield return null;
        }

        playerCamera.transform.localPosition = originalPos;
    }


    // Optional debug visual
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 0.8f, hitRadius);
    }
}
