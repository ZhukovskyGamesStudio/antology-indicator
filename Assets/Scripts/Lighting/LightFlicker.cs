using UnityEngine;

/// <summary>
/// Живой накал лампы. Абсолютно ровный свет читается как картинка;
/// еле заметное «дыхание» накала читается как комната, в которой кто-то живёт.
///
/// Амплитуда и частота растут вместе с безумием, а ближе к максимуму
/// добавляются короткие провалы накала — свет начинает вести себя нечестно
/// раньше, чем игрок это осознаёт.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour {
    [Range(0f, 0.5f)]
    [Tooltip("Амплитуда колебаний накала при нулевом безумии")]
    public float CalmAmplitude = 0.045f;

    [Range(0f, 0.8f)]
    [Tooltip("Амплитуда при максимальном безумии")]
    public float MadAmplitude = 0.17f;

    [Tooltip("Частота колебаний при нулевом безумии")]
    public float CalmSpeed = 0.55f;

    [Tooltip("Частота при максимальном безумии")]
    public float MadSpeed = 2.3f;

    [Range(0f, 2f)]
    [Tooltip("Шанс в секунду на короткий провал накала при максимальном безумии")]
    public float MadDropoutPerSecond = 0.35f;

    [Tooltip("Длительность провала накала, сек")]
    public float DropoutTime = 0.11f;

    private Light _light;
    private float _baseIntensity;
    private float _lastWritten = -1f;
    private float _seed;
    private float _dropoutLeft;

    private void Awake() {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _seed = Random.value * 1000f;
    }

    private void OnEnable() {
        // Свет могли выключить и включить обратно — начинаем с чистого накала.
        _dropoutLeft = 0f;
        _lastWritten = -1f;
    }

    private void Update() {
        if (_light == null) {
            return;
        }

        // Накал мог поменять кто-то снаружи (StoryManager приглушает комнаты
        // в главе с молотком). Принимаем чужое значение как новую базу.
        if (_lastWritten < 0f || !Mathf.Approximately(_light.intensity, _lastWritten)) {
            _baseIntensity = _light.intensity;
        }

        float madness = 0f;
        MadnessManager manager = MadnessManager.instance;

        if (manager != null && manager.MaxMadness > 0f) {
            madness = Mathf.Clamp01(manager.Madness / manager.MaxMadness);
        }

        float amplitude = Mathf.Lerp(CalmAmplitude, MadAmplitude, madness);
        float speed = Mathf.Lerp(CalmSpeed, MadSpeed, madness);

        // Два слоя шума: медленное дыхание плюс мелкая рябь накала.
        float noise = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f;
        noise += (Mathf.PerlinNoise(_seed + 37f, Time.time * speed * 3.1f) * 2f - 1f) * 0.35f;

        if (madness > 0.35f && Random.value < MadDropoutPerSecond * madness * Time.deltaTime) {
            _dropoutLeft = DropoutTime;
        }

        _dropoutLeft = Mathf.Max(0f, _dropoutLeft - Time.deltaTime);
        float dropout = _dropoutLeft > 0f ? Mathf.Lerp(1f, 0.4f, madness) : 1f;

        _lastWritten = _baseIntensity * (1f + noise * amplitude) * dropout;
        _light.intensity = _lastWritten;
    }
}
