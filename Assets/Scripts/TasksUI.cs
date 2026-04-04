using TMPro;
using UnityEngine;

public class TasksUI: MonoBehaviour {


    public TextMeshProUGUI text;
    
    public void ShowTask(string taskName) {
        text.text = taskName;
    }

    public void CompleteTask() {
        text.text = "<s>" + text.text + "</s>";
    }
}
