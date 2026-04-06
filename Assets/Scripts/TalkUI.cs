using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TalkUI : MonoBehaviour {
    public TextMeshProUGUI text;
    public Image back;
    private CancellationTokenSource _cts =  new CancellationTokenSource();

    public static TalkUI instance;

    private void Awake() {
        instance = this;
    }

    public void Say(string text) {
        _cts?.Cancel();
        _cts =  new CancellationTokenSource();
        SayAsync(text);
    }

    private async UniTask SayAsync(string phraze) {
        back.gameObject.SetActive(true);
        text.text = phraze;
        await UniTask.WaitForSeconds(2f, cancellationToken: _cts.Token);
        text.text = "";
        back.gameObject.SetActive(false);
    }
}
