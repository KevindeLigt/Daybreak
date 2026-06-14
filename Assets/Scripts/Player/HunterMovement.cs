using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class HunterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4.0f;

    [Tooltip("How quickly the player reaches target speed while grounded.")]
    public float groundAcceleration = 28f;

    [Tooltip("How quickly the player stops while grounded.")]
    public float groundDeceleration = 34f;

    [Tooltip("How much control the player has while airborne.")]
    public float airAcceleration = 8f;

    [Header("Jump / Gravity")]
    public float gravity = -26f;
    public float jumpHeight = 0.9f;

    [Tooltip("Extra gravity when falling. Higher values make the jump feel less floaty.")]
    public float fallMultiplier = 1.8f;

    [Tooltip("Small downward force to keep the CharacterController grounded.")]
    public float groundedStickForce = -5f;

    public float maxFallSpeed = -35f;

    [Tooltip("Allows jumping shortly after walking off an edge.")]
    public float coyoteTime = 0.08f;

    [Tooltip("Allows jump input shortly before landing.")]
    public float jumpBufferTime = 0.10f;

    [Header("Look Settings")]
    public Transform cameraRoot;
    public float lookClamp = 80f;

    [Header("Sensitivity Settings")]
    public float mouseSensitivity = 1.0f;
    public float controllerSensitivity = 120f;

    private CharacterController controller;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    private float cameraPitch = 0f;

    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;

    private bool usingMouse = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void Look(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        lookInput = context.ReadValue<Vector2>();
        usingMouse = context.control.device is Mouse;
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            lastJumpPressedTime = Time.time;
        }
    }

    void Update()
    {
        HandleMovement();
        HandleLook();
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;

            if (verticalVelocity < 0f)
                verticalVelocity = groundedStickForce;
        }

        Vector3 inputDirection =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        Vector3 targetHorizontalVelocity = inputDirection * walkSpeed;

        bool hasInput = moveInput.sqrMagnitude > 0.01f;

        float acceleration;

        if (isGrounded)
            acceleration = hasInput ? groundAcceleration : groundDeceleration;
        else
            acceleration = airAcceleration;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontalVelocity,
            acceleration * Time.deltaTime
        );

        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canUseCoyoteJump = Time.time - lastGroundedTime <= coyoteTime;

        if (jumpBuffered && canUseCoyoteJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
        }

        if (verticalVelocity < 0f)
            verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
        else
            verticalVelocity += gravity * Time.deltaTime;

        verticalVelocity = Mathf.Max(verticalVelocity, maxFallSpeed);

        Vector3 finalVelocity = horizontalVelocity;
        finalVelocity.y = verticalVelocity;

        controller.Move(finalVelocity * Time.deltaTime);
    }

    void HandleLook()
    {
        Vector2 delta = lookInput;
        lookInput = Vector2.zero;

        float sensitivity = usingMouse ? mouseSensitivity : controllerSensitivity;

        float mouseX;
        float mouseY;

        if (usingMouse)
        {
            // Mouse delta is already frame-based, so don't multiply by Time.deltaTime.
            mouseX = delta.x * sensitivity;
            mouseY = delta.y * sensitivity;
        }
        else
        {
            // Controller stick input is continuous, so it should use Time.deltaTime.
            mouseX = delta.x * sensitivity * Time.deltaTime;
            mouseY = delta.y * sensitivity * Time.deltaTime;
        }

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -lookClamp, lookClamp);

        cameraRoot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}