using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Держит визуальное состояние UI-тоггла языка в соответствии с текущим языком.
///
/// Кнопки RU/EN уже вызывают <see cref="Language.ChangeLanguage"/> через свой
/// onValueChanged (настроено в сцене). Этот компонент лишь подсвечивает нужный
/// тоггл при загрузке сцены и при смене языка — БЕЗ повторного вызова колбэка
/// (SetIsOnWithoutNotify), поэтому не конфликтует с существующей привязкой.
///
/// Повесить на объект RUToggle (lang = RU) и ENToggle (lang = EN).
/// </summary>
[RequireComponent(typeof(Toggle))]
[DisallowMultipleComponent]
public class LanguageToggle : MonoBehaviour {
    [SerializeField] private LangCode lang;

    private Toggle _toggle;

    private void Awake() {
        _toggle = GetComponent<Toggle>();
    }

    private void OnEnable() {
        if (_toggle == null) {
            _toggle = GetComponent<Toggle>();
        }

        Sync();
        Language.OnLanguageChanged += Sync;
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Sync;
    }

    private void Sync() {
        if (_toggle != null) {
            _toggle.SetIsOnWithoutNotify(Language.Current == lang);
        }
    }
}
