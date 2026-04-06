using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class BlendItem : MonoBehaviour {
    public Renderer rend1, rend2;

    private CancellationTokenSource cts =  new();

    public float time = 0.4f;

    private void Start() {
        rend1.material.DOFade(1, 0.01f);
        rend2.material.DOFade(0, 0.01f);
    }

    public void Blend(bool isSecond) {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        rend1.material.DOFade(isSecond ? 0 : 1, time).WithCancellation(cts.Token);
        rend2.material.DOFade(isSecond ? 1 : 0, time).WithCancellation(cts.Token);
    }
}