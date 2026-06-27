using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class OpenableFolder : MonoBehaviour {
    private Transform _movingPart;

    private CancellationTokenSource _cts = new();

    private void Start() {
        if (_movingPart == null) {
            _movingPart = transform.GetChild(0).GetChild(0);
        }
    }

    public void Open() {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _movingPart.DOLocalRotate(new Vector3(0,0, -120), 0.5f).WithCancellation(_cts.Token);
    }

    public void Close() {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _movingPart.DOLocalRotate(new Vector3(0, 0, 0), 0.5f).WithCancellation(_cts.Token);
    }
}