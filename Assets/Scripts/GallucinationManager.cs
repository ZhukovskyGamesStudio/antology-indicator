using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class GallucinationManager : MonoBehaviour {
    public FirstPersonController firstPersonController;
    public Volume volume;
    public VolumeProfile VolumeProfile;

    public AnimationCurve gallucinationCurve;
    public float randomGal = 0.05f;

    [Header("Fov")]
    public bool IsFov = true;

    [Tooltip("Нормальный FOV (безумие = 0), вокруг него идёт дыхание")]
    public float MinFov;
    public float MaxFov; // оставлено для совместимости, в осцилляции не используется

    [Tooltip("Макс. амплитуда дыхания FOV (градусы) при полном безумии. Амплитуда растёт с безумием")]
    public float FovOscAmplitude = 7f;

    [Tooltip("Скорость дыхания FOV (рад/сек)")]
    public float FovOscSpeed = 1.2f;

    [Tooltip("Скорость возврата FOV к нормальному, когда держишь предмет (1/сек)")]
    public float FovHoldReturnSpeed = 14f;

    [Header("Chromatic Aberration")]
    public bool IsChromaticAberration = true;
    public AnimationCurve aberrationCurve;
    
    [Header("Channels Mixer")]
    public bool IsChannelMixer = true;

    [Header("Depth Of Field")]
    public bool IsDof = true;

    [Header("Smoothing")]
    [Tooltip("Скорость сглаживания эффектов (1/сек), кадронезависимая. 6.32 ≈ прежнее ощущение при 60 FPS; меньше — плавнее, больше — резче.")]
    public float responseSpeed = 6.32f;

    private ChromaticAberration chromaticAberration;
    private ChannelMixer channelMixer;
    private DepthOfField dof;

    private void Start() {
        VolumeProfile = volume.profile;
        VolumeProfile.TryGet(out chromaticAberration);
        VolumeProfile.TryGet(out channelMixer);
        VolumeProfile.TryGet(out dof);
        // Мгновенно выставляем стартовые значения (безумие = 0), без сглаживания.
        UpdateVolume(0, 1f);
    }

    private void Update() {
        float curGal = MadnessManager.instance.Madness / MadnessManager.instance.MaxMadness * Random.Range(1f - randomGal, 1f + randomGal);

        float curved = gallucinationCurve.Evaluate(curGal);

        // Экспоненциальное сглаживание, не зависящее от частоты кадров.
        // Time.deltaTime ограничиваем, чтобы после фриза/загрузки эффекты не "прыгали".
        float smooth = 1f - Mathf.Exp(-responseSpeed * Mathf.Min(Time.deltaTime, 0.1f));

        UpdateVolume(curved, smooth);
    }

    private void UpdateVolume(float curved, float smooth) {
        if (IsFov) {
            if (FirstPersonController.isHolding) {
                // Держим предмет — быстро возвращаем FOV к нормальному (без дыхания).
                float holdSmooth = 1f - Mathf.Exp(-FovHoldReturnSpeed * Mathf.Min(Time.deltaTime, 0.1f));
                firstPersonController.fov = Mathf.Lerp(firstPersonController.fov, MinFov, holdSmooth);
            } else {
                // Постоянное «дыхание» дальше-ближе вокруг нормального FOV.
                // Амплитуда растёт с безумием, но остаётся небольшой.
                float amp = FovOscAmplitude * curved;
                float target = MinFov + amp * Mathf.Sin(Time.time * FovOscSpeed);
                firstPersonController.fov = Mathf.Lerp(firstPersonController.fov, target, smooth);
            }
        }

        if (IsChromaticAberration) {
            // Сначала переводим безумие через кривую в целевую интенсивность,
            // затем плавно ведём к ней — так же, как остальные эффекты (без петли обратной связи).
            float target = aberrationCurve.Evaluate(curved);
            chromaticAberration.intensity.Override(Mathf.Lerp(chromaticAberration.intensity.value, target, smooth));
        }

        if (IsDof) {
            dof.focusDistance.Override( Mathf.Lerp(dof.focusDistance.value, 0.7f * (1 - curved), smooth));
        }

        if (IsChannelMixer) {
            channelMixer.blueOutBlueIn.Override( Mathf.Lerp(channelMixer.blueOutBlueIn.value, Mathf.Lerp(100, 50f, curved), smooth));
            channelMixer.redOutBlueIn.Override( Mathf.Lerp(channelMixer.redOutBlueIn.value, Mathf.Lerp(0, 50f, curved), smooth));
        }
    }
}