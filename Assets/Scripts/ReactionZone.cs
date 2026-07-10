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

    // Дедуп по тексту — как ReactOnce, чтобы одинаковые зоны не повторяли реплику.
    private static readonly HashSet<string> _saidGlobal = new();
    private bool _saidLocal;

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

        TalkUI.instance.Say(reactionOnce).Forget();
    }
}
