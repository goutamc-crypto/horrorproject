using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerControl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private MobileJoystick movementJoystick;
    [SerializeField] private MobileButton sprintButton;
    [SerializeField] private MobileButton jumpButton;
    [SerializeField] private RectTransform[] lookBlockers;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 6.6f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float deceleration = 22f;
    [SerializeField] private float airControl = 0.45f;
    [SerializeField] private float jumpHeight = 1.25f;
    [SerializeField] private float gravity = -6f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float terminalFallSpeed = -55f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundedOffset = -0.08f;
    [SerializeField] private float groundedRadius = 0.35f;

    [Header("Look")]
    [SerializeField] private float touchSensitivity = 0.12f;
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float lookSmoothing = 22f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Editor Testing")]
    [SerializeField] private bool keyboardAndMouse = true;

    public bool IsGrounded { get; private set; }
    public Vector2 MoveInput { get; private set; }

    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float yaw;
    private float pitch;
    private Vector2 targetLookDelta;
    private Vector2 smoothLookDelta;
    private int activeLookTouchId = -1;
    private bool mouseLookStartedAwayFromUi;

    private void Reset()
    {
        cameraRoot = Camera.main != null ? Camera.main.transform : null;
        playerCamera = Camera.main;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (cameraRoot == null && playerCamera != null)
        {
            cameraRoot = playerCamera.transform;
        }

        yaw = transform.eulerAngles.y;
        pitch = cameraRoot != null ? NormalizeAngle(cameraRoot.localEulerAngles.x) : 0f;
    }

    private void Update()
    {
        ReadMoveInput();
        CheckGrounded();
        MovePlayer();
    }

    private void LateUpdate()
    {
        ReadLookInput();
        RotateView();
    }

    public void SetMoveInput(Vector2 input)
    {
        MoveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetSprintInput(bool isSprinting)
    {
        if (sprintButton != null)
        {
            sprintButton.SetPressed(isSprinting);
        }
    }

    public void Jump()
    {
        if (IsGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void ReadMoveInput()
    {
        Vector2 input = movementJoystick != null ? movementJoystick.Value : Vector2.zero;

        if (keyboardAndMouse)
        {
            input += ReadKeyboardMove();
        }

        MoveInput = Vector2.ClampMagnitude(input, 1f);
    }

    private void MovePlayer()
    {
        bool wantsSprint = IsSprintHeld();
        float moveAmount = Mathf.Clamp01(MoveInput.magnitude);
        float targetSpeed = (wantsSprint ? sprintSpeed : walkSpeed) * moveAmount;

        Vector3 inputDirection = transform.right * MoveInput.x + transform.forward * MoveInput.y;
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        Vector3 targetVelocity = inputDirection * targetSpeed;
        float response = IsGrounded
            ? (targetVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration)
            : acceleration * airControl;

        planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, response * Time.deltaTime);

        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        if (IsJumpPressed())
        {
            Jump();
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, terminalFallSpeed);

        Vector3 motion = planarVelocity;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private void CheckGrounded()
    {
        Vector3 spherePosition = transform.position + Vector3.up * groundedOffset;
        IsGrounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private bool IsSprintHeld()
    {
        bool held = sprintButton != null && sprintButton.IsPressed;

#if ENABLE_INPUT_SYSTEM
        if (keyboardAndMouse && Keyboard.current != null)
        {
            held |= Keyboard.current.leftShiftKey.isPressed;
        }
#endif

        return held;
    }

    private bool IsJumpPressed()
    {
        bool pressed = jumpButton != null && jumpButton.ConsumePressedThisFrame();

#if ENABLE_INPUT_SYSTEM
        if (keyboardAndMouse && Keyboard.current != null)
        {
            pressed |= Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#endif

        return pressed;
    }

    private Vector2 ReadKeyboardMove()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return input;
        }

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            input.y += 1f;
        }
#endif

        return Vector2.ClampMagnitude(input, 1f);
    }

    private void ReadLookInput()
    {
        targetLookDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        ReadTouchLook();
        ReadMouseLook();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void ReadTouchLook()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            activeLookTouchId = -1;
            return;
        }

        bool activeTouchFound = false;

        foreach (TouchControl touch in touchscreen.touches)
        {
            UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.None)
            {
                continue;
            }

            int touchId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();

            if (activeLookTouchId == -1 && phase == UnityEngine.InputSystem.TouchPhase.Began && !IsScreenPointBlocked(position))
            {
                activeLookTouchId = touchId;
            }

            if (touchId != activeLookTouchId)
            {
                continue;
            }

            activeTouchFound = true;

            if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                targetLookDelta += touch.delta.ReadValue() * touchSensitivity;
            }

            if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                activeLookTouchId = -1;
            }
        }

        if (!activeTouchFound)
        {
            activeLookTouchId = -1;
        }
    }

    private void ReadMouseLook()
    {
        if (!keyboardAndMouse || Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            mouseLookStartedAwayFromUi = !IsScreenPointBlocked(Mouse.current.position.ReadValue());
        }

        if (Mouse.current.leftButton.isPressed && mouseLookStartedAwayFromUi)
        {
            targetLookDelta += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            mouseLookStartedAwayFromUi = false;
        }
    }
#endif

    private void RotateView()
    {
        float smoothing = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);
        smoothLookDelta = Vector2.Lerp(smoothLookDelta, targetLookDelta, smoothing);

        yaw += smoothLookDelta.x;
        pitch = Mathf.Clamp(pitch - smoothLookDelta.y, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraRoot != null)
        {
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private bool IsScreenPointBlocked(Vector2 screenPoint)
    {
        if (lookBlockers != null)
        {
            for (int i = 0; i < lookBlockers.Length; i++)
            {
                if (RectContainsScreenPoint(lookBlockers[i], screenPoint))
                {
                    return true;
                }
            }
        }

        if (movementJoystick != null && RectContainsScreenPoint(movementJoystick.RectTransform, screenPoint))
        {
            return true;
        }

        if (sprintButton != null && RectContainsScreenPoint(sprintButton.RectTransform, screenPoint))
        {
            return true;
        }

        return jumpButton != null && RectContainsScreenPoint(jumpButton.RectTransform, screenPoint);
    }

    private static bool RectContainsScreenPoint(RectTransform rectTransform, Vector2 screenPoint)
    {
        if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera eventCamera = null;

        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                eventCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
            else if (canvas.renderMode == RenderMode.WorldSpace)
            {
                eventCamera = Camera.main;
            }
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, eventCamera);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle > 180f ? angle - 360f : angle;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * groundedOffset, groundedRadius);
    }
}
