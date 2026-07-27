using UnityEngine;

/// <summary>
/// Еле заметное «дыхание» объекта: поворот вокруг точки подвеса и/или снос на
/// пару сантиметров.
///
/// Главный адресат — не сам объект, а тени. Весь свет в сцене рантайм-прямой,
/// поэтому если сдвинуть источник на 3–4 см, тени от мебели и плафона поползут
/// по стенам. Комната перестаёт быть фотографией, хотя ничего заметного в кадре
/// не происходит.
///
/// Два режима, потому что случаи разные:
///  • ПОВОРОТ вокруг <see cref="PivotOffset"/> — для того, что игрок видит:
///    шторы, подвешенные предметы.
///  • СНОС (<see cref="CalmShift"/>) — для источников света. У точечной лампы
///    поворот не значит ничего, значение имеет только позиция; а плафон люстры
///    в этой квартире вообще прижат к потолку и качаться не может, так что
///    двигать надо свет, а не светильник.
///
/// Снос задаётся в МЕТРАХ и в мировом масштабе: у люстры родитель ужат до 0.143,
/// и «полградуса» там означало бы три миллиметра.
/// </summary>
public class IdleSway : MonoBehaviour {
    [Header("Поворот вокруг подвеса")]
    [Tooltip("Смещение до точки подвеса в локальных осях объекта (единицы родителя). " +
             "Ноль — объект крутится вокруг собственного пивота")]
    public Vector3 PivotOffset = Vector3.zero;

    [Tooltip("Амплитуда поворота в спокойном состоянии, градусы")]
    public Vector3 CalmAngles = Vector3.zero;

    [Tooltip("Амплитуда поворота при полном безумии, градусы")]
    public Vector3 MadAngles = Vector3.zero;

    [Header("Снос, метры (мировой масштаб)")]
    [Tooltip("На сколько метров объект гуляет в спокойном состоянии")]
    public Vector3 CalmShift = Vector3.zero;

    [Tooltip("На сколько метров объект гуляет при полном безумии")]
    public Vector3 MadShift = Vector3.zero;

    [Header("Скорость")]
    public float CalmSpeed = 0.14f;
    public float MadSpeed = 0.46f;

    [Header("Реакция на безумие")]
    [Tooltip("Выключи, если объект должен вести себя одинаково всю игру")]
    public bool FollowMadness = true;

    private Vector3 _startPos;
    private Quaternion _startRot;
    private float _seed;

    private void Awake() {
        _startPos = transform.localPosition;
        _startRot = transform.localRotation;
        _seed = Random.value * 400f;
    }

    private void LateUpdate() {
        float madness = 0f;

        if (FollowMadness) {
            MadnessManager manager = MadnessManager.instance;

            if (manager != null && manager.MaxMadness > 0f) {
                madness = Mathf.Clamp01(manager.Madness / manager.MaxMadness);
            }
        }

        float speed = Mathf.Lerp(CalmSpeed, MadSpeed, madness);
        float t = Time.time * speed;

        // Разные сдвиги сида и разные множители частоты по осям — иначе объект
        // ходит по прямой туда-сюда, а это читается как анимация, а не как жизнь.
        Vector3 noise = new Vector3(
            Mathf.PerlinNoise(_seed, t) * 2f - 1f,
            Mathf.PerlinNoise(_seed + 53f, t * 0.63f) * 2f - 1f,
            Mathf.PerlinNoise(_seed + 97f, t * 1.31f) * 2f - 1f);

        Vector3 angles = Vector3.Scale(noise, Vector3.Lerp(CalmAngles, MadAngles, madness));
        Quaternion rotation = _startRot * Quaternion.Euler(angles);
        transform.localRotation = rotation;

        Vector3 position = _startPos;

        if (PivotOffset != Vector3.zero) {
            // Держим точку подвеса на месте: без этого объект не качается,
            // а ездит вбок вместе с креплением.
            position += _startRot * PivotOffset - rotation * PivotOffset;
        }

        Vector3 shift = Vector3.Scale(noise, Vector3.Lerp(CalmShift, MadShift, madness));

        if (shift != Vector3.zero) {
            position += WorldToLocalScale(shift);
        }

        transform.localPosition = position;
    }

    /// Снос задан в метрах, а localPosition живёт в единицах родителя.
    private Vector3 WorldToLocalScale(Vector3 worldShift) {
        Vector3 scale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;

        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 0f : worldShift.x / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 0f : worldShift.y / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 0f : worldShift.z / scale.z);
    }

    private void OnDrawGizmosSelected() {
        if (PivotOffset == Vector3.zero) {
            return;
        }

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Vector3 pivot = transform.TransformPoint(PivotOffset);
        Gizmos.DrawLine(transform.position, pivot);
        Gizmos.DrawWireSphere(pivot, 0.04f);
    }
}
