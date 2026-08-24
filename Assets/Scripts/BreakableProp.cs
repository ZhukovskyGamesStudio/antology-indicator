using DG.Tweening;
using UnityEngine;

/// <summary>
/// Декоративный предмет-картинка, который можно разбить молотком (ЛКМ, когда молоток
/// в руке). Живёт рядом с HittableObj (Hp = 1): на смертельном ударе подменяет спрайт
/// на сломанный, коротко пружинит масштабом и стучит молотком; звон самой поломки
/// (стекло) играет HittableObj.deathClips — тот же клип, что у банки перца.
/// </summary>
[RequireComponent(typeof(HittableObj))]
public class BreakableProp : MonoBehaviour {
    [Tooltip("Спрайт сломанного состояния (подменяется на месте целого)")]
    public Sprite brokenSprite;

    [Tooltip("Стук молотка в момент поломки. Звон стекла — в HittableObj.deathClips")]
    public AudioClip[] hammerClips;

    [Header("Пружинка масштаба")]
    public float punchScale = 0.14f;
    public float punchDuration = 0.35f;

    private HittableObj _hittable;
    private SpriteRenderer _renderer;
    private bool _broken;

    private void Awake() {
        _hittable = GetComponent<HittableObj>();
        _renderer = GetComponent<SpriteRenderer>();
        _hittable.OnDeath.AddListener(Break);
    }

    /// <summary>
    /// Коллайдер под прицел создаём сами и только в игровой сцене (признак —
    /// MadnessManager): те же комнаты стоят декорацией в главном меню, и лишняя
    /// коробка там перехватывала бы клики по книгам. Start, а не Awake — порядок
    /// Awake-ов не гарантирован, а к Start синглтоны уже расставлены.
    /// Слой игрока исключён, чтобы коробка не толкала капсулу у стены
    /// (рейкасты excludeLayers не читают, так что прицел и удар работают).
    /// </summary>
    private void Start() {
        if (MadnessManager.instance == null || _renderer == null || _renderer.sprite == null) {
            return;
        }

        if (GetComponent<Collider>() != null) {
            return;
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        Bounds b = _renderer.sprite.bounds;
        box.center = b.center;
        box.size = new Vector3(b.size.x, b.size.y, 0.05f);
        box.excludeLayers = LayerMask.GetMask("Default");
    }

    public void Break() {
        if (_broken) {
            return;
        }

        _broken = true;
        // Осколки больше не мишень: прицел не должен предлагать бить их снова.
        _hittable.enabled = false;

        if (_renderer != null && brokenSprite != null) {
            _renderer.sprite = brokenSprite;
        }

        SoundUtil.PlayRandom(null, hammerClips, transform.position);
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.6f);
    }

    private void OnDestroy() {
        transform.DOKill();
    }
}
