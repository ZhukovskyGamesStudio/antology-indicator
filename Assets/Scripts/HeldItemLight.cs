using UnityEngine;

/// <summary>
/// Небольшой свет из камеры на предмет в руках: плавно разгорается, когда игрок
/// что-то поднял, и гаснет, когда положил. Висит на источнике света — дочернем
/// объекте камеры, смотрящем вперёд.
/// </summary>
[RequireComponent(typeof(Light))]
public class HeldItemLight : MonoBehaviour {
    [SerializeField]
    private Light _light;

    [Tooltip("Яркость, когда предмет в руках")]
    [SerializeField]
    private float _intensity = 1.6f;

    [Tooltip("Скорость разгорания/затухания (1/сек), кадронезависимая")]
    [SerializeField]
    private float _fadeSpeed = 5f;

    private void Reset() {
        _light = GetComponent<Light>();
    }

    private void Awake() {
        if (_light == null) {
            _light = GetComponent<Light>();
        }

        _light.intensity = 0f;
        _light.enabled = false;
    }

    private void Update() {
        float target = FirstPersonController.isHolding ? _intensity : 0f;
        float smooth = 1f - Mathf.Exp(-_fadeSpeed * Mathf.Min(Time.deltaTime, 0.1f));
        float value = Mathf.Lerp(_light.intensity, target, smooth);

        if (value < 0.005f) {
            value = 0f;
        }

        _light.intensity = value;
        // Погасший источник незачем гонять через рендер каждый кадр.
        _light.enabled = value > 0f;
    }
}
