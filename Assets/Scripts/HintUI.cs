using TMPro;
using UnityEngine;

public class HintUI : MonoBehaviour {

    public TextMeshProUGUI tmp;

    // Русский ключ текущей подсказки — для перерисовки при смене языка.
    private string _source = "";

    private void OnEnable() {
        Language.OnLanguageChanged += Apply;
        // Подсказка прячется вместе с игровым UI на паузу, а OnDisable отписывает
        // от смены языка — добираем пропущенное переключение при включении.
        Apply();
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Apply;
    }

    public void ShowHint(string text) {
        _source = text;
        Apply();
    }

    public void Hide() {
        _source = "";
        Apply();
    }

    private void Apply() {
        if (tmp != null) {
            tmp.text = Language.Get(_source);
        }
    }

}
