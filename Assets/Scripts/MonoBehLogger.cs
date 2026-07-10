using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MonoBehLogger : MonoBehaviour {
    public void Log(string msg) {
        StoryManager.instance.LogEvent(msg);
    }

    public void LogOnce(string msg) {
        StoryManager.instance.LogOnce(msg);
    }

    public void LogClear(string msg) {
        StoryManager.instance.LogClear(msg);
    }

    public void ChangeQuest(string newQuest) {
        StoryManager.instance.tasksUI.ShowTaskOnce(newQuest);
    }

    public void PlayHandAnim(string anim) {
        HUD.instance.TriggerAnim(anim);
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
        TalkUI.instance.Say(msg).Forget();
    }
    
    /// <summary>
    /// Реакция, пока у игрока ещё нет настоящего молотка (напр. при подъёме фейкового).
    /// Как только молоток получен — молчит.
    /// </summary>
    public void ReactWhileNoHammer(string msg) {
        if (HUD.instance == null || HUD.instance.HasHammer) {
            return;
        }

        TalkUI.instance.Say(msg).Forget();
    }

    private static readonly List<string> _reactedMessages = new();
    public void ReactOnce(string msg) {
        if (_reactedMessages.Contains(msg)) {
            return;
        }
        _reactedMessages.Add(msg);
        TalkUI.instance.Say(msg).Forget();
    }
}