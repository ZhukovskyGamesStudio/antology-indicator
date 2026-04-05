using UnityEngine.Events;

public class InteractiveObj : MonoBehLogger {
    public UnityEvent OnClick;

    private void OnMouseDown() {
        if (enabled) {
            OnClick?.Invoke();
        }
    }
}