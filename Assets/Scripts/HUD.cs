using System;
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
    private static readonly int Stick = Animator.StringToHash("Stick");
    private static readonly int Feather = Animator.StringToHash("Feather");
    private static readonly int Pepper = Animator.StringToHash("Pepper");
    private static readonly int HasHammerHash = Animator.StringToHash("HasHammer");
    public AudioSource Click, Swing, Hit, Sneeze, Death;

    public Animator anim;

    public CanvasGroup melodyCanvasGroup, cursorCg, handCg;
    public Animation melodyAnimation;

    private CancellationTokenSource cts = new();

    public bool HasHammer;
    public static HUD instance;

    
    public FirstPersonController firstPersonController;
    public Animation fpsAnim;
    public AnimationClip sneeze, death;

    public Action OnHitLand;

    [Header("Чиханье")]
    [Tooltip("Звуки чиханья по нарастающей: 1-й применённый предмет — слабый чих, 2-й — сильнее, " +
             "3-й — во всю силу. Клип выбирается по номеру предмета; если список короче, " +
             "берётся последний. Пусто — играет клип, назначенный на сам AudioSource")]
    public AudioClip[] sneezeClips;

    [Tooltip("На сколько придерживать безумие с момента применения предмета для чиханья: " +
             "анимация руки (~2 с) + выравнивание камеры (0.7 с) + сам чих (~2.9 с)")]
    public float SneezePauseSeconds = 6f;

    public void HitLand() {
        OnHitLand?.Invoke();
    }

    public void TriggerClick() {
        anim.SetTrigger(Click1);
    }

    public void TriggerHit() {
        anim.SetTrigger(Hit1);
    }

    public void TriggerSwing() {
        anim.SetTrigger(Swing1);
    }

    public void TriggerSneeze() {
        anim.SetTrigger(Win1);
    }

    public void TriggerDeath() {
        anim.SetTrigger(Death1);
    }

 public void TriggerFeather() {
        PauseMadnessForSneeze();
        anim.SetTrigger(Feather);
    }

 public void TriggerPepper() {
        PauseMadnessForSneeze();
        anim.SetTrigger(Pepper);
    }

 public void TriggerStick() {
        PauseMadnessForSneeze();
        anim.SetTrigger(Stick);
    }

    /// <summary>
    /// Вызывается из UnityEvent предметов (PlayHandAnim). Предметы для чиханья
    /// приходят сюда же, поэтому паузу безумия ставим по имени триггера —
    /// иначе игрок может умереть посреди анимации, которой не управляет.
    /// </summary>
    public void TriggerAnim(string trigger) {
        if (trigger == "Pepper" || trigger == "Stick" || trigger == "Feather") {
            PauseMadnessForSneeze();
        }

        anim.SetTrigger(trigger);
    }

    private void PauseMadnessForSneeze() {
        if (MadnessManager.instance != null) {
            MadnessManager.instance.PauseForSeconds(SneezePauseSeconds);
        }
    }

    public void SetCursorAndHand(bool isOn) {
        // Важно убить активные tweens на target — иначе при быстром
        // pick/drop накапливаются параллельные DOFade-ы (WithCancellation
        // отменяет только UniTask-await, но не сам твин), и они дерутся
        // за alpha канваса. Из-за этого рука с молотком "застревала"
        // на промежуточной/нулевой прозрачности — animator продолжал
        // дёргать Hit/Swing, но в невидимый канвас.
        float target = isOn ? 1f : 0f;
        handCg.DOKill();
        cursorCg.DOKill();
        handCg.DOFade(target, 0.3f);
        cursorCg.DOFade(target, 0.3f);
    }

    private void Awake() {
        instance = this;
    }

    public void SetHammer(bool isOn) {
        HasHammer = isOn;
        UpdateHammer();
    }
    public void UpdateHammer() {
        anim.SetBool(HasHammerHash, HasHammer);
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

    /// <summary>
    /// Чих. Сила чиха растёт от предмета к предмету: первый — слабый, третий —
    /// во всю силу. Номер берётся из счётчика применённых предметов в сюжете
    /// (событие предмета логается раньше, чем запускается анимация руки).
    /// </summary>
    public void PlaySneeze() {
        AudioClip clip = PickSneezeClip();
        if (clip != null) {
            Sneeze.clip = clip;
        }

        Sneeze.Play();
    }

    private AudioClip PickSneezeClip() {
        if (sneezeClips == null || sneezeClips.Length == 0) {
            return null;
        }

        int used = StoryManager.instance != null ? StoryManager.instance.SneezeItemsUsed : 0;
        return sneezeClips[Mathf.Clamp(used - 1, 0, sneezeClips.Length - 1)];
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
        firstPersonController.cameraCanMove = true;
    }

    public void TeleportBack() {
        firstPersonController.gameObject.transform.position = new Vector3(3.08f, 1.25f, 7.41f);
        StoryManager.instance.storyObjectsContainer.NormalRooms.SetActive(true);
        StoryManager.instance.storyObjectsContainer.LabirintRooms.SetActive(false);
        StoryManager.instance.TeleportRadioBack();
    }
}