using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранная виньетка, которая проявляется по мере роста безумия.
/// Висит на самой картинке внутри OtherUi — значит, автоматически прячется
/// вместе с остальным HUD в паузе и на экранах победы/проигрыша.
/// </summary>
[RequireComponent(typeof(Image))]
public class MadnessVignette : MonoBehaviour {
    [SerializeField]
    private Image _image;

    [Tooltip("X — безумие 0..1, Y — альфа виньетки 0..1. Держи левую часть в нуле, чтобы в начале игры её не было видно")]
    [SerializeField]
    private AnimationCurve _alphaCurve = new(new Keyframe(0f, 0f), new Keyframe(0.45f, 0f), new Keyframe(1f, 1f));

    [Tooltip("Скорость сглаживания (1/сек), кадронезависимая")]
    [SerializeField]
    private float _responseSpeed = 3f;

    private void Reset() {
        _image = GetComponent<Image>();
    }

    private void Awake() {
        if (_image == null) {
            _image = GetComponent<Image>();
        }

        _image.raycastTarget = false;
        SetAlpha(0f, 0f);
    }

    private void Update() {
        MadnessManager madness = MadnessManager.instance;
        if (madness == null) {
            return;
        }

        float percent = madness.MaxMadness <= 0f ? 0f : Mathf.Clamp01(madness.Madness / madness.MaxMadness);
        float target = Mathf.Clamp01(_alphaCurve.Evaluate(percent));
        float smooth = 1f - Mathf.Exp(-_responseSpeed * Mathf.Min(Time.deltaTime, 0.1f));

        SetAlpha(Mathf.Lerp(_image.color.a, target, smooth), target);
    }

    /// <param name="snapTo">Значение, к которому нужно прилипнуть, когда разница уже незаметна:
    /// экспоненциальное сглаживание до цели не доходит, а запись цвета каждый кадр
    /// без нужды помечает канвас грязным.</param>
    private void SetAlpha(float alpha, float snapTo) {
        if (Mathf.Abs(alpha - snapTo) < 0.002f) {
            alpha = snapTo;
        }

        if (Mathf.Approximately(_image.color.a, alpha)) {
            return;
        }

        Color color = _image.color;
        color.a = alpha;
        _image.color = color;
    }
}
