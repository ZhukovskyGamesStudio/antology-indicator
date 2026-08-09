using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// "Открываемый" объект (шкаф, тумбочка и т.п.) с галлюцинаторным поведением.
///
/// Взаимодействие мышью (<see cref="Interact"/>) открывает/закрывает дверцу.
/// Щелчок пальцем (<see cref="MadnessManager.ClickKey"/>) по кругу меняет
/// содержимое (книги → инструменты → ...). Если шкаф закрыт — играется хлопок
/// дверцей, если открыт — дверца закрывается, и подмена происходит в конце
/// анимации закрытия. В обоих случаях один щелчок = одна смена содержимого.
/// </summary>
public class Openable : MonoBehaviour {
    [Header("Анимации")]
    [Tooltip("Legacy Animation на объекте. Если не задано — берётся с этого GameObject")]
    public Animation anim;

    [Tooltip("Открытие дверцы")]
    public AnimationClip openClip;

    [Tooltip("Закрытие дверцы. Если не задано — играется openClip наоборот")]
    public AnimationClip closeClip;

    [Tooltip("Хлопок дверцей при щелчке — момент смены содержимого в закрытом шкафу")]
    public AnimationClip changeClip;

    [Header("Звук")]
    [Tooltip("Звук открытия дверцы. Проигрывается в Open(). Можно не задавать")]
    public AudioSource openSound;

    [Tooltip("Звук закрытия дверцы. Проигрывается в Close(). Можно не задавать")]
    public AudioSource closeSound;

    [Header("Содержимое")]
    [Tooltip("Состояния содержимого. На каждый щелчок (в закрытом шкафу) активируется " +
             "следующее по кругу. Должно быть >= 2, чтобы было что менять")]
    public List<GameObject> states = new();

    [Tooltip("Задержка от начала хлопка до подмены содержимого — момент, когда дверца " +
             "закрыта и подмена не видна")]
    public float swapAt = 0.25f;

    [Header("Условия")]
    [Tooltip("Реагирует ли предмет на щелчок пальцем. Выключено — висит неподвижно.\n" +
             "Нужно шторам: содержимое за ними больше не перещёлкивается (states пуст), " +
             "и дёргаться на каждый щелчок им незачем")]
    public bool ReactsToSnap = true;

    [Tooltip("Минимальный интервал между сменами, чтобы спам Q не дёргал предмет каждый кадр")]
    public float cooldown = 0.5f;

    [Tooltip("Реплика при первом сдвиге мебели щелчком (один раз за игру, на любом Openable). Пусто — без реакции")]
    public string snapReactionOnce = "Издеваетесь?.. Теперь я двигаю мебель щелчками?";

    private static bool _snapReacted;

    /// <summary>
    /// Сброс статики между запусками игры (из меню приложение не перезапускается):
    /// реакция на первый сдвиг мебели снова прозвучит, а щелчки снова не двигают
    /// содержимое, пока сюжет этого не разрешит.
    /// </summary>
    public static void ResetRunState() {
        _snapReacted = false;
        IsChangeAllowed = false;
    }

    // Игрок — чтобы щелчок не прятал комнату-состояние, внутри которой он стоит
    // (иначе провал в пустоту, баг с ванной).
    private static Transform _player;
    private static Transform Player {
        get {
            if (_player == null) {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) {
                    _player = p.transform;
                }
            }

            return _player;
        }
    }

    /// <summary>Стоит ли игрок сейчас внутри границ этого состояния (комнаты).</summary>
    private static bool PlayerInside(GameObject state) {
        if (state == null || !state.activeInHierarchy || Player == null) {
            return false;
        }

        Renderer[] rs = state.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) {
            return false;
        }

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) {
            b.Encapsulate(rs[i].bounds);
        }

        return b.Contains(Player.position);
    }

    /// <summary>Открыта ли дверца сейчас.</summary>
    public bool IsOpen { get; private set; }

    // Все включённые Openable — чтобы щелчок мог достучаться до тех, что сейчас в кадре.
    private static readonly List<Openable> All = new();

    private Renderer[] _renderers;
    private float _lastChangeTime = -999f;
    private int _currentState;
    private bool _isChanging;

    public static bool IsChangeAllowed = false;

    /// <summary>Виден ли предмет хоть одной камере прямо сейчас.</summary>
    public bool IsVisible {
        get {
            if (_renderers == null) {
                return false;
            }

            foreach (Renderer r in _renderers) {
                if (r != null && r.isVisible) {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake() {
        if (anim == null) {
            anim = GetComponent<Animation>();
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable() {
        All.Add(this);
        ApplyState();
    }

    private void OnDisable() {
        All.Remove(this);
    }

    private void Reset() {
        anim = GetComponent<Animation>();
    }

    /// <summary>Взаимодействие игрока (клик мышью): открыть, если закрыто, и наоборот.</summary>
    public void Interact() {
        if (_isChanging) {
            return;
        }

        if (IsOpen) {
            Close();
        } else {
            Open();
        }
    }

    public void Open() {
        if (IsOpen) {
            return;
        }

        IsOpen = true;
        PlayClip(openClip, reversed: false);
        PlaySound(openSound);
    }

    /// <summary>
    /// Сообщить, что дверцу открыли/закрыли МИМО этого компонента — например,
    /// сюжетным клипом через <see cref="PlayAnim"/>, — не проигрывая ничего заново.
    ///
    /// Без этого состояние разъезжается с картинкой: холодильник в кухонной главе
    /// открывает клип FridgeWork, а <see cref="IsOpen"/> остаётся false, и первый
    /// клик игрока считает дверцу закрытой — она рывком захлопывается и открывается
    /// снова вместо того, чтобы закрыться.
    /// </summary>
    public void SetOpenState(bool isOpen) {
        IsOpen = isOpen;
    }

    public void Close() {
        // Без guard по IsOpen: триггер TriggerToClose должен закрывать дверцы,
        // даже если шкаф-проход открыт "по умолчанию" (IsOpen ещё false).
        IsOpen = false;

        // Закрытие = отдельный клип, либо openClip проигранный наоборот.
        if (closeClip != null) {
            PlayClip(closeClip, reversed: false);
        } else {
            PlayClip(openClip, reversed: true);
        }

        PlaySound(closeSound);
    }

    private static void PlaySound(AudioSource source) {
        if (source != null) {
            source.Play();
        }
    }

    private void PlayClip(AnimationClip clip, bool reversed) {
        if (anim == null || clip == null) {
            return;
        }

        AnimationState state = anim[clip.name];
        if (state == null) {
            return;
        }

        state.speed = reversed ? -1f : 1f;
        state.time = reversed ? state.length : 0f;
        anim.Play(clip.name);
    }

    /// <summary>Щелчок пальцем: меняем содержимое у всех закрытых предметов, что сейчас в кадре.</summary>
    public static void SnapAllVisible() {
        if (!IsChangeAllowed) {
            return;
        }
        // Идём с конца — на случай если TrySnap что-то выключит и изменит список.
        for (int i = All.Count - 1; i >= 0; i--) {
            All[i].TrySnap();
        }
    }

    public void TrySnap() {
        // Работаем только с тем, что сейчас в кадре и не в процессе анимации.
        if (!ReactsToSnap || _isChanging || !IsVisible) {
            return;
        }

        if (Time.time - _lastChangeTime < cooldown) {
            return;
        }

        _lastChangeTime = Time.time;

        // Реакция на первый сдвиг мебели щелчком (один раз за игру).
        if (!_snapReacted && !string.IsNullOrEmpty(snapReactionOnce) && TalkUI.instance != null) {
            _snapReacted = true;
            TalkUI.instance.Say(snapReactionOnce).Forget();
        }

        if (IsOpen) {
            // Открытое — закрываем щелчком и меняем содержимое в конце анимации
            // закрытия, за уже закрытой дверцей. Раньше на это уходило два щелчка
            // (первый закрывал, второй менял) — лишнее действие без смысла.
            CloseAndChange(this.GetCancellationTokenOnDestroy()).Forget();
        } else if (states.Count > 0 && PlayerInside(states[_currentState])) {
            // Не прячем комнату-состояние, внутри которой сейчас стоит игрок —
            // иначе он проваливается в пустоту (баг со щелчком в ванной).
        } else {
            // Закрытое — меняем содержимое (как раньше).
            Change(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    private async UniTaskVoid Change(CancellationToken token) {
        _isChanging = true;

        PlayClip(changeClip, reversed: false);

        if (states.Count > 0) {
            if (await WaitOrCanceled(swapAt, token)) {
                return;
            }

            NextState();
        }

        _isChanging = false;
    }

    /// <summary>
    /// Щелчок по открытому предмету: закрыть дверцу и подменить содержимое,
    /// когда она уже закрылась. Ждём длину того клипа, который реально играет
    /// <see cref="Close"/>, — подмена происходит ровно на последнем кадре.
    /// </summary>
    private async UniTaskVoid CloseAndChange(CancellationToken token) {
        _isChanging = true;

        Close();

        if (states.Count > 0) {
            if (await WaitOrCanceled(CloseDuration, token)) {
                return;
            }

            NextState();
        }

        _isChanging = false;
    }

    /// <summary>Сколько играет закрытие: отдельный клип, либо openClip наоборот.</summary>
    private float CloseDuration {
        get {
            AnimationClip clip = closeClip != null ? closeClip : openClip;
            return clip != null ? clip.length : swapAt;
        }
    }

    /// <returns>true, если ждать больше нет смысла (объект уничтожают).</returns>
    private async UniTask<bool> WaitOrCanceled(float seconds, CancellationToken token) {
        if (seconds <= 0f) {
            return token.IsCancellationRequested;
        }

        return await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token)
            .SuppressCancellationThrow();
    }

    /// <summary>
    /// Следующее состояние содержимого. Проверка PlayerInside повторяется здесь,
    /// а не только перед задержкой: игрок мог зайти в комнату-состояние, пока
    /// играла анимация, и прятать её тогда нельзя — он провалится в пустоту.
    /// </summary>
    private void NextState() {
        if (PlayerInside(states[_currentState])) {
            return;
        }

        _currentState = (_currentState + 1) % states.Count;
        ApplyState();
    }

    private void ApplyState() {
        for (int i = 0; i < states.Count; i++) {
            if (states[i] != null) {
                states[i].SetActive(i == _currentState);
            }
        }
    }
}
