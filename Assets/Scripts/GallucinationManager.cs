using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class GallucinationManager : MonoBehaviour {
    public FirstPersonController firstPersonController;
    public VolumeProfile VolumeProfile;

    public AnimationCurve gallucinationCurve;
    public float randomGal = 0.05f;

    [Header("Fov")]
    public bool IsFov = true;

    public float MinFov;
    public float MaxFov;

    [Header("Chromatic Aberration")]
    public bool IsChromaticAberration = true;

    [Header("Channels Mixer")]
    public bool IsChannelMixer = true;

    [Header("Depth Of Field")]
    public bool IsDof = true;

    private ChromaticAberration chromaticAberration;
    private ChannelMixer channelMixer;
    private DepthOfField dof;

    private float lerpSpeed = 0.1f;
    
    private void Start() {
        VolumeProfile.TryGet(out chromaticAberration);
        VolumeProfile.TryGet(out channelMixer);
        VolumeProfile.TryGet(out dof);
        UpdateVolume(0);
    }

    private void Update() {
        float curGal = MadnessManager.instance.Madness / MadnessManager.instance.MaxMadness * Random.Range(1f - randomGal, 1f + randomGal);

        float curved = gallucinationCurve.Evaluate(curGal);

        UpdateVolume(curved);
    }

    private void UpdateVolume(float curved) {
        if (IsFov) {
            firstPersonController.fov = Mathf.Lerp(firstPersonController.fov, Mathf.Lerp(MinFov, MaxFov, curved), lerpSpeed);
        }

        if (IsChromaticAberration) {
            chromaticAberration.intensity.Override(Mathf.Lerp(chromaticAberration.intensity.value, curved, lerpSpeed));
        }

        if (IsDof) {
            dof.focusDistance.value = Mathf.Lerp(dof.focusDistance.value, 0.7f * (1 - curved), lerpSpeed);
        }

        if (IsChannelMixer) {
            channelMixer.blueOutBlueIn.value = Mathf.Lerp(firstPersonController.fov, Mathf.Lerp(100, 50f, curved), lerpSpeed); 
            channelMixer.redOutBlueIn.value =  Mathf.Lerp(firstPersonController.fov, Mathf.Lerp(0, 50f, curved), lerpSpeed); 
        }
    }
}