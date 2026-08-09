using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class MadnessManager : MonoBehaviour {
    public AudioSource humming, clicking;

    public float speedMultiplier = 1.67f;

    public float HummingPower;
    public float ClickingPower;

    public float Madness;

    public float MaxMadness = 100;
    public float TmpMaxMadness = 100;
    public float MadnessSpeedPerS = 1;

    public bool IsMadnessRaising;

    /// <summary>
    /// Игрок читает книгу: безумие стоит на месте, а щелчки и пение
    /// спокойно восстанавливаются. Ставится из <see cref="CollectableBook"/>.
    /// </summary>
    public bool IsBookPaused { get; private set; }

    // До какого момента держится служебная пауза безумия (см. PauseForSeconds).
    private float _storyPauseUntil = -1f;

    /// <summary>
    /// Служебная пауза: играет анимация, которой игрок не управляет (применил
    /// предмет для чиханья, добивает радио). Безумие не растёт и проиграть
    /// нельзя. В отличие от <see cref="IsBookPaused"/> значок паузы НЕ
    /// показывается — это не механика, а страховка от смерти в катсцене.
    /// </summary>
    public bool IsStoryPaused => Time.time < _storyPauseUntil;

    /// <summary>
    /// Придержать безумие на seconds секунд. Вызовы складываются по максимуму,
    /// так что серия ударов по радио держит паузу непрерывно. Пауза снимается
    /// сама по времени: пропущенное событие анимации не может «залипнуть».
    /// </summary>
    public void PauseForSeconds(float seconds) {
        _storyPauseUntil = Mathf.Max(_storyPauseUntil, Time.time + seconds);
    }

    public float HummingChillPerS = 2;

    [Tooltip("Нижний порог «силы голоса» для пения, в процентах шкалы.\n" +
             "Лечение от пения пропорционально остатку шкалы, и без порога оно на выдохе " +
             "сравнивается с ростом безумия: игрок держит E, шкала пения ещё не пуста, " +
             "а шкала рассудка уже стоит на месте. Порог держит пение чуть сильнее роста " +
             "до самого конца шкалы — пока есть чем петь, рассудок растёт.")]
    [Range(0f, 100f)]
    public float MinHummingPercent = 35f;

    [Tooltip("Сколько безумия снимает один щелчок при ПОЛНОЙ шкале щелчков (дальше — пропорционально остатку)")]
    public float ClickChillPerClick = 10;

    [Tooltip("Сколько процентов шкалы щелчков тратит один щелчок")]
    public float ClickCostPerClick = 12.5f;

    public bool IsHumming;

    public float RefillHumming = 1;
    public float RefillClicking = 1;

    public DateTime LastClickTime;
    public float ClickCooldown = 1.5f;
    public KeyCode HummingKey = KeyCode.E;
    public KeyCode ClickKey = KeyCode.Q;

    public RadioAudio RadioAudio;

    public StoryManager StoryManager;
    public HUD hud;
    private bool isDead;
    private float _lastNoHearHint = -999f;
    public bool IsVolumesFixed;
    public AudioSource FakeHummingFade;
    private CancellationTokenSource humCts = new();
    public static MadnessManager instance;

    private void Awake() {
        instance = this;
    }

    private void Start() {
        hud.OnHitLand = TryHit;
        humming.Play();
    }

    private bool _wasHumming;

    public void SyncHumming(float time) {
        humming.time = time;
    }

    private void TryHit() {
        if (hud.HasHammer && CursorRaycast.Raycast(out RaycastHit hit)) {
            hit.transform.GetComponent<InteractiveObj>();
            if (CursorRaycast.CanHit(hit, out HittableObj obj)) {
                obj.Hit();
            }
        }
    }
    
    private void Update() {
        if (!isDead && !FirstPersonController.isHolding) {
            if (Input.GetKeyDown(ClickKey) && (DateTime.Now - LastClickTime).TotalSeconds > ClickCooldown) {
                LastClickTime = DateTime.Now;
                Click();
            }

            if (Input.GetMouseButtonDown(0) && hud.HasHammer) {
                if (CursorRaycast.Raycast(out RaycastHit hit)) {
                    InteractiveObj inter = hit.transform.GetComponent<InteractiveObj>();
                    if (CursorRaycast.CanHit(hit, out HittableObj obj)) {
                        hud.TriggerHit();
                    } else if (inter != null && inter.enabled) {
                        // Взаимодействие с предметом (взять/открыть) — без взмаха
                        // и без звука взмаха. Сам клик по InteractiveObj обрабатывается
                        // в InteractiveObj.OnMouseDown.
                    } else {
                        hud.TriggerSwing();
                    }
                } else {
                    hud.TriggerSwing();
                }
            }
        }

        if (HummingPower <= 80) {
            StoryManager.LogOnce("Hummed");
        }

        if (ClickingPower <= 70) {
            StoryManager.LogOnce("Clicked");
        }

        // С книгой в руках пение не тратится: игрок отдыхает, обе шкалы копятся.
        IsHumming = Input.GetKey(HummingKey) && !IsBookPaused;
        if (!isDead && IsHumming) {
            HummingPower -= HummingChillPerS * 1.5f * Time.deltaTime;
            if (!_wasHumming) {
                hud.SetMelody(true);
                BlendHumming(true);
            }
        } else {
            HummingPower += RefillHumming * Time.deltaTime;

            if (_wasHumming) {
                hud.SetMelody(false);
                BlendHumming(false);
            }
        }

        if (IsMadnessRaising && !IsBookPaused && !IsStoryPaused) {
            Madness += MadnessSpeedPerS * speedMultiplier * Time.deltaTime;
        }

        if (IsHumming) {
            // Порог: на остатке шкалы пение не должно уходить в ноль эффекта,
            // иначе рассудок визуально замирает, хотя игрок ещё поёт.
            float voice = Mathf.Max(HummingPower, MinHummingPercent);
            Madness -= HummingChillPerS * speedMultiplier * 0.01f * voice * Time.deltaTime;
        }

        // Обе шкалы восстанавливаются по одной формуле: щелчки раньше копились
        // в 1.67 раза быстрее пения (лишний speedMultiplier), и «полная шкала»
        // у них стоила разного времени.
        ClickingPower += RefillClicking * Time.deltaTime;

        HummingPower = Mathf.Clamp(HummingPower, 0, 100);
        ClickingPower = Mathf.Clamp(ClickingPower, 0, 100);
        Madness = Mathf.Clamp(Madness, 0, TmpMaxMadness);

        // Подсказка, если игрок «сбил» и щелчки, и пение (обе шкалы почти пусты) —
        // намекаем переждать, пока восстановятся. Не чаще раза в 25 сек.
        if (!isDead && IsMadnessRaising && HummingPower < 15f && ClickingPower < 15f
            && Time.time - _lastNoHearHint > 25f && TalkUI.instance != null) {
            _lastNoHearHint = Time.time;
            TalkUI.instance.Say("Я сам себя не слышу... Нужно немного переждать.").Forget();
        }

        if (!IsVolumesFixed) {
            UpdateSounds();
        }

        humming.volume = FakeHummingFade.volume * HummingPower / 100f;

        // Проигрыш посреди неуправляемой анимации (чиханье, добивание радио)
        // запрещён — см. PauseForSeconds.
        if (!isDead && !IsStoryPaused && Madness >= MaxMadness) {
            StoryManager.Lose();
            isDead = true;
        }

        _wasHumming = IsHumming;
    }

    private void UpdateSounds() {
        RadioAudio.SetPercent(Madness / MaxMadness);
        clicking.volume = ClickingPower / 100f;
    }

    /// <summary>Взял/положил книгу — ставим или снимаем паузу безумия.</summary>
    public void SetBookPause(bool isPaused) {
        IsBookPaused = isPaused;
    }

    public void Click() {
        Madness -= ClickChillPerClick * 0.01f * ClickingPower;
        // Цена щелчка — отдельное число, а не производная от его силы. Раньше
        // они были связаны, и «полная шкала щелчков» всегда давала одно и то же
        // суммарное лечение, как ClickChillPerClick ни крути.
        ClickingPower -= ClickCostPerClick;
        hud.TriggerClick();
        Openable.SnapAllVisible();
    }

    private void BlendHumming(bool isOn) {
        humCts?.Cancel();
        humCts = new CancellationTokenSource();
        FakeHummingFade.DOFade(isOn ? 1 : 0f, 0.3f).WithCancellation(humCts.Token);
    }

    public async UniTask DropMadness(float maxTime) {
        float time = 0;
        while (time < maxTime) {
            TmpMaxMadness = Mathf.Lerp(100, 0, time / maxTime);
            time += Time.deltaTime;
            await UniTask.WaitForEndOfFrame();
        }

        TmpMaxMadness = 0;
    }
}