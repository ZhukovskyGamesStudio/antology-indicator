using TMPro;
using UnityEngine;

/// <summary>
/// Делает статичную TMP-подпись переключаемой по языку.
///
/// Русский ключ берётся из самого текста при загрузке (то, что вписано в сцене),
/// либо из <see cref="sourceOverride"/>, если оно задано. При смене языка текст
/// перерисовывается автоматически.
///
/// Вешать на статичные подписи: кнопки меню, заголовки, экраны паузы/победы.
/// НЕ вешать на тексты, которыми управляет код в рантайме
/// (TalkUI / TasksUI / HintUI / WinPanel — они локализуются сами).
/// </summary>
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour {
    [SerializeField] private TMP_Text target;

    [Tooltip("Если задано — используется как русский ключ. Иначе ключ = текст подписи на момент загрузки.")]
    [TextArea]
    [SerializeField] private string sourceOverride;

    private string _source;
    private bool _captured;

    private void Reset() {
        target = GetComponent<TMP_Text>();
    }

    private void Awake() {
        Capture();
    }

    private void Capture() {
        if (_captured) {
            return;
        }

        if (target == null) {
            target = GetComponent<TMP_Text>();
        }

        _source = !string.IsNullOrEmpty(sourceOverride)
            ? sourceOverride
            : (target != null ? target.text : null);
        _captured = true;
    }

    private void OnEnable() {
        // На случай, если объект стартовал выключенным и Awake ещё не звали.
        Capture();
        Language.OnLanguageChanged += Apply;
        Apply();
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Apply;
    }

    private void Apply() {
        if (target != null && _source != null) {
            target.text = Language.Get(_source);
        }
    }
}
