using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Слайдер общей громкости в меню. Управляет <see cref="AudioListener.volume"/>
/// (глобальная громкость, влияет на всё, в т.ч. на выход микшера) и сохраняет
/// значение в PlayerPrefs. По умолчанию слайдер на 100%.
///
/// Позиция слайдера (0..1) — это пользовательская настройка, но реальная громкость
/// = позиция * <see cref="MasterScale"/>. MasterScale — калибровка «комфортного»
/// потолка: при слайдере на 100% громкость AudioListener = MasterScale.
/// </summary>
public class VolumeSettings : MonoBehaviour {
    public const string Key = "Volume";
    public const float DefaultVolume = 1f;

    [Tooltip("Громкость AudioListener при слайдере на 100%. 0.10 — комфортный " +
             "потолок (раньше столько давало ~20% слайдера)")]
    public const float MasterScale = 0.10f;

    [Tooltip("Слайдер громкости. Если пусто — берётся с этого объекта")]
    public Slider slider;

    /// <summary>
    /// Применить сохранённую громкость к <see cref="AudioListener.volume"/> (по умолчанию 50%).
    /// Вызывается и на LoadingScene (чтобы громкость была верной с самого старта), и в меню.
    /// </summary>
    public static float ApplySaved() {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Key, DefaultVolume));
        AudioListener.volume = volume * MasterScale;
        return volume;
    }

    private void Awake() {
        if (slider == null) {
            slider = GetComponent<Slider>();
        }

        float volume = ApplySaved();

        if (slider != null) {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(volume);
            slider.onValueChanged.AddListener(SetVolume);
        }
    }

    public void SetVolume(float volume) {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume * MasterScale;
        PlayerPrefs.SetFloat(Key, volume);
    }
}
