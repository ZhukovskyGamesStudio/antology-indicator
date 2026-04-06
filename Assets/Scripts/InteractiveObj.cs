using UnityEngine;
using UnityEngine.Events;

public class InteractiveObj : MonoBehLogger {
    public UnityEvent OnClick;

    private void OnMouseDown() {
        if (enabled && CursorRaycast.Raycast(out RaycastHit hit) && hit.distance <= CursorRaycast.RangeStatic) {
            OnClick?.Invoke();
        }
    }
}