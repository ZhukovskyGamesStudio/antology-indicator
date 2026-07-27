using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Зона лужи. Пока игрок внутри — его шаги становятся мокрыми (см. <see cref="Footsteps"/>).
/// Требует триггер-коллайдер, покрывающий лужу.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PuddleZone : MonoBehaviour {
    [Tooltip("Реплика при первом заходе в любую лужу (один раз за игру). Пусто — без реакции")]
    public string reactionOnce = "Откуда вода? На улице ж нет дождя! Что за дверью?..";

    private static bool _reacted;

    /// <summary>Забыть, что реплика уже звучала — новый запуск игры начинается с нуля.</summary>
    public static void ResetSaid() {
        _reacted = false;
    }

    private void Reset() {
        Collider c = GetComponent<Collider>();
        if (c != null) {
            c.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag("Player")) {
            return;
        }

        Footsteps f = other.GetComponentInParent<Footsteps>();
        if (f != null) {
            f.EnterPuddle();
        }

        if (!_reacted && !string.IsNullOrEmpty(reactionOnce) && TalkUI.instance != null) {
            _reacted = true;
            TalkUI.instance.Say(reactionOnce).Forget();
        }
    }

    private void OnTriggerExit(Collider other) {
        if (!other.CompareTag("Player")) {
            return;
        }

        Footsteps f = other.GetComponentInParent<Footsteps>();
        if (f != null) {
            f.ExitPuddle();
        }
    }
}
