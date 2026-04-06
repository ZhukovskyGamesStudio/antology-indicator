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
}