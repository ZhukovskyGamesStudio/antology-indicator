using UnityEngine;

/// <summary>
/// Мягкий бестеневой «отражённый» свет рядом с основной лампой.
///
/// Настоящего GI в сцене нет: лайтмапы числятся запечёнными, но реально
/// залайтмаплено 4 рендерера из 700, а ambient до этого был чистый чёрный.
/// Поэтому потолок вокруг плафона и углы комнат проваливались в ноль —
/// комната читалась как пещера, а не как комната.
///
/// Этот свет подхватывает интенсивность, дальность и слой света у родительской
/// лампы, поэтому per-instance правки лампы (у каждой комнаты своя яркость)
/// и рантайм-приглушение из StoryManager работают сами собой.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class BounceFill : MonoBehaviour {
    [Tooltip("Основная лампа. Пусто — берётся с родительского объекта.")]
    public Light KeyLight;

    [Range(0f, 1f)]
    [Tooltip("Доля интенсивности основной лампы")]
    public float IntensityFactor = 0.42f;

    [Tooltip("Множитель дальности: заполняющий бьёт дальше и мягче")]
    public float RangeFactor = 1.45f;

    [Tooltip("Цветовая температура. Чуть теплее ключевой — так читается отражение от обоев и дерева.")]
    public float Temperature = 3400f;

    private Light _light;

    private void OnEnable() {
        Configure();
        Follow();
    }

    private void OnValidate() {
        Configure();
        Follow();
    }

    private void LateUpdate() {
        // В рантайме лампа мерцает (LightFlicker) и её приглушает StoryManager —
        // заполняющий должен идти следом, иначе при выключенном свете он останется гореть.
        if (Application.isPlaying) {
            Follow();
        }
    }

    /// Постоянные свойства: заполняющий никогда не платит за тени.
    private void Configure() {
        if (_light == null) {
            _light = GetComponent<Light>();
        }

        if (_light == null) {
            return;
        }

        _light.type = LightType.Point;
        _light.shadows = LightShadows.None;
        _light.useColorTemperature = true;
        _light.colorTemperature = Temperature;
        _light.color = Color.white;
        _light.bounceIntensity = 0f;
    }

    /// Значения, зависящие от основной лампы.
    private void Follow() {
        if (_light == null) {
            return;
        }

        if (KeyLight == null && transform.parent != null) {
            KeyLight = transform.parent.GetComponentInParent<Light>();
        }

        if (KeyLight == null || KeyLight == _light) {
            return;
        }

        float intensity = KeyLight.intensity * IntensityFactor;
        float range = KeyLight.range * RangeFactor;

        // Пишем только при реальном изменении: в эдиторе иначе сцена дёргается как «изменённая».
        if (!Mathf.Approximately(_light.intensity, intensity)) {
            _light.intensity = intensity;
        }

        if (!Mathf.Approximately(_light.range, range)) {
            _light.range = range;
        }

        // Покомнатная изоляция света обязана сохраниться, иначе заполняющий
        // засветит соседние комнаты сквозь стены.
        if (_light.renderingLayerMask != KeyLight.renderingLayerMask) {
            _light.renderingLayerMask = KeyLight.renderingLayerMask;
        }
    }
}
