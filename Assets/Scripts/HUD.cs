using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class HUD : MonoBehaviour {
    private static readonly int Click1 = Animator.StringToHash("Click");
    public AudioSource Click;
    
    public Animator anim;
    
    public CanvasGroup melodyCanvasGroup;
    public Animation melodyAnimation;

    private CancellationTokenSource cts =  new CancellationTokenSource();
    
    public void PlayClick() {
        anim.SetTrigger(Click1);
    }
    
    public void ClickSound() {
        Click.Play();
    }

    public void SetMelody(bool isOn) {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        melodyAnimation.Stop();
        melodyAnimation.Play();
        melodyCanvasGroup.DOFade(isOn ? 0.1f : 0f, 0.3f).WithCancellation(cts.Token);
    }
    
    
}
