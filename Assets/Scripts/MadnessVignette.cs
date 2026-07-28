using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранная виньетка, которая проявляется по мере роста безумия.
///
/// Слоёв может быть несколько (рисованная рамка поверх мягкого затемнения),
/// у каждого — свой набор кадров и своя скорость перещёлкивания: рамка «кипит»,
/// как в рисованной анимации, а мягкая подложка дышит медленнее.
///
/// **Скорость перещёлкивания ведёт то же безумие, что и альфа.** В спокойствии
/// кадры меняются очень медленно, к пику разгоняются до `FrameDurationMad` —
/// то есть виньетка не просто темнеет, она ещё и начинает дёргаться. Постоянная
/// скорость читалась как техническая анимация, идущая сама по себе.
///
/// Порядок слоёв в этом списке ни на что не влияет — что поверх чего решает
/// порядок объектов в иерархии Canvas.
///
/// Живёт внутри OtherUi — значит, автоматически прячется вместе с остальным
/// HUD в паузе и на экранах победы/проигрыша.
/// </summary>
public class MadnessVignette : MonoBehaviour {
    [Serializable]
    public class Layer {
        [Tooltip("Картинка слоя")]
        public Image Image;

        [Tooltip("Кадры по порядку. Один кадр — слой просто не анимируется")]
        public Sprite[] Frames;

        [Tooltip("Потолок альфы этого слоя. Общая альфа по безумию домножается на него")]
        [Range(0f, 1f)]
        public float MaxAlpha = 1f;

        [Tooltip("Сколько держится один кадр в спокойствии, сек. Большое значение — " +
                 "рисунок почти стоит на месте")]
        public float FrameDurationCalm = 1.6f;

        [Tooltip("Сколько держится один кадр на пике безумия, сек")]
        public float FrameDurationMad = 0.13f;

        [Tooltip("Стартовый сдвиг фазы В КАДРАХ — чтобы слои не начинали в такт друг другу")]
        public float StartPhase;

        /// <summary>Положение внутри цикла, в кадрах (0..Frames.Length).</summary>
        [NonSerialized]
        public float Phase;

        [NonSerialized]
        public int ShownFrame = -1;
    }

    [SerializeField]
    private Layer[] _layers = new Layer[0];

    [Tooltip("X — безумие 0..1, Y — альфа виньетки 0..1. Левая часть должна лежать в нуле: " +
             "спокойного игрока виньетка не касается вообще")]
    [SerializeField]
    private AnimationCurve _alphaCurve = new(new Keyframe(0f, 0f), new Keyframe(0.2f, 0f),
        new Keyframe(0.55f, 0.4f), new Keyframe(1f, 1f));

    [Tooltip("Скорость сглаживания (1/сек), кадронезависимая")]
    [SerializeField]
    private float _responseSpeed = 3f;

    private float _alpha;

    private void Awake() {
        _alpha = 0f;
        for (int i = 0; i < _layers.Length; i++) {
            Layer layer = _layers[i];
            if (layer == null || layer.Image == null) {
                continue;
            }

            layer.Image.raycastTarget = false;
            layer.Phase = layer.StartPhase;
            layer.ShownFrame = -1;
            SetAlpha(layer.Image, 0f);
        }
    }

    private void Update() {
        MadnessManager madness = MadnessManager.instance;
        if (madness == null) {
            return;
        }

        float percent = madness.MaxMadness <= 0f ? 0f : Mathf.Clamp01(madness.Madness / madness.MaxMadness);
        float target = Mathf.Clamp01(_alphaCurve.Evaluate(percent));
        float smooth = 1f - Mathf.Exp(-_responseSpeed * Mathf.Min(Time.deltaTime, 0.1f));

        _alpha = Mathf.Lerp(_alpha, target, smooth);

        // Экспоненциальное сглаживание до цели не доходит — прилипаем, когда
        // разница уже незаметна, иначе каждый кадр без нужды пишем цвет.
        if (Mathf.Abs(_alpha - target) < 0.002f) {
            _alpha = target;
        }

        for (int i = 0; i < _layers.Length; i++) {
            Apply(_layers[i], percent);
        }
    }

    private void Apply(Layer layer, float percent) {
        if (layer == null || layer.Image == null) {
            return;
        }

        float alpha = _alpha * layer.MaxAlpha;
        SetAlpha(layer.Image, alpha);

        // Пока виньетки не видно, кадры не листаем: смена спрайта помечает
        // канвас грязным, даже если картинка полностью прозрачна.
        if (alpha <= 0f || layer.Frames == null || layer.Frames.Length == 0) {
            return;
        }

        if (layer.Frames.Length > 1) {
            // Фазу копим сами, а не считаем от Time.unscaledTime: длительность кадра
            // плавает вместе с безумием, и формула от абсолютного времени скакала бы
            // по кадрам при каждом её изменении.
            float duration = Mathf.Lerp(layer.FrameDurationCalm, layer.FrameDurationMad, percent);
            if (duration > 0f) {
                layer.Phase = Mathf.Repeat(layer.Phase + Time.unscaledDeltaTime / duration, layer.Frames.Length);
            }
        }

        int frame = Mathf.Min(layer.Frames.Length - 1, (int)layer.Phase);
        if (frame != layer.ShownFrame && layer.Frames[frame] != null) {
            layer.ShownFrame = frame;
            layer.Image.sprite = layer.Frames[frame];
        }
    }

    private static void SetAlpha(Image image, float alpha) {
        if (Mathf.Approximately(image.color.a, alpha)) {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
