using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class HunterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Look Settings")]
    public Transform cameraRoot;
    public float lookClamp = 80f;

    [Header("Sensitivity Settings")]
    public float mouseSensitivity = 1.0f;
    public float controllerSensitivity = 120f; // gamepad usually needs MUCH higher values
    private float currentSensitivity = 1f;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private float cameraPitch = 0f;

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

        // Detect device
        if (context.control.device is Mouse)
        {
            currentSensitivity = mouseSensitivity;
        }
        else
        {
            currentSensitivity = controllerSensitivity;
        }
    }


    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    void Update()
    {
        HandleMovement();
        HandleLook();
    }

    void HandleMovement()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * walkSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleLook()
    {
        Vector2 delta = lookInput;
        lookInput = Vector2.zero; // <-- STOP DRIFT

        float mouseX = delta.x * currentSensitivity * Time.deltaTime;
        float mouseY = delta.y * currentSensitivity * Time.deltaTime;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -lookClamp, lookClamp);

        cameraRoot.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
        transform.Rotate(Vector3.up * mouseX);
    }

}
