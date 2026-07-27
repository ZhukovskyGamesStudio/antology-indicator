using UnityEngine;

/// <summary>
/// Живая камера: дыхание, качание, крен при стрейфе и сердцебиение.
///
/// Без этого камера — штатив: пока игрок не жмёт клавиши, кадр стоит абсолютно
/// мёртво, и никакой пост-обработкой это не лечится. Достаточно десятых долей
/// градуса, чтобы кадр начал «дышать»; всё, что заметно глазом как движение, —
/// уже перебор и укачивает.
///
/// Куда пишем: <see cref="SwayNode"/> — это Joint, родитель CameraAnimAnchor.
/// Его локальный ПОВОРОТ не трогает никто:
///   • FirstPersonController пишет joint.localPosition (headbob) и
///     PlayerCamera.localEulerAngles (питч),
///   • клип Sneeze анимирует CameraAnimAnchor,
///   • HUD.AsyncDeath доворачивает саму камеру.
/// Поэтому мы ни с кем не деремся и не трогаем иерархию.
/// </summary>
[DisallowMultipleComponent]
public class CameraLife : MonoBehaviour {
    [Tooltip("Узел, которому крутим локальный поворот. Обычно Joint. Пусто — свой transform.")]
    public Transform SwayNode;

    [Tooltip("Нужен, чтобы гасить эффект в катсценах и читать ввод для крена")]
    public FirstPersonController Controller;

    [Header("Дыхание")]
    public bool IsBreathing = true;

    [Range(0f, 2f)]
    [Tooltip("Амплитуда дыхания, градусы. 0.2–0.3 — предел, где это ещё не читается как движение")]
    public float BreathAmplitude = 0.22f;

    [Tooltip("Вдохов в секунду. 0.25 ≈ 15 вдохов в минуту — спокойное дыхание")]
    public float BreathSpeed = 0.25f;

    [Header("Качание (растёт с безумием)")]
    public bool IsSway = true;

    [Range(0f, 3f)]
    public float CalmSway = 0.16f;

    [Range(0f, 6f)]
    public float MadSway = 1.15f;

    public float CalmSwaySpeed = 0.12f;
    public float MadSwaySpeed = 0.5f;

    [Header("Крен при ходьбе вбок")]
    public bool IsStrafeRoll = true;

    [Range(0f, 6f)]
    [Tooltip("На сколько градусов заваливается кадр при движении вбок")]
    public float StrafeRoll = 1.3f;

    [Tooltip("Скорость набора/сброса крена (1/сек)")]
    public float RollResponse = 4.5f;

    [Header("Сердцебиение")]
    public bool IsHeartbeat = true;

    [Range(0f, 1f)]
    [Tooltip("С какой доли безумия сердце начинает быть слышно в кадре")]
    public float HeartbeatFrom = 0.4f;

    [Tooltip("Ударов в минуту в начале порога")]
    public float CalmBpm = 64f;

    [Tooltip("Ударов в минуту при полном безумии")]
    public float MadBpm = 130f;

    [Range(0f, 3f)]
    [Tooltip("Толчок кадра на удар, градусы")]
    public float HeartbeatKick = 0.55f;

    [Header("Затухание")]
    [Range(0f, 1f)]
    [Tooltip("Во сколько раз слабее эффект, пока предмет в руках: игрок разглядывает вещь, кадр должен успокоиться")]
    public float HoldDamping = 0.3f;

    [Tooltip("Скорость смены веса эффекта (1/сек)")]
    public float WeightResponse = 3f;

    private float _weight = 1f;
    private float _roll;
    private float _beatPhase;
    private float _seed;

    private void Awake() {
        if (SwayNode == null) {
            SwayNode = transform;
        }

        _seed = Random.value * 500f;
    }

    private void LateUpdate() {
        if (SwayNode == null) {
            return;
        }

        float madness = 0f;
        MadnessManager manager = MadnessManager.instance;

        if (manager != null && manager.MaxMadness > 0f) {
            madness = Mathf.Clamp01(manager.Madness / manager.MaxMadness);
        }

        // Катсцены (смерть, чиханье, финал) сами доворачивают камеру — в это время
        // нас в кадре быть не должно, иначе выравнивание горизонта дёргается.
        float targetWeight = 1f;

        if (Controller != null && !Controller.cameraCanMove) {
            targetWeight = 0f;
        } else if (FirstPersonController.isHolding) {
            targetWeight = HoldDamping;
        }

        _weight = Mathf.Lerp(_weight, targetWeight, 1f - Mathf.Exp(-WeightResponse * Mathf.Min(Time.deltaTime, 0.1f)));

        float pitch = 0f;
        float yaw = 0f;
        float roll = 0f;

        if (IsBreathing) {
            // Вдох/выдох: питч по синусу, крен вдвое слабее и вдвое медленнее —
            // так дыхание не читается как ровный маятник.
            float t = Time.time * BreathSpeed * Mathf.PI * 2f;
            pitch += Mathf.Sin(t) * BreathAmplitude;
            roll += Mathf.Sin(t * 0.5f + 1.1f) * BreathAmplitude * 0.45f;
        }

        if (IsSway) {
            float amplitude = Mathf.Lerp(CalmSway, MadSway, madness);
            float speed = Mathf.Lerp(CalmSwaySpeed, MadSwaySpeed, madness);
            float t = Time.time * speed;

            // Перлин, а не синус: синус — это метроном, его глаз ловит за пару секунд.
            pitch += (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * amplitude;
            yaw += (Mathf.PerlinNoise(_seed + 61f, t) * 2f - 1f) * amplitude * 1.2f;
            roll += (Mathf.PerlinNoise(_seed + 131f, t * 0.7f) * 2f - 1f) * amplitude * 0.8f;
        }

        if (IsStrafeRoll && Controller != null) {
            float strafe = Input.GetAxis("Horizontal");

            if (!Controller.playerCanMove || FirstPersonController.isHolding) {
                strafe = 0f;
            }

            _roll = Mathf.Lerp(_roll, -strafe * StrafeRoll, 1f - Mathf.Exp(-RollResponse * Mathf.Min(Time.deltaTime, 0.1f)));
            roll += _roll;
        }

        if (IsHeartbeat && madness > HeartbeatFrom) {
            // Ниже порога сердце не считаем вовсе, иначе первый удар прилетает
            // рывком в момент пересечения порога.
            float over = Mathf.InverseLerp(HeartbeatFrom, 1f, madness);
            float bpm = Mathf.Lerp(CalmBpm, MadBpm, over);
            _beatPhase += bpm / 60f * Time.deltaTime;
            _beatPhase -= Mathf.Floor(_beatPhase);

            // Два удара: «тук-тук», второй слабее и чуть позже.
            float pulse = Thump(_beatPhase) + Thump(_beatPhase - 0.17f) * 0.55f;
            float kick = pulse * HeartbeatKick * over;

            pitch -= kick;
            roll += kick * 0.35f;
        }

        SwayNode.localRotation = Quaternion.Euler(pitch * _weight, yaw * _weight, roll * _weight);
    }

    /// Короткий затухающий толчок в начале доли.
    private static float Thump(float phase) {
        if (phase < 0f) {
            phase += 1f;
        }

        return Mathf.Exp(-phase * 22f);
    }
}
