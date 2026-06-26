using UnityEngine;
using UnityEngine.UI;

public class BarsPanel : MonoBehaviour {
    [SerializeField]
    private CanvasGroup _humming, _clicking;

    [SerializeField]
    private Slider _hummingSlider, _clickingSlider;

    [SerializeField]
    private Image _hummingFill, _clickingFill;

    [SerializeField]
    private Gradient _colorGradient;

    [SerializeField]
    private AnimationCurve _alphaCurve;

    public void SetHumming(float percent) {
        _hummingSlider.value = percent;
        _humming.alpha = _alphaCurve.Evaluate(percent);
        _hummingFill.color = _colorGradient.Evaluate(percent);
    }

    public void SetClicking(float percent) {
        _clickingSlider.value = percent;
        _clicking.alpha = _alphaCurve.Evaluate(percent);
        _clickingFill.color = _colorGradient.Evaluate(percent);
    }
}