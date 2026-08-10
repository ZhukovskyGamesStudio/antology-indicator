using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранные ТВ-помехи. Включаются в финале, когда диалог «обрывается»:
/// картинка уходит в шум, а заголовок и кнопка выхода остаются поверх (они выше
/// по иерархии Canvas).
///
/// Шум намеренно НЕ пиксельный белый: чистый рандом по точкам читается как
/// техническая заглушка, а не как телевизор. Здесь зерно строится строками —
/// у каждой строки развёртки своя яркость, внутри строки идут горизонтальные
/// штрихи, сверху ползёт мягкая полоса кадровой развёртки, и через строку
/// проходит гребёнка. Это те четыре признака, по которым глаз узнаёт ТВ-шум.
///
/// Второе: помехи полупрозрачные. Раньше они закрывали кадр целиком, и сцена под
/// ними просто исчезала. Альфа ограничена <see cref="maxAlpha"/>, и вдобавок
/// каждая точка тем прозрачнее, чем она темнее (<see cref="minPixelAlpha"/>) —
/// так шум ложится ПОВЕРХ картинки, а не вместо неё.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ScreenStatic : MonoBehaviour {
    [Header("Текстура")]
    [Tooltip("Сколько строк развёртки. Меньше — крупнее и «телевизионнее» гребёнка")]
    public int lines = 216;

    [Tooltip("Сколько точек в строке. Растягивается на всю ширину экрана")]
    public int columns = 384;

    [Tooltip("Частота обновления шума в секунду (0 — каждый кадр)")]
    public float updatesPerSecond = 24f;

    [Header("Зерно")]
    [Tooltip("Средняя яркость шума. Выше — ближе к белому экрану, ниже — к тёмному")]
    [Range(0f, 1f)]
    public float level = 0.56f;

    [Tooltip("Минимальная длина горизонтального штриха, в точках")]
    public int minRun = 2;

    [Tooltip("Максимальная длина горизонтального штриха. Чем длиннее, тем спокойнее шум")]
    public int maxRun = 9;

    [Tooltip("Разброс яркости внутри строки: 0 — строка ровная, 1 — каша")]
    [Range(0f, 1f)]
    public float grain = 0.16f;

    [Tooltip("Разброс яркости между строками — то, что даёт горизонтальную полосатость")]
    [Range(0f, 1f)]
    public float rowContrast = 0.42f;

    [Tooltip("Насколько темнее каждая вторая строка (гребёнка ЭЛТ). 1 — гребёнки нет")]
    [Range(0f, 1f)]
    public float scanlineDarkness = 0.84f;

    [Tooltip("Доля строк-«срывов»: редкая яркая строка во всю ширину")]
    [Range(0f, 0.2f)]
    public float tearChance = 0.025f;

    [Header("Полоса кадровой развёртки")]
    [Tooltip("Скорость движения полосы по экрану, экранов в секунду. 0 — полоса стоит")]
    public float rollSpeed = 0.3f;

    [Tooltip("Высота полосы в долях экрана")]
    [Range(0f, 0.5f)]
    public float barWidth = 0.14f;

    [Tooltip("Насколько полоса светлее остального шума")]
    [Range(0f, 1f)]
    public float barLift = 0.3f;

    [Header("Прозрачность")]
    [Tooltip("Потолок альфы: до него доводит Show(). Меньше — сквозь помехи лучше видно сцену")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.62f;

    [Tooltip("Альфа самой тёмной точки шума. Светлые точки всегда непрозрачные")]
    [Range(0f, 1f)]
    public float minPixelAlpha = 0.3f;

    [Tooltip("Доля точек с цветной каймой (0 — чисто чёрно-белый шум)")]
    [Range(0f, 1f)]
    public float colorAmount = 0.06f;

    private RawImage _img;
    private Texture2D _tex;
    private Color32[] _buf;
    private float _lastUpdate;
    private int _width;
    private int _height;

    private void Awake() {
        _img = GetComponent<RawImage>();
        _width = Mathf.Max(8, columns);
        _height = Mathf.Max(8, lines);

        // Bilinear, а не Point: строк меньше, чем пикселей на экране, и жёсткая
        // лесенка читалась бы как «низкое разрешение», а не как развёртка.
        _tex = new Texture2D(_width, _height, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _buf = new Color32[_width * _height];
        _img.texture = _tex;
        Regenerate();
    }

    private void OnEnable() {
        _lastUpdate = float.NegativeInfinity;
    }

    private void Update() {
        float interval = updatesPerSecond > 0f ? 1f / updatesPerSecond : 0f;
        if (Time.unscaledTime - _lastUpdate >= interval) {
            _lastUpdate = Time.unscaledTime;
            Regenerate();
        }
    }

    /// <summary>
    /// Проявить помехи, плавно подняв альфу до <see cref="maxAlpha"/>
    /// за <paramref name="duration"/> секунд.
    /// </summary>
    public async UniTask Show(float duration) {
        gameObject.SetActive(true);
        Color c = _img.color;
        c.a = 0f;
        _img.color = c;
        await _img.DOFade(maxAlpha, duration).SetUpdate(true)
            .WithCancellation(this.GetCancellationTokenOnDestroy());
    }

    private void Regenerate() {
        float roll = rollSpeed != 0f ? Mathf.Repeat(Time.unscaledTime * rollSpeed, 1f) : 0.5f;
        float half = Mathf.Max(barWidth, 0.0001f);
        int runMin = Mathf.Max(1, minRun);
        int runMax = Mathf.Max(runMin, maxRun);

        for (int y = 0; y < _height; y++) {
            // Яркость всей строки целиком — главный признак ТВ-шума: глаз видит
            // горизонтальные полосы, а не отдельные точки.
            float row = level + (Random.value - 0.5f) * rowContrast;
            if (Random.value < tearChance) {
                row = Random.Range(0.85f, 1f);
            }

            // Мягкая полоса кадровой развёртки, ползущая по экрану.
            float v = _height > 1 ? y / (float)(_height - 1) : 0f;
            float toBar = Mathf.Abs(Mathf.Repeat(v - roll + 0.5f, 1f) - 0.5f);
            row += barLift * Mathf.Clamp01(1f - toBar / half);

            // Гребёнка строк.
            if ((y & 1) == 1) {
                row *= scanlineDarkness;
            }

            int rowStart = y * _width;
            int x = 0;
            while (x < _width) {
                int run = Mathf.Min(Random.Range(runMin, runMax + 1), _width - x);
                float g = Mathf.Clamp01(row + Random.Range(-grain, grain));
                byte lum = (byte)(g * 255f);
                byte a = (byte)(Mathf.Lerp(minPixelAlpha, 1f, g) * 255f);

                Color32 c;
                if (colorAmount > 0f && Random.value < colorAmount) {
                    // Цветная кайма ЭЛТ: холодная или тёплая, но не радуга из
                    // случайных RGB — та и делала прежний шум «цифровым».
                    byte dim = (byte)(lum * 0.62f);
                    c = Random.value < 0.5f
                        ? new Color32(dim, lum, lum, a)
                        : new Color32(lum, dim, (byte)(lum * 0.85f), a);
                } else {
                    c = new Color32(lum, lum, lum, a);
                }

                for (int i = 0; i < run; i++) {
                    _buf[rowStart + x + i] = c;
                }

                x += run;
            }
        }

        _tex.SetPixels32(_buf);
        _tex.Apply(false);
    }

    private void OnDestroy() {
        if (_tex != null) {
            Destroy(_tex);
        }
    }
}
