using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class HUD : MonoBehaviour {
    private static readonly int Click1 = Animator.StringToHash("Click");
    private static readonly int Win1 = Animator.StringToHash("Win");
    private static readonly int Death1 = Animator.StringToHash("Death");
    private static readonly int Hit1 = Animator.StringToHash("Hit");
    private static readonly int Swing1 = Animator.StringToHash("Swing");
    private static readonly int HasHammerHash = Animator.StringToHash("HasHammer");
    public AudioSource Click, Swing, Hit, Sneeze, Death;

    public Animator anim;

    public CanvasGroup melodyCanvasGroup, cursorCg, handCg;
    public Animation melodyAnimation;

    private CancellationTokenSource cts = new();

    public bool HasHammer;
    public static HUD instance;
    private CancellationTokenSource fadeCts = new();

    
    public FirstPersonController firstPersonController;
    public Animation fpsAnim;
    public AnimationClip sneeze, death;

    public void TriggerClick() {
        anim.SetTrigger(Click1);
    }

    public void TriggerHit() {
        anim.SetTrigger(Hit1);
    }

    public void TriggerSwing() {
        anim.SetTrigger(Swing1);
    }

    public void TriggerWin() {
        anim.SetTrigger(Win1);
    }

    public void TriggerDeath() {
        anim.SetTrigger(Death1);
    }

    public void SetCursorAndHand(bool isOn) {
        fadeCts?.Cancel();
        fadeCts = new CancellationTokenSource();
        handCg.DOFade(isOn ? 1 : 0, 0.3f).WithCancellation(fadeCts.Token);
        cursorCg.DOFade(isOn ? 1 : 0, 0.3f).WithCancellation(fadeCts.Token);
    }

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

    public void PlaySneeze() {
        Sneeze.Play();
    }

    public void PlayDeath() {
        Death.Play();
    }

    public void PlayDeathAnim() {
        AsyncDeath();
    }

    private async UniTask AsyncDeath() {
        firstPersonController.cameraCanMove = false;
        firstPersonController.playerCanMove = false;
        var camT = Camera.main.transform;
        await camT.DORotate(new Vector3(0, camT.eulerAngles.y, camT.eulerAngles.z), 0.7f);
        fpsAnim.Play(death.name);
    }

    public void PlaySneezeAnim() {
        AsyncSneeze();
    }

    private async UniTask AsyncSneeze() {
        firstPersonController.cameraCanMove = false;
        firstPersonController.playerCanMove = false;
        var camT = Camera.main.transform;
        await camT.DORotate(new Vector3(0, camT.eulerAngles.y, camT.eulerAngles.z), 0.7f);
        fpsAnim.Play(sneeze.name);
    }
}