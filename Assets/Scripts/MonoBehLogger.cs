using Cysharp.Threading.Tasks;
using UnityEngine;

public class MonoBehLogger: MonoBehaviour {
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
}