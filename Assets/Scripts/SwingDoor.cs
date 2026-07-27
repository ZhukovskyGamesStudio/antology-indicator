using DG.Tweening;
using UnityEngine;

/// <summary>
/// Обычная распашная дверь: клик — открыть, ещё клик — закрыть.
///
/// В отличие от <see cref="Openable"/> не требует ни легаси-<c>Animation</c>,
/// ни отдельного клипа: у дверных моделей пивот стоит в петлях, поэтому дверь
/// достаточно повернуть вокруг мировой вертикали через собственный пивот.
/// Поза, в которой дверь стоит в сцене, считается закрытой.
/// </summary>
[DisallowMultipleComponent]
public class SwingDoor : MonoBehaviour {
    [Tooltip("На сколько градусов распахивается дверь. Знак задаёт сторону открывания")]
    public float openAngle = 95f;

    [Tooltip("Сколько секунд занимает открытие/закрытие")]
    public float duration = 0.6f;

    [Tooltip("Звук открытия. Можно не задавать")]
    public AudioSource openSound;

    [Tooltip("Звук закрытия. Если пусто — играется звук открытия")]
    public AudioSource closeSound;

    /// <summary>Открыта ли дверь сейчас.</summary>
    public bool IsOpen { get; private set; }

    private Quaternion _closed;
    private Tween _tween;

    private void Awake() {
        // Как дверь поставлена в сцене — так она и закрыта.
        _closed = transform.rotation;
    }

    private void OnDestroy() {
        _tween?.Kill();
    }

    /// <summary>Взаимодействие игрока: закрытую открываем, открытую закрываем.</summary>
    public void Interact() {
        Set(!IsOpen);
    }

    public void Open() {
        Set(true);
    }

    public void Close() {
        Set(false);
    }

    private void Set(bool open) {
        IsOpen = open;

        Quaternion target = open
            ? Quaternion.AngleAxis(openAngle, Vector3.up) * _closed
            : _closed;

        _tween?.Kill();
        _tween = transform.DORotateQuaternion(target, duration).SetEase(Ease.OutQuad);

        AudioSource sound = open ? openSound : (closeSound != null ? closeSound : openSound);
        if (sound != null) {
            sound.Play();
        }
    }
}
