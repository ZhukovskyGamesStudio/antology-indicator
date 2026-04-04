using UnityEngine;

public class RadioAudio : MonoBehaviour {
    public AnimationCurve noiseCurve, curse1Curve, curse2Curve;

    public AudioSource noiseSource, curse1Source, curse2Source;

    public void SetPercent(float percent) {
        noiseSource.volume = noiseCurve.Evaluate(percent);
        curse1Source.volume = curse1Curve.Evaluate(percent);
        curse2Source.volume = curse2Curve.Evaluate(percent);
    }
    
    
    
}