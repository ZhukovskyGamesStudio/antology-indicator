using System;
using UnityEngine;
using UnityEngine.Events;

public class InteractiveObj : MonoBehaviour {
    public UnityEvent OnClick;

    private void OnMouseDown() {
        OnClick?.Invoke();
    }
}