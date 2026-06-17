using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class BlendItem : MonoBehaviour {
    public Renderer BeforeBlend, AfterBlend;

    private CancellationTokenSource _cts =  new();

    public float Time = 0.4f;

    private void Start() {
        BeforeBlend.material.DOFade(1, 0.01f);
        AfterBlend.material.DOFade(0, 0.01f);
    }

    public void Blend(bool isBlended) {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        BeforeBlend.material.DOFade(isBlended ? 0 : 1, Time).WithCancellation(_cts.Token);
        AfterBlend.material.DOFade(isBlended ? 1 : 0, Time).WithCancellation(_cts.Token);
    }
}