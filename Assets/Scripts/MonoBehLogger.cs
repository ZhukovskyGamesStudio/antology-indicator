using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Хелперы для вызова из UnityEvent в инспекторе: залогировать событие,
/// сказать реплику, дёрнуть анимацию руки.
///
/// Все методы терпят отсутствие своего адресата: те же префабы (книги) стоят
/// и в главном меню, где нет ни StoryManager, ни TalkUI, ни HUD. Без этих
/// проверок падал бы весь UnityEvent целиком — вместе с тем, что вызывается
/// после (см. <see cref="Pickable.TogglePick"/>).
/// </summary>
public class MonoBehLogger : MonoBehaviour {
    public void Log(string msg) {
        if (StoryManager.instance != null) {
            StoryManager.instance.LogEvent(msg);
        }
    }

    public void LogOnce(string msg) {
        if (StoryManager.instance != null) {
            StoryManager.instance.LogOnce(msg);
        }
    }

    public void LogClear(string msg) {
        if (StoryManager.instance != null) {
            StoryManager.instance.LogClear(msg);
        }
    }

    public void ChangeQuest(string newQuest) {
        if (StoryManager.instance != null && StoryManager.instance.tasksUI != null) {
            StoryManager.instance.tasksUI.ShowTaskOnce(newQuest);
        }
    }

    public void PlayHandAnim(string anim) {
        if (HUD.instance != null) {
            HUD.instance.TriggerAnim(anim);
        }
    }

    public void PlaySound(AudioSource source) {
        if (source != null) {
            source.Play();
        }
    }

    public void React(string msg) {
        // Реакция fire-and-forget. TalkUI сам дедуплицирует подряд идущие
        // одинаковые реплики: если такая же фраза уже играет или стоит
        // в очереди — новая Say-вставка игнорируется.
        if (TalkUI.instance != null) {
            TalkUI.instance.Say(msg).Forget();
        }
    }

    /// <summary>
    /// Реакция, пока у игрока ещё нет настоящего молотка (напр. при подъёме фейкового).
    /// Как только молоток получен — молчит.
    /// </summary>
    public void ReactWhileNoHammer(string msg) {
        if (HUD.instance == null || HUD.instance.HasHammer) {
            return;
        }

        React(msg);
    }

    private static readonly List<string> _reactedMessages = new();

    /// <summary>
    /// Забыть все «сказанные один раз» реплики. Список статический и переживает
    /// смену сцены, поэтому новый запуск игры (из меню, без перезапуска приложения)
    /// обязан его чистить — иначе второе прохождение проходит молча.
    /// </summary>
    public static void ResetReactions() {
        _reactedMessages.Clear();
    }

    public void ReactOnce(string msg) {
        // Некому сказать (главное меню) — и в «уже сказанные» не записываем,
        // иначе реплика сгорела бы, ни разу не прозвучав в игре.
        if (TalkUI.instance == null || _reactedMessages.Contains(msg)) {
            return;
        }

        _reactedMessages.Add(msg);
        TalkUI.instance.Say(msg).Forget();
    }
}
