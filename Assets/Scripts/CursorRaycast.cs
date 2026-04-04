using System;
using UnityEngine;
using UnityEngine.UI;

public class CursorRaycast : MonoBehaviour {
    public Image cursor;

    public Sprite canInteract, defaultSprite;

    private Camera _camera;

    private void LateUpdate() {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1.3f)) {
            InteractiveObj inter = hit.transform.GetComponent<InteractiveObj>();
            if (inter != null && inter.enabled) {
                cursor.sprite = canInteract;
                return;
            }
        }

        cursor.sprite = defaultSprite;
    }
}