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

    [Tooltip("Нормальный FOV (безумие = 0)")]
    public float MinFov = 60f;

    [Tooltip("FOV при полном безумии — мир слегка отодвигается. Разница с MinFov должна быть небольшой (5–10 градусов)")]
    public float MaxFov = 68f;

    [Tooltip("Скорость возврата FOV к нормальному, когда держишь предмет (1/сек)")]
    public float FovHoldReturnSpeed = 14f;

    [Header("Chromatic Aberration")]
    public bool IsChromaticAberration = true;

    [Tooltip("X — безумие после gallucinationCurve (0..1), Y — интенсивность аберрации (0..1). Чтобы эффект начинался раньше — поднимай левую часть кривой")]
    public AnimationCurve aberrationCurve;

    [Range(0f, 1f)]
    [Tooltip("Общий множитель силы аберрации")]
    public float AberrationMax = 1f;

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

        if (!VolumeProfile.TryGet(out chromaticAberration)) {
            Debug.LogError($"[Gallucination] В профиле {volume.sharedProfile?.name} нет Chromatic Aberration — эффект не будет работать", this);
        } else {
            // Override у параметра ставит сам intensity.Override(), но сам компонент
            // тоже должен быть включён, иначе URP просто пропустит эффект.
            chromaticAberration.active = true;
            chromaticAberration.intensity.overrideState = true;
        }

        VolumeProfile.TryGet(out channelMixer);
        VolumeProfile.TryGet(out dof);
        // Мгновенно выставляем стартовые значения (безумие = 0), без сглаживания.
        UpdateVolume(0, 1f);
    }

    private void Update() {
        MadnessManager madness = MadnessManager.instance;
        if (madness == null || madness.MaxMadness <= 0f) {
            return;
        }

        float curGal = madness.Madness / madness.MaxMadness * Random.Range(1f - randomGal, 1f + randomGal);

        float curved = gallucinationCurve.Evaluate(curGal);

        // Экспоненциальное сглаживание, не зависящее от частоты кадров.
        // Time.deltaTime ограничиваем, чтобы после фриза/загрузки эффекты не "прыгали".
        float smooth = 1f - Mathf.Exp(-responseSpeed * Mathf.Min(Time.deltaTime, 0.1f));

        UpdateVolume(curved, smooth);
    }

    private void UpdateVolume(float curved, float smooth) {
        if (IsFov) {
            if (FirstPersonController.isHolding) {
                // Держим предмет — быстро возвращаем FOV к нормальному.
                float holdSmooth = 1f - Mathf.Exp(-FovHoldReturnSpeed * Mathf.Min(Time.deltaTime, 0.1f));
                firstPersonController.fov = Mathf.Lerp(firstPersonController.fov, MinFov, holdSmooth);
            } else {
                // Без дыхания: с ростом безумия мир просто слегка отодвигается.
                float target = Mathf.Lerp(MinFov, MaxFov, curved);
                firstPersonController.fov = Mathf.Lerp(firstPersonController.fov, target, smooth);
            }
        }

        if (IsChromaticAberration && chromaticAberration != null) {
            // Сначала переводим безумие через кривую в целевую интенсивность,
            // затем плавно ведём к ней — так же, как остальные эффекты (без петли обратной связи).
            float target = Mathf.Clamp01(aberrationCurve.Evaluate(curved) * AberrationMax);
            chromaticAberration.intensity.Override(Mathf.Lerp(chromaticAberration.intensity.value, target, smooth));
        }

        if (IsDof && dof != null) {
            dof.focusDistance.Override( Mathf.Lerp(dof.focusDistance.value, 0.7f * (1 - curved), smooth));
        }

        if (IsChannelMixer && channelMixer != null) {
            channelMixer.blueOutBlueIn.Override( Mathf.Lerp(channelMixer.blueOutBlueIn.value, Mathf.Lerp(100, 50f, curved), smooth));
            channelMixer.redOutBlueIn.Override( Mathf.Lerp(channelMixer.redOutBlueIn.value, Mathf.Lerp(0, 50f, curved), smooth));
        }
    }
}