using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    public float mouseSensitivity = 3f;
    public float distance = 5f;
    public float height = 2f;

    private float yaw;
    private float pitch = 20f;

    private InputAction lookAction;

    private void Awake()
    {
        lookAction = new InputAction(
            "Look",
            InputActionType.Value,
            "<Mouse>/delta");
    }

    private void OnEnable()
    {
        lookAction.Enable();
    }

    private void OnDisable()
    {
        lookAction.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        Vector2 mouseDelta =
            lookAction.ReadValue<Vector2>();

        yaw += mouseDelta.x * mouseSensitivity * Time.deltaTime;
        pitch -= mouseDelta.y * mouseSensitivity * Time.deltaTime;

        pitch = Mathf.Clamp(pitch, -30f, 70f);

        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0);

        Vector3 targetPosition =
            target.position +
            Vector3.up * height;

        Vector3 cameraPosition =
            targetPosition -
            rotation * Vector3.forward * distance;

        transform.position = cameraPosition;
        transform.rotation = rotation;
    }
}