using System;
using UnityEngine;
using UnityEngine.UI;

public class CursorRaycast : MonoBehaviour {
    public Image cursor;

    public Sprite canInteract, defaultSprite;

    private Camera _camera;


    private void LateUpdate() {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit)) {
            InteractiveObj inter = hit.transform.GetComponent<InteractiveObj>();
            if (inter != null) {
                cursor.sprite = canInteract;
                return;
            }
        }

        cursor.sprite = defaultSprite;
    }
}