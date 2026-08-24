using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinPanel : MonoBehaviour {
    [SerializeField]
    private TextMeshProUGUI _endText;

    [Tooltip("Предельная ширина текста в единицах канваса (1920 = вся ширина экрана).\n" +
             "Длиннее — заголовок переносится на вторую строку, а плашка перестаёт расти.")]
    [SerializeField]
    private float _maxTextWidth = 1400f;

    // Русский ключ текущего текста — для перерисовки при смене языка.
    private string _source;
    // Навязывает предел ширины плашке, которая обнимает текст (HLG + ContentSizeFitter).
    private LayoutElement _textLayout;

    private void Awake() {
        _textLayout = TextWidthLimit.EnsureElement(_endText);
    }

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
            TextWidthLimit.Apply(_endText, _textLayout, _maxTextWidth);
        }
    }
}
