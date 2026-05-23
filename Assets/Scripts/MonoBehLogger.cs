using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MonoBehLogger: MonoBehaviour {
    /// <summary>
    /// Кэш сказанных через ReactOnce сообщений на этом компоненте.
    /// Лениво инициализируется, чтобы не платить за неиспользующие.
    /// </summary>
    private HashSet<string> _saidOnce;

    public void Log(string msg) {
        StoryManager.instance.LogEvent(msg);
    }

    public void LogOnce(string msg) {
        StoryManager.instance.LogOnce(msg);
    }
    public void LogClear(string msg) {
        StoryManager.instance.LogClear(msg);
    }

    public void React(string msg) {
        // Реакция fire-and-forget — TalkUI.Say теперь возвращает UniTask
        // и встаёт в очередь, не перебивая активную реплику.
        TalkUI.instance.Say(msg).Forget();
    }

    /// <summary>
    /// Произнести реплику только при первом вызове с такой строкой на этом
    /// компоненте. Подходит для реакций на повторяющиеся действия (например,
    /// поднять/положить один и тот же предмет): первая фраза прозвучит,
    /// последующие — нет.
    /// </summary>
    public void ReactOnce(string msg) {
        if (_saidOnce == null) {
            _saidOnce = new HashSet<string>();
        }

        if (!_saidOnce.Add(msg)) {
            return;
        }

        TalkUI.instance.Say(msg).Forget();
    }
}