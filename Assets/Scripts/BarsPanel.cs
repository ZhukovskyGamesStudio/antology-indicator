using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Три шкалы в углу экрана: пение, щелчки и рассудок. Все три невидимы, пока
/// всё хорошо, — альфа берётся из общей кривой и растёт по мере опустошения.
/// </summary>
public class BarsPanel : MonoBehaviour {
    /// <summary>Одна шкала: группа для альфы, сам слайдер и картинка заливки.</summary>
    [Serializable]
    public class Bar {
        public CanvasGroup Group;
        public Slider Slider;
        public Image Fill;

        public void Set(float percent, Gradient gradient, AnimationCurve alphaCurve, float minAlpha = 0f) {
            if (Slider != null) {
                Slider.value = percent;
            }

            if (Group != null) {
                Group.alpha = Mathf.Max(minAlpha, alphaCurve.Evaluate(percent));
            }

            if (Fill != null) {
                Fill.color = gradient.Evaluate(percent);
            }
        }
    }

    [SerializeField]
    private Bar _humming, _clicking, _sanity;

    [Header("Иконки внутри шкалы рассудка")]
    [Tooltip("Значок «рассудок восстанавливается» — всплывает, пока игрок поёт или щёлкает")]
    [SerializeField]
    private Image _increaseIcon;

    [Tooltip("Значок «безумие на паузе» — задел на будущее, сейчас только ссылка")]
    [SerializeField]
    private Image _pauseIcon;

    [Tooltip("Скорость появления/угасания под-иконок (1/сек)")]
    [SerializeField]
    private float _iconFadeSpeed = 12f;

    [SerializeField]
    private Gradient _colorGradient;

    [SerializeField]
    private AnimationCurve _alphaCurve;

    private bool _increaseOn;
    private bool _pauseOn;

    private void Awake() {
        SetIconAlpha(_increaseIcon, 0f);
        SetIconAlpha(_pauseIcon, 0f);
    }

    private void Update() {
        // Иконки живут по нескалированному времени: они не часть геймплея,
        // а индикация ввода, и не должны застывать при Time.timeScale = 0.
        float smooth = 1f - Mathf.Exp(-_iconFadeSpeed * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        FadeIcon(_increaseIcon, _increaseOn ? 1f : 0f, smooth);
        FadeIcon(_pauseIcon, _pauseOn ? 1f : 0f, smooth);
    }

    public void SetHumming(float percent) {
        _humming.Set(percent, _colorGradient, _alphaCurve);
    }

    public void SetClicking(float percent) {
        _clicking.Set(percent, _colorGradient, _alphaCurve);
    }

    /// <summary>Шкала рассудка: 1 — полный рассудок, 0 — безумие на максимуме.</summary>
    /// <param name="minAlpha">Насильно показать шкалу, даже если рассудок ещё полный
    /// (например, когда на ней горит значок паузы).</param>
    public void SetSanity(float percent, float minAlpha = 0f) {
        _sanity.Set(percent, _colorGradient, _alphaCurve, minAlpha);
    }

    /// <summary>Показать/скрыть значок восстановления рассудка (пение, щелчок).</summary>
    public void SetIncrease(bool isOn) {
        _increaseOn = isOn;
    }

    /// <summary>Показать/скрыть значок паузы безумия.</summary>
    public void SetPause(bool isOn) {
        _pauseOn = isOn;
    }

    private static void FadeIcon(Image icon, float target, float smooth) {
        if (icon == null) {
            return;
        }

        float alpha = icon.color.a;
        if (Mathf.Abs(alpha - target) < 0.002f) {
            // Дотягиваем вручную: экспоненциальное сглаживание само по себе
            // до цели никогда не доходит и дёргает канвас каждый кадр.
            if (!Mathf.Approximately(alpha, target)) {
                SetIconAlpha(icon, target);
            }

            return;
        }

        SetIconAlpha(icon, Mathf.Lerp(alpha, target, smooth));
    }

    private static void SetIconAlpha(Image icon, float alpha) {
        if (icon == null) {
            return;
        }

        Color color = icon.color;
        color.a = alpha;
        icon.color = color;
    }
}
