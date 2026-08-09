using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Струя воды из крана. Висит на объекте Water внутри ванной (watercloset).
///
/// Включает воду сюжет — ровно в тот момент, когда из-под шторы натекает лужа
/// (<see cref="StoryManager"/>.SetPuddles). Выключает её игрок: кран зовёт
/// <see cref="StopAll"/> из своего InteractiveObj.OnClick, шум затихает, струя
/// пропадает, а лужи высыхают (<see cref="Puddle.DryAll"/>).
///
/// Перекрытый кран останавливает ВСЮ воду и ВСЕ лужи, а не только «свои».
/// Комнаты лабиринта — копии одной и той же квартиры: журчащий кран в соседней
/// копии показал бы игроку, что закрыл он не тот кран.
/// </summary>
[DisallowMultipleComponent]
public class RunningWater : MonoBehaviour {
    [Tooltip("Сколько секунд затихает шум воды после того, как кран перекрыли. " +
             "Столько же ещё видна струя — вода, которая уже в воздухе, должна долететь")]
    public float StopFade = 0.35f;

    [Tooltip("Кран, которым эту воду перекрывают. Включается и выключается вместе " +
             "с водой: по закрытому крану щёлкать уже нечем")]
    public InteractiveObj Tap;

    [Tooltip("Перекрытый кран высушивает лужи. Снять, если эта вода к лужам отношения не имеет")]
    public bool DriesPuddles = true;

    // Вода, которая сейчас реально течёт. Регистрация по OnEnable/OnDisable,
    // поэтому список сам чистится при выключении комнаты и выгрузке сцены.
    private static readonly List<RunningWater> Flowing = new();

    private AudioSource[] _sources;
    private float[] _volumes;
    private bool _isStopping;

    /// <summary>
    /// Перекрыть все краны и высушить все лужи. Точка входа для
    /// InteractiveObj.OnClick на самом кране.
    /// </summary>
    public void StopAll() {
        // С конца: остановленная вода выключается и вычёркивает себя из списка.
        for (int i = Flowing.Count - 1; i >= 0; i--) {
            Flowing[i].Stop();
        }

        if (DriesPuddles) {
            Puddle.DryAll();
        }
    }

    /// <summary>Мгновенно включить или выключить воду — этим пользуется сюжет.</summary>
    public void SetFlowing(bool isFlowing) {
        if (Tap != null) {
            Tap.enabled = isFlowing;
        }

        gameObject.SetActive(isFlowing);
    }

    /// <summary>Перекрыть этот кран: шум затихает, потом струя выключается.</summary>
    public void Stop() {
        if (!gameObject.activeInHierarchy || _isStopping) {
            return;
        }

        _isStopping = true;
        if (Tap != null) {
            Tap.enabled = false;
        }

        FadeOut(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid FadeOut(CancellationToken ct) {
        for (int i = 0; i < _sources.Length; i++) {
            _sources[i].DOFade(0f, StopFade);
        }

        await UniTask.WaitForSeconds(StopFade, cancellationToken: ct);
        gameObject.SetActive(false);
    }

    private void Awake() {
        _sources = GetComponentsInChildren<AudioSource>(true);
        _volumes = new float[_sources.Length];
        for (int i = 0; i < _sources.Length; i++) {
            _volumes[i] = _sources[i].volume;
        }
    }

    private void OnEnable() {
        // Прошлое перекрытие оставило громкость в нуле (и, возможно, недоигравший
        // твин) — вода начинается с чистого листа.
        _isStopping = false;
        for (int i = 0; i < _sources.Length; i++) {
            _sources[i].DOKill();
            _sources[i].volume = _volumes[i];
        }

        if (!Flowing.Contains(this)) {
            Flowing.Add(this);
        }
    }

    private void OnDisable() {
        Flowing.Remove(this);
    }
}
