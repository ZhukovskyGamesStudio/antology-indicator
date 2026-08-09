using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Триггер-зона: при первом заходе игрока произносит реплику один раз.
/// Требует триггер-коллайдер, покрывающий зону (напр. вход в ванную).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReactionZone : MonoBehaviour {
    [Tooltip("Реплика при первом входе игрока. Пусто — без реакции")]
    [TextArea] public string reactionOnce;

    [Tooltip("Один раз на всю игру (true) или один раз на этот конкретный триггер (false)")]
    public bool globalOnce = true;

    [Tooltip("Промолчать, если в момент входа уже играет (или стоит в очереди) другая реплика.\n" +
             "Реплика-«узнавание» имеет смысл только в свою секунду: если игрок вошёл, уже что-то " +
             "сказав (взял предмет прямо на пороге), она выедет с опозданием и невпопад. " +
             "Такая пропущенная реплика сгорает совсем и позже не всплывает.")]
    public bool SkipIfBusy;

    // Дедуп по тексту — как ReactOnce, чтобы одинаковые зоны не повторяли реплику.
    private static readonly HashSet<string> _saidGlobal = new();
    private bool _saidLocal;

    /// <summary>Забыть все сказанные реплики — новый запуск игры начинается с нуля.</summary>
    public static void ResetSaid() {
        _saidGlobal.Clear();
    }

    private void Reset() {
        Collider c = GetComponent<Collider>();
        if (c != null) {
            c.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag("Player") || string.IsNullOrEmpty(reactionOnce) || TalkUI.instance == null) {
            return;
        }

        if (globalOnce) {
            if (_saidGlobal.Contains(reactionOnce)) {
                return;
            }

            _saidGlobal.Add(reactionOnce);
        } else {
            if (_saidLocal) {
                return;
            }

            _saidLocal = true;
        }

        // Помечаем сказанной в любом случае — реплика одноразовая, и если её
        // момент занят чужой репликой, она не откладывается, а сгорает.
        if (SkipIfBusy && TalkUI.instance.IsBusy) {
            return;
        }

        TalkUI.instance.Say(reactionOnce).Forget();
    }
}
