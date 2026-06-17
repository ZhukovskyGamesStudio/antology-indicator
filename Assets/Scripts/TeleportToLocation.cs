using UnityEngine;

public class TeleportToLocation : MonoBehaviour {
    public GameObject Obj;
    public Transform TargetLocation;

    public void Teleport() {
        if (Obj.transform.position == TargetLocation.position) {
            return;
        }

        Obj.transform.position = TargetLocation.position;
    }
}