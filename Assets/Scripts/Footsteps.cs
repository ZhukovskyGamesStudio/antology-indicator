using UnityEngine;

/// <summary>
/// Шаги игрока. Пока игрок движется — с интервалом проигрывает звук шага:
/// на обычном полу иногда скрипит (<see cref="creakClips"/>), в луже — мокрые шаги
/// (<see cref="wetClips"/>). О входе/выходе из лужи сообщает <see cref="PuddleZone"/>.
/// Висит на префабе игрока (FirstPersonController).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Footsteps : MonoBehaviour {
    [Tooltip("Источник шагов. Если пусто — берётся с этого объекта")]
    public AudioSource source;

    [Tooltip("Скрипы пола (floor_creak) — случайный из набора")]
    public AudioClip[] creakClips;

    [Tooltip("Мокрые шаги по луже (floor_wet) — случайный из набора")]
    public AudioClip[] wetClips;

    [Tooltip("Вероятность скрипа на обычном шаге (0..1) — 'иногда скрипит пол'")]
    [Range(0f, 1f)] public float creakChance = 0.25f;

    [Tooltip("Интервал между шагами при ходьбе, сек")]
    public float stepInterval = 0.45f;

    [Tooltip("Порог горизонтальной скорости, выше которого игрок считается идущим")]
    public float minSpeed = 0.6f;

    [Tooltip("Разброс питча для разнообразия шагов")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Tooltip("Скрип только на дереве: материалы с этими подстроками в имени считаются " +
             "плиткой (ванная/кухня) и НЕ скрипят")]
    public string[] noCreakMaterials = { "bathroom", "tile", "plitka" };

    [Tooltip("Дальность луча вниз для определения материала пола под ногами")]
    public float surfaceCheckDistance = 3f;

    private Rigidbody _rb;
    private float _timer;
    private int _puddleCount;

    private void Awake() {
        _rb = GetComponent<Rigidbody>();
        if (source == null) {
            source = GetComponent<AudioSource>();
        }
    }

    /// <summary>Игрок вошёл в лужу (вызывает <see cref="PuddleZone"/>).</summary>
    public void EnterPuddle() {
        _puddleCount++;
    }

    /// <summary>Игрок вышел из лужи (вызывает <see cref="PuddleZone"/>).</summary>
    public void ExitPuddle() {
        _puddleCount = Mathf.Max(0, _puddleCount - 1);
    }

    private void Update() {
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;

        if (v.magnitude < minSpeed) {
            // Стоим — следующий шаг проиграется сразу, как только пойдём.
            _timer = 0f;
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer > 0f) {
            return;
        }

        _timer = stepInterval;
        Step();
    }

    private void Step() {
        if (_puddleCount > 0) {
            Play(wetClips);
        } else if (creakClips != null && creakClips.Length > 0 && Random.value < creakChance && OnCreakableFloor()) {
            Play(creakClips);
        }
    }

    /// <summary>Под ногами дерево (скрипит), а не плитка ванной/кухни.</summary>
    private bool OnCreakableFloor() {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                surfaceCheckDistance, ~0, QueryTriggerInteraction.Ignore)) {
            return false; // в воздухе — не скрипим
        }

        Renderer r = hit.collider.GetComponent<Renderer>();
        if (r == null || r.sharedMaterial == null) {
            return true; // материал неизвестен — считаем обычным полом
        }

        string mat = r.sharedMaterial.name.ToLower();
        for (int i = 0; i < noCreakMaterials.Length; i++) {
            string kw = noCreakMaterials[i];
            if (!string.IsNullOrEmpty(kw) && mat.Contains(kw.ToLower())) {
                return false; // плитка — не скрипим
            }
        }

        return true;
    }

    private void Play(AudioClip[] clips) {
        if (clips == null || clips.Length == 0) {
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) {
            return;
        }

        if (source != null) {
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clip);
        } else {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}
