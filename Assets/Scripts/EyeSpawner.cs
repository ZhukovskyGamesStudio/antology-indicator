using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Подсаживает моргающий глаз в тёмные углы вокруг игрока, когда безумие
/// переваливает за порог. Точки заранее по сцене не расставлены: кандидаты
/// набрасываются рейкастами из камеры и проверяются на реальную освещённость
/// (свет + перекрытия), так что глаз лезет только туда, где темно.
/// </summary>
public class EyeSpawner : MonoBehaviour {
    [Header("Кадры моргания: открыт → прищур → закрыт")]
    public Sprite[] BlinkFrames;

    [Tooltip("Материал спрайта. Пусто — берётся дефолтный (unlit), глаз будет одинаково виден в темноте")]
    public Material EyeMaterial;

    [Tooltip("Цвет глаза. Альфа задаёт максимальную видимость")]
    public Color Tint = new(1f, 1f, 1f, 0.85f);

    [Tooltip("Высота глаза в метрах")]
    public float EyeWorldSize = 0.22f;

    [Tooltip("Камера игрока. Пусто — Camera.main")]
    public Camera Cam;

    [Header("Когда появляется")]
    [Range(0f, 1f)]
    [Tooltip("С какого процента безумия глаза вообще начинают появляться")]
    public float MinMadnessPercent = 0.25f;

    [Tooltip("Пауза между попытками на пороге безумия (сек)")]
    public float IntervalCalm = 14f;

    [Tooltip("Пауза между попытками при полном безумии (сек)")]
    public float IntervalPeak = 5f;

    [Tooltip("Сколько точек пробуем за одну попытку")]
    public int AttemptsPerTry = 30;

    [Header("Где появляется")]
    public float MinDistance = 1.5f;
    public float MaxDistance = 8f;

    [Tooltip("Угол от центра экрана (градусы): глаз лезет в периферию, а не в упор перед носом")]
    public Vector2 AngleRange = new(16f, 40f);

    [Tooltip("Отступ от поверхности, чтобы спрайт не резался стеной")]
    public float SurfaceOffset = 0.06f;

    public LayerMask SurfaceMask = ~0;

    [Tooltip("Не сажать глаз на пол и потолок: 0 — только строго вертикальные стены, 1 — куда угодно")]
    [Range(0f, 1f)]
    public float MaxUpDot = 0.6f;

    [Tooltip("Требовать, чтобы точка попала в кадр (иначе глаз может появиться за спиной)")]
    public bool OnlyInView = true;

    [Header("Темнота")]
    [Tooltip("Максимальная расчётная освещённость точки, при которой она ещё считается тёмной")]
    public float DarknessThreshold = 0.08f;

    [Tooltip("Учитывать Rendering Layers света (в проекте свет разделён по комнатам)")]
    public bool RespectLightLayers = true;

    [Header("Поведение")]
    public float FadeInTime = 0.7f;
    public float FadeOutTime = 0.5f;

    [Tooltip("Сколько раз моргнуть за появление")]
    public Vector2Int BlinkCountRange = new(2, 4);

    [Tooltip("Длительность одного кадра моргания")]
    public float BlinkFrameTime = 0.06f;

    [Tooltip("Пауза между морганиями")]
    public Vector2 BlinkPauseRange = new(0.5f, 1.6f);

    /// <summary>Ниже этой паузы попытки не учащаются — страховка от нуля в инспекторе.</summary>
    private const float MinInterval = 0.5f;

    private SpriteRenderer _eye;
    private Light[] _lights = Array.Empty<Light>();
    private readonly RaycastHit[] _hits = new RaycastHit[1];

    private void Start() {
        if (!HasValidFrames()) {
            enabled = false;
            return;
        }

        CreateEye();
        Run(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void LateUpdate() {
        if (_eye != null && _eye.gameObject.activeSelf) {
            FaceCamera();
        }
    }

    private bool HasValidFrames() {
        if (BlinkFrames == null || BlinkFrames.Length == 0) {
            Debug.LogError("[EyeSpawner] Не заданы кадры моргания", this);
            return false;
        }

        for (int i = 0; i < BlinkFrames.Length; i++) {
            if (BlinkFrames[i] == null) {
                Debug.LogError($"[EyeSpawner] Кадр {i} пустой — глаз будет исчезать на этом кадре", this);
                return false;
            }
        }

        return true;
    }

    private void CreateEye() {
        GameObject go = new("Eye");
        go.transform.SetParent(transform, false);
        _eye = go.AddComponent<SpriteRenderer>();
        _eye.sprite = BlinkFrames[0];
        _eye.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _eye.receiveShadows = false;

        if (EyeMaterial != null) {
            _eye.sharedMaterial = EyeMaterial;
        }

        float spriteHeight = _eye.sprite.bounds.size.y;
        go.transform.localScale = Vector3.one * (spriteHeight > 0f ? EyeWorldSize / spriteHeight : 1f);
        SetAlpha(0f);
        go.SetActive(false);
    }

    private async UniTaskVoid Run(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            await UniTask.Delay(Mathf.RoundToInt(NextInterval() * 1000f), cancellationToken: token);

            // Выключенный в инспекторе компонент должен быть настоящим выключателем:
            // UniTask живёт своей жизнью и сам по себе про enabled ничего не знает.
            if (!isActiveAndEnabled || MadnessPercent() < MinMadnessPercent) {
                continue;
            }

            try {
                if (TryFindDarkSpot(out Vector3 point)) {
                    await ShowEye(point, token);
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                // Иначе одна случайная ошибка молча убивает цикл до конца игры.
                Debug.LogException(e, this);
                HideEye();
            }
        }
    }

    /// <summary>Чем больше безумия, тем чаще пробуем.</summary>
    private float NextInterval() {
        float t = Mathf.InverseLerp(MinMadnessPercent, 1f, MadnessPercent());
        return Mathf.Max(MinInterval, Mathf.Lerp(IntervalCalm, IntervalPeak, t));
    }

    private static float MadnessPercent() {
        MadnessManager madness = MadnessManager.instance;
        if (madness == null || madness.MaxMadness <= 0f) {
            return 0f;
        }

        return Mathf.Clamp01(madness.Madness / madness.MaxMadness);
    }

    private Camera ActiveCamera() {
        return Cam != null && Cam.isActiveAndEnabled ? Cam : Camera.main;
    }

    /// <summary>Набрасывает случайные точки в периферии взгляда и возвращает первую достаточно тёмную.</summary>
    public bool TryFindDarkSpot(out Vector3 point) {
        point = Vector3.zero;
        Camera cam = ActiveCamera();
        if (cam == null) {
            return false;
        }

        RefreshLights();

        for (int i = 0; i < AttemptsPerTry; i++) {
            if (!Physics.Raycast(cam.transform.position, RandomPeripheralDirection(cam), out RaycastHit hit,
                    MaxDistance, SurfaceMask, QueryTriggerInteraction.Ignore)) {
                continue;
            }

            if (hit.distance < MinDistance || Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > MaxUpDot) {
                continue;
            }

            Vector3 candidate = hit.point + hit.normal * SurfaceOffset;

            if (OnlyInView && !IsInView(cam, candidate)) {
                continue;
            }

            if (EstimateLight(candidate, hit.normal, hit.collider) > DarknessThreshold) {
                continue;
            }

            point = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Конус вокруг взгляда с уклоном влево/вправо — «краем глаза», а не над головой.</summary>
    private Vector3 RandomPeripheralDirection(Camera cam) {
        Transform camT = cam.transform;
        float angle = Random.Range(AngleRange.x, AngleRange.y);
        float roll = Random.Range(-70f, 70f) + (Random.value < 0.5f ? 0f : 180f);
        return Quaternion.AngleAxis(roll, camT.forward) * (Quaternion.AngleAxis(angle, camT.up) * camT.forward);
    }

    private static bool IsInView(Camera cam, Vector3 point) {
        Vector3 viewport = cam.WorldToViewportPoint(point);
        return viewport.z > 0f
            && viewport.x > 0.04f && viewport.x < 0.96f
            && viewport.y > 0.04f && viewport.y < 0.96f;
    }

    private void RefreshLights() {
        // Комнаты в игре подменяются (NormalRooms ↔ LabirintRooms), поэтому список
        // источников пересобираем перед каждой попыткой, а не кэшируем навсегда.
        _lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    /// <summary>Грубая оценка освещённости точки: сумма вкладов видимых источников.</summary>
    public float EstimateLight(Vector3 point, Vector3 normal, Collider surface) {
        uint surfaceLayers = SurfaceRenderingLayers(surface);

        float sum = 0f;
        for (int i = 0; i < _lights.Length; i++) {
            sum += LightContribution(_lights[i], point, normal, surfaceLayers);
        }

        return sum;
    }

    private uint SurfaceRenderingLayers(Collider surface) {
        if (!RespectLightLayers || surface == null) {
            return uint.MaxValue;
        }

        // Коллайдер и рендерер не всегда на одном объекте — ищем ближайший вверх по иерархии.
        Renderer rend = surface.GetComponent<Renderer>();
        if (rend == null) {
            rend = surface.GetComponentInParent<Renderer>();
        }

        return rend != null ? rend.renderingLayerMask : uint.MaxValue;
    }

    private float LightContribution(Light light, Vector3 point, Vector3 normal, uint surfaceLayers) {
        if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f) {
            return 0f;
        }

        if (RespectLightLayers && (light.renderingLayerMask & surfaceLayers) == 0) {
            return 0f;
        }

        // Свет без теней в URP светит сквозь стены — значит, и перекрытия проверять нельзя.
        bool castsShadows = light.shadows != LightShadows.None;

        if (light.type == LightType.Directional) {
            float ndotlDir = Mathf.Max(0f, Vector3.Dot(normal, -light.transform.forward));
            if (ndotlDir <= 0f) {
                return 0f;
            }

            if (castsShadows && Physics.Raycast(point, -light.transform.forward, 50f, SurfaceMask, QueryTriggerInteraction.Ignore)) {
                return 0f;
            }

            return light.intensity * ndotlDir;
        }

        Vector3 toLight = light.transform.position - point;
        float distance = toLight.magnitude;
        if (distance > light.range || distance <= 0.001f) {
            return 0f;
        }

        Vector3 lightDir = toLight / distance;
        float ndotl = Mathf.Max(0f, Vector3.Dot(normal, lightDir));
        if (ndotl <= 0f) {
            return 0f;
        }

        if (light.type == LightType.Spot && Vector3.Angle(light.transform.forward, -lightDir) > light.spotAngle * 0.5f) {
            return 0f;
        }

        // Стена между точкой и лампой — значит, свет сюда не доходит.
        if (castsShadows && Physics.RaycastNonAlloc(point, lightDir, _hits, distance - 0.05f, SurfaceMask, QueryTriggerInteraction.Ignore) > 0) {
            return 0f;
        }

        // Приближение затухания Unity: к границе range вклад падает почти в ноль.
        float attenuation = 1f / (1f + 25f * distance * distance / (light.range * light.range));
        return light.intensity * attenuation * ndotl;
    }

    private async UniTask ShowEye(Vector3 point, CancellationToken token) {
        _eye.transform.position = point;
        _eye.sprite = BlinkFrames[0];
        SetAlpha(0f);
        _eye.gameObject.SetActive(true);
        FaceCamera();

        await Fade(0f, Tint.a, FadeInTime, token);

        int blinks = Random.Range(BlinkCountRange.x, BlinkCountRange.y + 1);
        for (int i = 0; i < blinks; i++) {
            await UniTask.Delay(Mathf.RoundToInt(Random.Range(BlinkPauseRange.x, BlinkPauseRange.y) * 1000f), cancellationToken: token);
            await Blink(token);
        }

        await Fade(Tint.a, 0f, FadeOutTime, token);
        HideEye();
    }

    /// <summary>Один цикл: открыт → прищур → закрыт → прищур → открыт.</summary>
    private async UniTask Blink(CancellationToken token) {
        for (int i = 1; i < BlinkFrames.Length; i++) {
            await ShowFrame(i, token);
        }

        for (int i = BlinkFrames.Length - 2; i >= 0; i--) {
            await ShowFrame(i, token);
        }
    }

    private async UniTask ShowFrame(int index, CancellationToken token) {
        _eye.sprite = BlinkFrames[index];
        await UniTask.Delay(Mathf.RoundToInt(BlinkFrameTime * 1000f), cancellationToken: token);
    }

    private async UniTask Fade(float from, float to, float duration, CancellationToken token) {
        float time = 0f;
        while (time < duration) {
            time += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, time / duration));
            FaceCamera();
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        SetAlpha(to);
    }

    private void HideEye() {
        if (_eye != null) {
            _eye.gameObject.SetActive(false);
        }
    }

    private void FaceCamera() {
        Camera cam = ActiveCamera();
        if (cam == null) {
            return;
        }

        _eye.transform.rotation = Quaternion.LookRotation(_eye.transform.position - cam.transform.position, Vector3.up);
    }

    private void SetAlpha(float alpha) {
        Color color = Tint;
        color.a = alpha;
        _eye.color = color;
    }
}
