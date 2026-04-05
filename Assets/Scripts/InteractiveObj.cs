using System;
using UnityEngine;
using UnityEngine.Events;

public class InteractiveObj : MonoBehLogger {
    public UnityEvent OnClick;

    private void OnMouseDown() {
        OnClick?.Invoke();
    }
}