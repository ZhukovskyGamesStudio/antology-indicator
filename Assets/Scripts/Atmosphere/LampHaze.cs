using UnityEngine;

/// <summary>
/// Ореол вокруг лампы — «свет, у которого есть объём».
///
/// Сейчас источники света в кадре не читаются как источники: плафон светится
/// эмиссией, блум её слегка размазывает, и на этом всё. Воздуха вокруг лампы нет,
/// поэтому свет ощущается наклеенным на поверхности, а не заполняющим комнату.
///
/// Живёт дочерним объектом лампы, как <see cref="BounceFill"/>, и так же
/// подхватывает у неё интенсивность и цвет: значит, и мерцание LightFlicker,
/// и выключение лампы из StoryManager работают сами собой, без единой правки
/// в сюжетном коде.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class LampHaze : MonoBehaviour {
    [Tooltip("Лампа-хозяин. Пусто — берётся с родителя.")]
    public Light KeyLight;

    [Header("Размер")]
    [Tooltip("Радиус ореола в метрах")]
    public float Radius = 0.85f;

    [Tooltip("Растяжение: (1,1) — круг, (0.6,1.6) — вытянутый вниз след под абажуром")]
    public Vector2 Stretch = Vector2.one;

    [Header("Яркость")]
    [Range(0f, 3f)]
    [Tooltip("Доля интенсивности лампы, уходящая в ореол")]
    public float IntensityFactor = 0.34f;

    [Tooltip("Потолок яркости: у настольной лампы интенсивность 2.6, без потолка ореол выжигает кадр")]
    public float MaxIntensity = 1.1f;

    [Tooltip("Ниже этой интенсивности лампы ореол просто выключается")]
    public float CutoffIntensity = 0.02f;

    [Header("Цвет")]
    [Range(0f, 1f)]
    [Tooltip("1 — цвет берётся из цветовой температуры лампы, 0 — из Tint")]
    public float ColorFromLamp = 1f;

    [Tooltip("Ручной цвет ореола")]
    public Color Tint = new Color(1f, 0.84f, 0.6f);

    [Header("Безумие")]
    [Range(0f, 3f)]
    [Tooltip("Множитель яркости ореола при полном безумии: воздух густеет, лампы начинают ореолить сильнее")]
    public float MadBoost = 1.5f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;

    private void OnEnable() {
        Sync();
    }

    private void OnValidate() {
        Sync();
    }

    private void LateUpdate() {
        Sync();
    }

    private void Sync() {
        if (_renderer == null) {
            _renderer = GetComponent<MeshRenderer>();
        }

        if (_renderer == null) {
            return;
        }

        if (KeyLight == null && transform.parent != null) {
            KeyLight = transform.parent.GetComponentInParent<Light>();
        }

        if (KeyLight == null) {
            _renderer.enabled = false;
            return;
        }

        // Лампу гасят через SetActive на объекте света (так делает ToggleOnOff) —
        // ореол обязан уйти вместе с ней.
        bool lampOn = KeyLight.isActiveAndEnabled && KeyLight.intensity > CutoffIntensity;
        _renderer.enabled = lampOn;

        if (!lampOn) {
            return;
        }

        // Radius — в метрах, а родитель может быть ужат (у люстры масштаб 0.143),
        // поэтому компенсируем масштаб родителя, иначе ореол зависит от того,
        // к какому светильнику его прицепили.
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float sx = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
        float sy = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;

        transform.localScale = new Vector3(
            Radius * 2f * Stretch.x / sx,
            Radius * 2f * Stretch.y / sy,
            1f);

        float madness = 0f;

        if (Application.isPlaying) {
            MadnessManager manager = MadnessManager.instance;

            if (manager != null && manager.MaxMadness > 0f) {
                madness = Mathf.Clamp01(manager.Madness / manager.MaxMadness);
            }
        }

        Color color = Tint;

        if (ColorFromLamp > 0f) {
            Color lampColor = KeyLight.useColorTemperature
                ? Mathf.CorrelatedColorTemperatureToRGB(KeyLight.colorTemperature) * KeyLight.color
                : KeyLight.color;
            color = Color.Lerp(Tint, lampColor, ColorFromLamp);
        }

        float intensity = Mathf.Min(KeyLight.intensity * IntensityFactor, MaxIntensity);
        intensity *= Mathf.Lerp(1f, MadBoost, madness);

        if (_block == null) {
            _block = new MaterialPropertyBlock();
        }

        _renderer.GetPropertyBlock(_block);
        _block.SetColor(ColorId, color);
        _block.SetFloat(IntensityId, intensity);
        _renderer.SetPropertyBlock(_block);
    }
}
