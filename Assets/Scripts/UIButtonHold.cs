using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public System.Action onDown;
    public System.Action onUp;
    public bool releaseOnExit = false;

    public void OnPointerDown(PointerEventData e) => onDown?.Invoke();
    public void OnPointerUp  (PointerEventData e) => onUp?.Invoke();
    public void OnPointerExit(PointerEventData e) { if (releaseOnExit) onUp?.Invoke(); }
}
