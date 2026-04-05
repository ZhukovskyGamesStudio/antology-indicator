using System;
using UnityEngine;
using UnityEngine.UI;

public class CursorRaycast : MonoBehaviour {
    public Image cursor;

    public Sprite canInteract, defaultSprite, canHit;

    private Camera _camera;
    public HUD Hud;

    private void LateUpdate() {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1.3f)) {
            if (Hud.HasHammer) {
                HittableObj hitt = hit.transform.GetComponent<HittableObj>();
                if (hitt != null && hitt.enabled) {
                    cursor.sprite = canHit;
                    return;
                }
            }

            InteractiveObj inter = hit.transform.GetComponent<InteractiveObj>();
            if (inter != null && inter.enabled) {
                cursor.sprite = canInteract;
                return;
            }
        }

        cursor.sprite = defaultSprite;
    }
}