using UnityEngine;

public class FaceCamera : MonoBehaviour {
    public bool IsLockX;

    private void Update() {
        transform.forward =  transform.position - Camera.main.transform.position;
        Vector3 rot = transform.rotation.eulerAngles;
        rot.z = 0;
        if (IsLockX) {
            rot.x = 0;
        }

        transform.rotation = Quaternion.Euler(rot);
    }
}