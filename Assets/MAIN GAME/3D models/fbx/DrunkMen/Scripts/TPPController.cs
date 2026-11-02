using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TPPController : MonoBehaviour
{
    public Transform cameraTransform;

    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -20f;


    [HideInInspector] public CharacterController controller;
    [HideInInspector] public Animator animator;
    private Vector3 velocity;

    private InputAction moveAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        moveAction = new InputAction("Move", InputActionType.Value);

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
    }

    private void Update()
    {
        if(controller.enabled == false) return;
        
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0;
        camRight.y = 0;

        camForward.Normalize();
        camRight.Normalize();

        if (controller.enabled == false) return;

        Vector3 moveDirection = camForward * input.y + camRight * input.x;
        animator.SetFloat("Speed", moveDirection.magnitude);



        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);

            controller.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);


        }

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        // controller.Move(velocity * Time.deltaTime);
    }
}