using UnityEngine;

public class PortalCenter : MonoBehaviour {
    public GameObject Room;

    public Transform Location1;

    private void OnTriggerEnter(Collider other) {
        if (!other.transform.CompareTag("Player")) {
            return;
        }

        if (Room.transform.position != Location1.position) {
            TeleportPlayerAndRoom();
        }
    }

    public void TeleportPlayerAndRoom() {
        Transform p = GameObject.FindGameObjectWithTag("Player").transform;
        Vector3 playerOffset = p.transform.position - Room.transform.position;
        Room.transform.position = Location1.position;
        p.transform.position = Location1.position + playerOffset;
    }
}