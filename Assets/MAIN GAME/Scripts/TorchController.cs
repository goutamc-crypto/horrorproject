using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Handheld torch on the camera: on/off (F key or mobile button), plus sway so the hand lags the view a little.
public class TorchController : MonoBehaviour
{
    [SerializeField] private Light beam;
    [SerializeField] private GameObject glow;
    [SerializeField] private MobileButton toggleButton;
    [SerializeField] private PlayerController player;
    [SerializeField] private bool startOn = true;

    [Header("Sway")]
    [SerializeField] private float swayDegrees = 3f;
    [SerializeField] private float swaySpeed = 9f;
    [SerializeField] private float bobAmount = 0.006f;

    public bool IsOn { get; private set; }

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Quaternion lastCameraRotation;
    private Vector2 sway;
    private float bobPhase;
    private float bobWeight;

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        lastCameraRotation = transform.parent.rotation;
        SetOn(startOn);
    }

    public void SetOn(bool on)
    {
        IsOn = on;
        beam.enabled = on;
        glow.SetActive(on);
    }

    private void Update()
    {
        bool pressed = toggleButton != null && toggleButton.ConsumePressedThisFrame();
#if ENABLE_INPUT_SYSTEM
        pressed |= Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#endif
        if (pressed)
        {
            SetOn(!IsOn);
        }
    }

    private void LateUpdate()
    {
        // Sway from how far the view turned this frame; clamped so a fast flick can't swing the torch off aim.
        Vector3 delta = (Quaternion.Inverse(lastCameraRotation) * transform.parent.rotation).eulerAngles;
        lastCameraRotation = transform.parent.rotation;
        Vector2 target = new Vector2(Mathf.DeltaAngle(0f, delta.x), Mathf.DeltaAngle(0f, delta.y));
        float blend = 1f - Mathf.Exp(-swaySpeed * Time.deltaTime);
        sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(-target * 1.5f, swayDegrees), blend);
        transform.localRotation = baseRotation * Quaternion.Euler(sway.x, sway.y, 0f);

        // Camera head-bob already carries the torch; this small figure-eight stops it looking welded to the lens.
        bobWeight = Mathf.MoveTowards(bobWeight, player != null && player.IsMoving ? 1f : 0f, 4f * Time.deltaTime);
        bobPhase += Time.deltaTime * (player != null ? 6f + 4f * player.Speed01 : 6f);
        transform.localPosition = basePosition + bobWeight * bobAmount *
            new Vector3(Mathf.Cos(bobPhase), Mathf.Sin(bobPhase * 2f), 0f);
    }
}
