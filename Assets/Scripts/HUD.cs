using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class HUD : MonoBehaviour {
    private static readonly int Click1 = Animator.StringToHash("Click");
    private static readonly int Hit1 = Animator.StringToHash("Hit");
    private static readonly int Swing1 = Animator.StringToHash("Swing");
    private static readonly int HasHammerHash = Animator.StringToHash("HasHammer");
    public AudioSource Click, Swing, Hit;

    public Animator anim;

    public CanvasGroup melodyCanvasGroup;
    public Animation melodyAnimation;

    private CancellationTokenSource cts = new();
    
    public bool HasHammer = false;

    public void TriggerClick() {
        anim.SetTrigger(Click1);
    }
    public void TriggerHit() {
        anim.SetTrigger(Hit1);
    }
    public void TriggerSwing() {
        anim.SetTrigger(Swing1);
    }

    public static HUD instance;
    private void Awake() {
        instance = this;
    }

    public void SetHammer(bool isOn) {
        HasHammer = isOn;
        anim.SetBool(HasHammerHash, isOn);
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
    

    public void PlaySwing() {
        Swing.Play();
    }

    public void PlayHit() {
        Hit.Play();
    }
}