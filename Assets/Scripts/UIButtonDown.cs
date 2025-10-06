
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonDown : MonoBehaviour, IPointerDownHandler
{
    public System.Action onDown;
    public void OnPointerDown(PointerEventData e) => onDown?.Invoke();
}
