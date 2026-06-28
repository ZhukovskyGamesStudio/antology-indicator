using TMPro;
using UnityEngine;

public class WinPanel : MonoBehaviour {
    [SerializeField]
    private TextMeshProUGUI _endText;

    // Русский ключ текущего текста — для перерисовки при смене языка.
    private string _source;

    private void OnEnable() {
        Language.OnLanguageChanged += Apply;
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Apply;
    }

    public void SetText(string text) {
        _source = text;
        Apply();
    }

    private void Apply() {
        if (_endText != null && _source != null) {
            _endText.text = Language.Get(_source);
        }
    }
}
