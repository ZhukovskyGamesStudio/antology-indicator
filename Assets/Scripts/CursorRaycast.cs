using System;
using UnityEngine;
using UnityEngine.UI;

public class CursorRaycast : MonoBehaviour {
    public Image cursor;

    public Sprite canInteract, defaultSprite, canHit;
    public float Range = 1.6f;
    public static float RangeStatic = 1.6f;
    private Camera _camera;

    private void Awake() {
        RangeStatic = Range;
    }

    private void LateUpdate() {
        if (Raycast(out RaycastHit hit)) {
            if (CanHit(hit, out HittableObj toHit)) {
                cursor.sprite = canHit;
                return;
            }

            InteractiveObj inter = hit.transform.GetComponent<InteractiveObj>();
            if (inter != null && inter.enabled) {
                cursor.sprite = canInteract;
                return;
            }
        }

        cursor.sprite = defaultSprite;
    }

    public static bool CanHit(RaycastHit hit, out HittableObj toHit) {
        if (HUD.instance.HasHammer) {
            toHit = hit.transform.GetComponent<HittableObj>();
            if (toHit != null && toHit.enabled) {
                return true;
            }
        }
        toHit = null;
        return false;
    }

    public static bool Raycast(out RaycastHit hit) {
        return Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, RangeStatic);
    }
}