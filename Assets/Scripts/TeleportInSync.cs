using UnityEngine;

public class TeleportInSync : MonoBehaviour {
    [Tooltip("Комната, в которой стоит этот триггер — система отсчёта, откуда телепортируем")]
    public GameObject Room;

    [Tooltip("Комната, в которую телепортируем игрока (точно такая же планировка)")]
    public GameObject SecondRoom;

    private void OnTriggerEnter(Collider other) {
        if (!other.transform.CompareTag("Player")) {
            return;
        }

        TeleportPlayerInSyncWithRooms();
    }

    public void TeleportPlayerInSyncWithRooms() {
        Transform p = GameObject.FindGameObjectWithTag("Player").transform;

        // Позиция и поворот игрока относительно своей комнаты.
        Vector3 localPos = Room.transform.InverseTransformPoint(p.position);
        Quaternion localRot = Quaternion.Inverse(Room.transform.rotation) * p.rotation;

        // Та же точка и поворот, но уже в системе отсчёта второй комнаты.
        Vector3 targetPos = SecondRoom.transform.TransformPoint(localPos);
        Quaternion targetRot = SecondRoom.transform.rotation * localRot;

        p.position = targetPos;
        // Тело игрока вращается только по Y (yaw); наклон камеры (pitch) сохраняется сам.
        // FirstPersonController каждый кадр перечитывает yaw из transform, поэтому запись держится.
        p.rotation = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);

        // Сбрасываем инерцию Rigidbody, чтобы игрока не «протащило» после телепорта.
        if (p.TryGetComponent(out Rigidbody rb)) {
            rb.position = targetPos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}