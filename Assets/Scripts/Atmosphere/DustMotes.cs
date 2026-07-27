using UnityEngine;

/// <summary>
/// Пыль в воздухе вокруг игрока.
///
/// В квартире, где никто не убирается, воздух не должен быть вакуумом. Пылинки
/// дают две вещи разом: воздух между камерой и стеной перестаёт быть пустотой,
/// и появляется видимая разница между освещённым объёмом и тёмным — пыль в
/// конусе лампы вспыхивает, в углу её просто нет.
///
/// Система симулируется в мировом пространстве, а за игроком едет только область
/// эмиссии: уже выпущенные пылинки остаются висеть в комнате, поэтому облако не
/// «приклеено» к камере и не едет вместе с поворотом головы.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class DustMotes : MonoBehaviour {
    [Tooltip("За кем едет область эмиссии. Пусто — берётся Camera.main")]
    public Transform Follow;

    [Tooltip("Скорость подтягивания к игроку (1/сек). Медленно — облако отстаёт и тянется шлейфом")]
    public float FollowSpeed = 2.5f;

    [Tooltip("Сдвиг области эмиссии относительно игрока: чуть вперёд по взгляду, чтобы пыль была там, куда смотришь")]
    public Vector3 Offset = new Vector3(0f, 0.35f, 0f);

    [Header("Реакция на безумие")]
    [Tooltip("Множитель плотности при нулевом безумии")]
    public float CalmRate = 1f;

    [Tooltip("Множитель плотности при полном безумии — воздух густеет")]
    public float MadRate = 2.4f;

    [Tooltip("Множитель турбулентности при нулевом безумии")]
    public float CalmTurbulence = 1f;

    [Tooltip("Множитель турбулентности при полном безумии — пыль начинает метаться")]
    public float MadTurbulence = 3.2f;

    [Header("Пока игрок сидит за столом")]
    [Tooltip("Кто решает, встал игрок или нет. Пусто — ищется на сцене")]
    public FirstPersonController Controller;

    [Range(0f, 1f)]
    [Tooltip("Множитель плотности, пока игрок не может ходить. В туториале камера утыкается " +
             "в стол, пыль оказывается прямо перед носом и лезет в текст книги")]
    public float SeatedRate = 0.2f;

    [Tooltip("Скорость, с которой пыль набирается после того, как игрок встал (1/сек). " +
             "Медленно — потому что заметное появление пыли читается как баг")]
    public float SeatedBlend = 0.35f;

    [Tooltip("Сколько секунд симуляции проиграть на старте, чтобы пыль уже висела в воздухе")]
    public float PrimeSeconds = 20f;

    private ParticleSystem _system;
    private float _baseRate;
    private float _baseNoise;
    private bool _hasNoise;
    private float _seated = 1f;
    private bool _primed;

    private void Awake() {
        _system = GetComponent<ParticleSystem>();

        ParticleSystem.EmissionModule emission = _system.emission;
        _baseRate = emission.rateOverTime.constant;

        ParticleSystem.NoiseModule noise = _system.noise;
        _hasNoise = noise.enabled;
        _baseNoise = noise.strength.constant;

        if (Controller == null) {
            Controller = FindFirstObjectByType<FirstPersonController>();
        }

        if (Follow == null && Camera.main != null) {
            Follow = Camera.main.transform;
        }

        if (Follow != null) {
            transform.position = Follow.position + Offset;
        }
    }

    private void LateUpdate() {
        if (Follow == null) {
            if (Camera.main == null) {
                return;
            }

            Follow = Camera.main.transform;
        }

        Vector3 target = Follow.position + Offset;
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-FollowSpeed * Mathf.Min(Time.deltaTime, 0.1f)));

        float madness = 0f;
        MadnessManager manager = MadnessManager.instance;

        if (manager != null && manager.MaxMadness > 0f) {
            madness = Mathf.Clamp01(manager.Madness / manager.MaxMadness);
        }

        // Пока игрок сидит за столом, камера утыкается в книгу, и пыль оказывается
        // прямо перед носом. Ждём, пока встанет.
        float seatedTarget = Controller == null || Controller.playerCanMove ? 1f : SeatedRate;

        if (!_primed) {
            // Первый кадр — сюда мы попадаем уже после всех Start(), то есть после того,
            // как StoryManager запретил движение. Раньше читать playerCanMove нельзя:
            // порядок Start-ов не гарантирован, и на старте мы увидели бы «игрок ходит».
            _primed = true;
            _seated = seatedTarget;
            ApplyRate(madness);
            // Префарм самого ParticleSystem считает по полной плотности, поэтому
            // пересобираем облако вручную — уже с нужной.
            _system.Clear(true);
            _system.Simulate(PrimeSeconds, true, true);
            _system.Play();
        } else {
            _seated = Mathf.Lerp(_seated, seatedTarget, 1f - Mathf.Exp(-SeatedBlend * Mathf.Min(Time.deltaTime, 0.1f)));
        }

        ApplyRate(madness);

        if (_hasNoise) {
            ParticleSystem.NoiseModule noise = _system.noise;
            noise.strength = _baseNoise * Mathf.Lerp(CalmTurbulence, MadTurbulence, madness);
        }
    }

    private void ApplyRate(float madness) {
        ParticleSystem.EmissionModule emission = _system.emission;
        emission.rateOverTime = _baseRate * Mathf.Lerp(CalmRate, MadRate, madness) * _seated;
    }
}
