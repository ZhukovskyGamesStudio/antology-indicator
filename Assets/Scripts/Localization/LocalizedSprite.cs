using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подменяет спрайт на UI <see cref="Image"/> или на <see cref="SpriteRenderer"/>
/// в зависимости от языка. Для картинок с текстом в UI или на 2D-объектах.
///
/// Обе версии спрайта держите в <c>Assets/Textures/Translatable/</c> и назначьте
/// в поля ru / en. Если en не задан — используется ru (запасной вариант).
/// </summary>
[DisallowMultipleComponent]
public class LocalizedSprite : MonoBehaviour {
    [SerializeField] private Image uiImage;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite ru;
    [SerializeField] private Sprite en;

    private void Reset() {
        uiImage = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() {
        Language.OnLanguageChanged += Apply;
        Apply();
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Apply;
    }

    private void Apply() {
        Sprite sprite = Language.Current == LangCode.EN && en != null ? en : ru;
        if (sprite == null) {
            return;
        }

        if (uiImage != null) {
            uiImage.sprite = sprite;
        }

        if (spriteRenderer != null) {
            spriteRenderer.sprite = sprite;
        }
    }
}
