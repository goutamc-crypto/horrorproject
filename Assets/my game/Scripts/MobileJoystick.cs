using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRange = 70f;
    [SerializeField] private float deadZone = 0.08f;

    public Vector2 Value { get; private set; }
    public RectTransform RectTransform => background != null ? background : transform as RectTransform;

    private void Reset()
    {
        background = transform as RectTransform;
    }

    private void Awake()
    {
        if (background == null)
        {
            background = transform as RectTransform;
        }

        SetHandle(Vector2.zero);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        Vector2 halfSize = background.rect.size * 0.5f;
        Vector2 normalized = new Vector2(
            halfSize.x > 0f ? localPoint.x / halfSize.x : 0f,
            halfSize.y > 0f ? localPoint.y / halfSize.y : 0f);

        Value = Vector2.ClampMagnitude(normalized, 1f);
        if (Value.magnitude < deadZone)
        {
            Value = Vector2.zero;
        }

        SetHandle(Value);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Value = Vector2.zero;
        SetHandle(Vector2.zero);
    }

    private void SetHandle(Vector2 value)
    {
        if (handle != null)
        {
            handle.anchoredPosition = value * handleRange;
        }
    }
}
