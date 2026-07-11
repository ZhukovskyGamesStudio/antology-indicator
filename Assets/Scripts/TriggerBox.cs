using UnityEngine;
using UnityEngine.Events;

public class TriggerBox : MonoBehaviour {
    public UnityEvent OnTriggerEnterEvent;

    [Header("Направление входа")]
    [Tooltip("Сторона, с которой игрок должен входить (относительно бокса). " +
             "Напр. (0,0,1) — вход спереди, (1,0,0) — справа.")]
    public Vector3 AllowedEntrySide = Vector3.forward;

    [Tooltip("Учитывать поворот бокса (локальное направление). Если выкл — направление в мировых координатах.")]
    public bool UseLocalSpace = true;

    [Tooltip("Строгость совпадения стороны: 0 — любая в нужной полусфере, ближе к 1 — точнее по оси.")]
    [Range(0f, 1f)]
    public float SideThreshold = 0.25f;

    [Tooltip("Игнорировать срабатывания первые доли секунды после включения — чтобы не " +
             "ловить ложный 'вход', когда триггер активировался, уже перекрывая игрока " +
             "(напр. звук шкафа при появлении лабиринта).")]
    public float ignoreAfterEnable = 0.3f;

    private float _enabledTime;

    private void OnEnable() {
        _enabledTime = Time.time;
    }

    private void OnTriggerEnter(Collider other) {
        // Активационный overlap (триггер включился, уже перекрывая игрока) — не вход.
        if (Time.time - _enabledTime < ignoreAfterEnable) {
            return;
        }

        if (!other.gameObject.CompareTag("Player")) {
            return;
        }

        Vector3 allowed = (UseLocalSpace
            ? transform.TransformDirection(AllowedEntrySide)
            : AllowedEntrySide).normalized;

        // Откуда вошёл игрок: вектор от центра бокса к игроку в момент входа.
        Vector3 entrySide = (other.transform.position - transform.position).normalized;

        if (Vector3.Dot(entrySide, allowed) >= SideThreshold) {
            OnTriggerEnterEvent.Invoke();
        }
    }

    private void OnDrawGizmosSelected() {
        Vector3 allowed = (UseLocalSpace
            ? transform.TransformDirection(AllowedEntrySide)
            : AllowedEntrySide).normalized;

        Gizmos.color = Color.green;
        Vector3 from = transform.position + allowed * 1.5f;
        Gizmos.DrawLine(transform.position, from);
        Gizmos.DrawSphere(from, 0.15f);
    }
}
