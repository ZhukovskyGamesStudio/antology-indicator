using UnityEngine;

/// <summary>
/// Держит <see cref="target"/> активным только пока игрок физически внутри этой
/// триггер-зоны. Используется, чтобы вложенная копия комнаты (MirrorRoom2) не
/// светилась «сквозь окно», когда игрок снаружи основной комнаты.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ActiveWhilePlayerInside : MonoBehaviour {
    [Tooltip("Объект, активный только пока игрок внутри этой зоны")]
    public GameObject target;

    private void Reset() {
        Collider c = GetComponent<Collider>();
        if (c != null) {
            c.isTrigger = true;
        }
    }

    private void OnEnable() {
        if (target != null) {
            target.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (target != null && other.CompareTag("Player")) {
            target.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (target != null && other.CompareTag("Player")) {
            target.SetActive(false);
        }
    }
}
