using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранные ТВ-помехи (белый шум). Включаются в финале, когда диалог
/// «обрывается»: вся картинка превращается в шум, а заголовок и кнопка выхода
/// остаются поверх (они выше по иерархии Canvas). Текстура шума перегенерируется
/// из случайных пикселей несколько раз в секунду.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ScreenStatic : MonoBehaviour {
    [Tooltip("Разрешение шумовой текстуры. Меньше — крупнее зерно и дешевле")]
    public int resolution = 320;

    [Tooltip("Частота обновления шума в секунду (0 — каждый кадр)")]
    public float updatesPerSecond = 20f;

    [Tooltip("Доля цветных пикселей (0 — чисто чёрно-белый шум)")]
    [Range(0f, 1f)]
    public float colorAmount = 0.05f;

    private RawImage _img;
    private Texture2D _tex;
    private Color32[] _buf;
    private float _lastUpdate;

    private void Awake() {
        _img = GetComponent<RawImage>();
        _tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _buf = new Color32[resolution * resolution];
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

    /// <summary>Проявить помехи, плавно подняв альфу за <paramref name="duration"/> секунд.</summary>
    public async UniTask Show(float duration) {
        gameObject.SetActive(true);
        Color c = _img.color;
        c.a = 0f;
        _img.color = c;
        await _img.DOFade(1f, duration).SetUpdate(true)
            .WithCancellation(this.GetCancellationTokenOnDestroy());
    }

    private void Regenerate() {
        for (int i = 0; i < _buf.Length; i++) {
            if (colorAmount > 0f && Random.value < colorAmount) {
                _buf[i] = new Color32(
                    (byte)Random.Range(0, 256),
                    (byte)Random.Range(0, 256),
                    (byte)Random.Range(0, 256), 255);
            } else {
                byte v = (byte)Random.Range(0, 256);
                _buf[i] = new Color32(v, v, v, 255);
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
