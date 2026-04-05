using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class TalkUI : MonoBehaviour {
    public TextMeshProUGUI text;

    private CancellationTokenSource _cts =  new CancellationTokenSource();

    public void Say(string text) {
        _cts?.Cancel();
        _cts =  new CancellationTokenSource();
        SayAsync(text);
    }

    private async UniTask SayAsync(string phraze) {
        text.text = phraze;
        await UniTask.WaitForSeconds(2f, cancellationToken: _cts.Token);
        text.text = "";
    }
}
