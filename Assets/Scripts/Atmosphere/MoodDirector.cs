using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Атмосферный спуск по главам.
///
/// Безумие уже ведёт эффекты в <see cref="GallucinationManager"/>, но безумие —
/// величина возвратная: попел, пощёлкал, и картинка снова как в первую минуту.
/// Поэтому вся игра от первой главы до финала выглядит одинаково — та же тёплая
/// квартира, тот же грейд. Здесь ведётся НЕвозвратная величина: номер главы.
/// Квартира по ходу истории холодеет, темнеет, теряет цвет и набирает зерно, и
/// назад это уже не отыгрывается.
///
/// Делит профиль с <see cref="GallucinationManager"/>, но не пересекается с ним
/// по параметрам: тот правит ChromaticAberration / ChannelMixer / DepthOfField,
/// этот — ColorAdjustments / Vignette / FilmGrain плюс ambient и туман сцены.
/// </summary>
public class MoodDirector : MonoBehaviour {
    [Serializable]
    public class Mood {
        [Tooltip("Только для читаемости в инспекторе")]
        public string Name = "";

        [Header("Ambient (Trilight)")]
        [ColorUsage(false, true)]
        public Color AmbientSky = new Color(0.064f, 0.074f, 0.098f);

        [ColorUsage(false, true)]
        public Color AmbientEquator = new Color(0.056f, 0.051f, 0.046f);

        [ColorUsage(false, true)]
        public Color AmbientGround = new Color(0.048f, 0.038f, 0.029f);

        [Header("Туман")]
        public float FogDensity = 0.032f;

        [ColorUsage(false, true)]
        public Color FogColor = new Color(0.055f, 0.048f, 0.042f);

        [Header("Грейд")]
        public float PostExposure = 0.3f;
        public float Contrast = 10f;
        public float Saturation = 6f;

        [Range(0f, 1f)]
        public float VignetteIntensity = 0.25f;

        [Range(0f, 1f)]
        public float FilmGrain = 0.14f;
    }

    [Tooltip("Volume игровой сцены. Пусто — берётся первый найденный на сцене")]
    public Volume Volume;

    [Tooltip("По одному настроению на главу: 0 — стол, 1 — электричество, 2 — радио, 3 — чип, 4 — финал. " +
             "Если глав больше, чем записей, берётся последняя")]
    public Mood[] Moods = new Mood[0];

    [Tooltip("Скорость перехода между главами (1/сек). Мелкое значение — переход растянут на десятки секунд и незаметен")]
    public float BlendSpeed = 0.25f;

    [Tooltip("Как часто перечитывать номер главы, сек")]
    public float PollInterval = 0.5f;

    [Header("Выключатели")]
    public bool IsAmbient = true;
    public bool IsFog = true;
    public bool IsGrade = true;

    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private FilmGrain _filmGrain;

    private Mood _current;
    private Mood _target;
    private float _pollTimer;
    private int _chapter = -1;

    // Исходное состояние сцены: play mode его и так откатывает, но компонент
    // могут дёрнуть и в эдиторе — возвращаем всё как было.
    private Color _sky, _equator, _ground, _fogColor;
    private float _fogDensity;

    private void Awake() {
        if (Moods == null || Moods.Length == 0) {
            Moods = BuildDefaultMoods();
        }

        _sky = RenderSettings.ambientSkyColor;
        _equator = RenderSettings.ambientEquatorColor;
        _ground = RenderSettings.ambientGroundColor;
        _fogColor = RenderSettings.fogColor;
        _fogDensity = RenderSettings.fogDensity;
    }

    private void Start() {
        if (Volume == null) {
            Volume = FindFirstObjectByType<Volume>();
        }

        if (Volume == null || Volume.profile == null) {
            Debug.LogError("[MoodDirector] Не найден Volume — атмосфера по главам работать не будет", this);
            enabled = false;
            return;
        }

        // volume.profile — рантайм-копия ассета, тот же инстанс, что берёт
        // GallucinationManager. Сам ассет игра не портит.
        Volume.profile.TryGet(out _colorAdjustments);
        Volume.profile.TryGet(out _vignette);
        Volume.profile.TryGet(out _filmGrain);

        _target = ResolveMood(ReadChapter());
        _current = Clone(_target);
        Apply(_current);
    }

    private void Update() {
        _pollTimer -= Time.unscaledDeltaTime;

        if (_pollTimer <= 0f) {
            _pollTimer = PollInterval;
            int chapter = ReadChapter();

            if (chapter != _chapter) {
                _chapter = chapter;
                _target = ResolveMood(chapter);
            }
        }

        if (_current == null || _target == null) {
            return;
        }

        float t = 1f - Mathf.Exp(-BlendSpeed * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        Blend(_current, _target, t);
        Apply(_current);
    }

    private void OnDisable() {
        RenderSettings.ambientSkyColor = _sky;
        RenderSettings.ambientEquatorColor = _equator;
        RenderSettings.ambientGroundColor = _ground;
        RenderSettings.fogColor = _fogColor;
        RenderSettings.fogDensity = _fogDensity;
    }

    private static int ReadChapter() {
        return PlayerPrefs.GetInt("Chapter", 0);
    }

    private Mood ResolveMood(int chapter) {
        if (Moods == null || Moods.Length == 0) {
            return null;
        }

        return Moods[Mathf.Clamp(chapter, 0, Moods.Length - 1)];
    }

    private void Apply(Mood mood) {
        if (mood == null) {
            return;
        }

        if (IsAmbient) {
            RenderSettings.ambientSkyColor = mood.AmbientSky;
            RenderSettings.ambientEquatorColor = mood.AmbientEquator;
            RenderSettings.ambientGroundColor = mood.AmbientGround;
        }

        if (IsFog) {
            RenderSettings.fogDensity = mood.FogDensity;
            RenderSettings.fogColor = mood.FogColor;
        }

        if (!IsGrade) {
            return;
        }

        if (_colorAdjustments != null) {
            _colorAdjustments.postExposure.Override(mood.PostExposure);
            _colorAdjustments.contrast.Override(mood.Contrast);
            _colorAdjustments.saturation.Override(mood.Saturation);
        }

        if (_vignette != null) {
            _vignette.intensity.Override(mood.VignetteIntensity);
        }

        if (_filmGrain != null) {
            _filmGrain.intensity.Override(mood.FilmGrain);
        }
    }

    private static void Blend(Mood from, Mood to, float t) {
        from.AmbientSky = Color.Lerp(from.AmbientSky, to.AmbientSky, t);
        from.AmbientEquator = Color.Lerp(from.AmbientEquator, to.AmbientEquator, t);
        from.AmbientGround = Color.Lerp(from.AmbientGround, to.AmbientGround, t);
        from.FogColor = Color.Lerp(from.FogColor, to.FogColor, t);
        from.FogDensity = Mathf.Lerp(from.FogDensity, to.FogDensity, t);
        from.PostExposure = Mathf.Lerp(from.PostExposure, to.PostExposure, t);
        from.Contrast = Mathf.Lerp(from.Contrast, to.Contrast, t);
        from.Saturation = Mathf.Lerp(from.Saturation, to.Saturation, t);
        from.VignetteIntensity = Mathf.Lerp(from.VignetteIntensity, to.VignetteIntensity, t);
        from.FilmGrain = Mathf.Lerp(from.FilmGrain, to.FilmGrain, t);
    }

    private static Mood Clone(Mood source) {
        if (source == null) {
            return null;
        }

        return new Mood {
            Name = source.Name,
            AmbientSky = source.AmbientSky,
            AmbientEquator = source.AmbientEquator,
            AmbientGround = source.AmbientGround,
            FogDensity = source.FogDensity,
            FogColor = source.FogColor,
            PostExposure = source.PostExposure,
            Contrast = source.Contrast,
            Saturation = source.Saturation,
            VignetteIntensity = source.VignetteIntensity,
            FilmGrain = source.FilmGrain
        };
    }

    private void Reset() {
        Moods = BuildDefaultMoods();
    }

    /// Первая запись — ровно текущий вид сцены, чтобы первая глава выглядела
    /// так же, как до появления этого компонента. Дальше — спуск.
    private static Mood[] BuildDefaultMoods() {
        return new[] {
            new Mood {
                Name = "0. Стол — квартира ещё тёплая",
                AmbientSky = new Color(0.064f, 0.074f, 0.098f),
                AmbientEquator = new Color(0.056f, 0.051f, 0.046f),
                AmbientGround = new Color(0.048f, 0.038f, 0.029f),
                FogDensity = 0.032f,
                FogColor = new Color(0.055f, 0.048f, 0.042f),
                PostExposure = 0.3f,
                Contrast = 10f,
                Saturation = 6f,
                VignetteIntensity = 0.25f,
                FilmGrain = 0.14f
            },
            new Mood {
                Name = "1. Электричество — воздух остывает",
                AmbientSky = new Color(0.056f, 0.066f, 0.096f),
                AmbientEquator = new Color(0.045f, 0.043f, 0.043f),
                AmbientGround = new Color(0.038f, 0.030f, 0.025f),
                FogDensity = 0.038f,
                FogColor = new Color(0.048f, 0.043f, 0.041f),
                PostExposure = 0.18f,
                Contrast = 13f,
                Saturation = -5f,
                VignetteIntensity = 0.30f,
                FilmGrain = 0.18f
            },
            new Mood {
                Name = "2. Радио — свет отступает",
                AmbientSky = new Color(0.047f, 0.056f, 0.090f),
                AmbientEquator = new Color(0.035f, 0.034f, 0.037f),
                AmbientGround = new Color(0.028f, 0.023f, 0.021f),
                FogDensity = 0.045f,
                FogColor = new Color(0.040f, 0.037f, 0.038f),
                PostExposure = 0.04f,
                Contrast = 16f,
                Saturation = -14f,
                VignetteIntensity = 0.35f,
                FilmGrain = 0.21f
            },
            new Mood {
                Name = "3. Чип — цвет уходит",
                AmbientSky = new Color(0.039f, 0.048f, 0.082f),
                AmbientEquator = new Color(0.028f, 0.028f, 0.033f),
                AmbientGround = new Color(0.021f, 0.017f, 0.017f),
                FogDensity = 0.052f,
                FogColor = new Color(0.033f, 0.031f, 0.035f),
                PostExposure = -0.12f,
                Contrast = 20f,
                Saturation = -24f,
                VignetteIntensity = 0.40f,
                FilmGrain = 0.25f
            },
            new Mood {
                Name = "4. Финал — холодная и выцветшая",
                AmbientSky = new Color(0.031f, 0.040f, 0.076f),
                AmbientEquator = new Color(0.022f, 0.023f, 0.030f),
                AmbientGround = new Color(0.015f, 0.013f, 0.014f),
                FogDensity = 0.060f,
                FogColor = new Color(0.026f, 0.026f, 0.032f),
                PostExposure = -0.3f,
                Contrast = 24f,
                Saturation = -35f,
                VignetteIntensity = 0.47f,
                FilmGrain = 0.30f
            }
        };
    }
}
