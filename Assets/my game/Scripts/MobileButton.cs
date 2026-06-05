using UnityEngine;
using UnityEngine.EventSystems;

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private bool releaseOnPointerExit = true;

    public bool IsPressed { get; private set; }
    public RectTransform RectTransform => transform as RectTransform;

    private bool pressedThisFrame;

    public void OnPointerDown(PointerEventData eventData)
    {
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (releaseOnPointerExit)
        {
            SetPressed(false);
        }
    }

    public void SetPressed(bool isPressed)
    {
        if (isPressed && !IsPressed)
        {
            pressedThisFrame = true;
        }

        IsPressed = isPressed;
    }

    public bool ConsumePressedThisFrame()
    {
        bool consumed = pressedThisFrame;
        pressedThisFrame = false;
        return consumed;
    }
}
