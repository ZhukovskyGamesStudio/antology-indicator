using UnityEngine;

/// <summary>
/// Подменяет текстуру на материале 3D-рендерера в зависимости от языка.
///
/// Положите обе версии текстуры в <c>Assets/Textures/Translatable/</c>
/// (например <c>Note_RU</c> и <c>Note_EN</c>) и назначьте их в поля ru / en.
/// Текстура меняется через MaterialPropertyBlock — общий материал-ассет
/// не модифицируется и лишних инстансов материала не создаётся.
/// </summary>
[DisallowMultipleComponent]
public class LocalizedTexture : MonoBehaviour {
    [SerializeField] private Renderer target;
    [SerializeField] private Texture2D ru;
    [SerializeField] private Texture2D en;

    [Tooltip("Имя свойства текстуры в шейдере. Для URP Lit/Unlit это _BaseMap.")]
    [SerializeField] private string textureProperty = "_BaseMap";

    [Tooltip("Индекс материала на рендерере (если их несколько).")]
    [SerializeField] private int materialIndex;

    private MaterialPropertyBlock _mpb;

    private void Reset() {
        target = GetComponent<Renderer>();
    }

    private void OnEnable() {
        Language.OnLanguageChanged += Apply;
        Apply();
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Apply;
    }

    private void Apply() {
        if (target == null) {
            return;
        }

        Texture2D tex = Language.Current == LangCode.EN && en != null ? en : ru;
        if (tex == null) {
            return;
        }

        _mpb ??= new MaterialPropertyBlock();
        target.GetPropertyBlock(_mpb, materialIndex);
        _mpb.SetTexture(textureProperty, tex);
        target.SetPropertyBlock(_mpb, materialIndex);
    }
}
