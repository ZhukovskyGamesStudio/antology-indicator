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
        StoryManager.instance.tasksUI.ShowTask(newQuest);
    }

    public void React(string msg) {
        // Реакция fire-and-forget. TalkUI сам дедуплицирует подряд идущие
        // одинаковые реплики: если такая же фраза уже играет или стоит
        // в очереди — новая Say-вставка игнорируется.
        TalkUI.instance.Say(msg).Forget();
    }
}