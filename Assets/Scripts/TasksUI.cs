using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class TasksUI : MonoBehaviour {
    public TextMeshProUGUI text;
    public RectTransform Ckeck;
    private CancellationTokenSource cts;

    public void ShowTask(string taskName) {
        Ckeck.gameObject.SetActive(false);
        text.text = taskName;
    }

    public void CompleteTask() {
        cts?.Cancel();
        Ckeck.gameObject.SetActive(true);
        Ckeck.transform.localScale = new Vector3(0, 1, 1);
        cts = new CancellationTokenSource();
        Ckeck.DOScaleX(1, 0.5f).WithCancellation(cts.Token); 
        text.text = "<s>" + text.text + "</s>";
    }
}