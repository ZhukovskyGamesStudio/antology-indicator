using UnityEngine;

/// <summary>
/// Пока игрок читает книгу, мир отходит на второй план: звуки не становятся
/// тише — они глохнут, как из-за стены. Висит на объекте с <see cref="AudioListener"/>
/// (PlayerCamera), поэтому берёт весь микс разом и не требует правок отдельных
/// источников.
///
/// Эффект «издалека» — это НЕ громкость. Издалека до нас доходят только низы:
/// высокие частоты съедает воздух и стены, а то, что дошло, приходит с
/// отражениями. Поэтому здесь два слоя — срез верхов (<see cref="AudioLowPassFilter"/>)
/// и лёгкий хвост отражений (<see cref="AudioReverbFilter"/>). Общую громкость
/// компонент не трогает вовсе: во-первых, приглушение читалось бы просто как
/// «сделали тише», во-вторых, <see cref="AudioListener.volume"/> занят ползунком
/// громкости из меню (<see cref="VolumeSettings"/>).
///
/// Срез ведётся по логарифму частоты: 22000 → 700 линейно по герцам почти весь
/// путь проходит в неслышимой области и в конце обваливается рывком.
/// </summary>
[RequireComponent(typeof(AudioListener))]
[DisallowMultipleComponent]
public class ReadingMuffle : MonoBehaviour {
    [Header("Срез верхов")]
    [Tooltip("Частота среза с книгой в руках, Гц. Ниже — глуше и «дальше»")]
    public float MuffledCutoff = 700f;

    [Tooltip("Частота среза в обычном состоянии, Гц. 22000 — фильтр не слышен вовсе")]
    public float OpenCutoff = 22000f;

    [Tooltip("Резонанс фильтра. 1 — чистый срез без подчёркнутой полосы у частоты среза")]
    public float Resonance = 1f;

    [Header("Отражения — чтобы это было «издалека», а не «под подушкой»")]
    [Tooltip("Добавлять хвост отражений. Без него срез верхов читается как глухота, а не как расстояние")]
    public bool UseReverb = true;

    [Tooltip("Громкость отражений при чтении, мБ. -10000 — отражений нет совсем")]
    public float MuffledRoom = -1500f;

    [Tooltip("Завал верхов в самих отражениях, мБ")]
    public float MuffledRoomHF = -1200f;

    [Tooltip("Длина хвоста отражений, сек")]
    public float ReverbDecay = 1.3f;

    [Header("Переходы")]
    [Tooltip("За сколько секунд мир глохнет, когда книгу взяли")]
    public float FadeIn = 0.4f;

    [Tooltip("За сколько секунд мир возвращается, когда книгу положили")]
    public float FadeOut = 0.55f;

    // Книга, которую сейчас читают. Статика переживает смену сцены, но
    // самолечится: уничтоженный Pickable читается как null, а спрятанный
    // в руках (Pickable.OnDisable) перестаёт быть IsPicked — то есть
    // «залипнуть» приглушённым мир не может даже без явного сброса.
    private static Pickable _reading;

    private AudioLowPassFilter _lowPass;
    private AudioReverbFilter _reverb;

    // 0 — обычный мир, 1 — полностью приглушённый.
    private float _blend;

    /// <summary>Игрок взял книгу в руки или положил её.</summary>
    public static void SetReading(Pickable book, bool isReading) {
        if (isReading) {
            _reading = book;
        } else if (_reading == book) {
            _reading = null;
        }
    }

    private static bool IsReading => _reading != null && _reading.IsPicked && _reading.isActiveAndEnabled;

    private void Awake() {
        _lowPass = GetComponent<AudioLowPassFilter>();
        if (_lowPass == null) {
            _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        }

        _lowPass.lowpassResonanceQ = Resonance;
        _lowPass.cutoffFrequency = OpenCutoff;

        if (!UseReverb) {
            return;
        }

        _reverb = GetComponent<AudioReverbFilter>();
        if (_reverb == null) {
            _reverb = gameObject.AddComponent<AudioReverbFilter>();
        }

        // User — единственный пресет, чьи поля можно вести руками. Готовые
        // пресеты переключаются рывком, а нам нужен плавный вход.
        _reverb.reverbPreset = AudioReverbPreset.User;
        _reverb.dryLevel = 0f; // прямой звук не трогаем — тише быть не должно
        _reverb.decayTime = ReverbDecay;
        _reverb.decayHFRatio = 0.4f;
        _reverb.room = -10000f;
        _reverb.roomHF = MuffledRoomHF;
    }

    private void Update() {
        bool reading = IsReading;
        float speed = reading ? FadeIn : FadeOut;
        float target = reading ? 1f : 0f;

        _blend = speed > 0f
            ? Mathf.MoveTowards(_blend, target, Time.unscaledDeltaTime / speed)
            : target;

        Apply();
    }

    private void Apply() {
        // Частоту ведём по логарифму: на слух путь от 22 кГц до 700 Гц — это
        // равномерный спуск в октавах, а не в герцах.
        float open = Mathf.Log10(Mathf.Max(20f, OpenCutoff));
        float muffled = Mathf.Log10(Mathf.Max(20f, MuffledCutoff));
        _lowPass.cutoffFrequency = Mathf.Pow(10f, Mathf.Lerp(open, muffled, _blend));

        if (_reverb != null) {
            _reverb.room = Mathf.Lerp(-10000f, MuffledRoom, _blend);
        }
    }
}
