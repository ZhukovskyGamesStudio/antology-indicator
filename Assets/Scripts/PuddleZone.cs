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

    // Кто сейчас стоит в луже. Нужен, чтобы отпустить его, если лужа исчезнет
    // прямо у него под ногами (высохла, комнату выключили).
    private Footsteps _inside;

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
            _inside = f;
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
            if (_inside == f) {
                _inside = null;
            }

            f.ExitPuddle();
        }
    }

    /// <summary>
    /// Лужа высохла (или комнату выключили) прямо под ногами. На выключение
    /// объекта OnTriggerExit приходит не всегда, а счётчик мокрых шагов в
    /// <see cref="Footsteps"/> накопительный — игрок остался бы «мокрым» навсегда.
    /// Если OnTriggerExit всё же пришёл, <see cref="_inside"/> уже пуст и второй
    /// раз никто не вычитается.
    /// </summary>
    private void OnDisable() {
        if (_inside != null) {
            _inside.ExitPuddle();
            _inside = null;
        }
    }
}
