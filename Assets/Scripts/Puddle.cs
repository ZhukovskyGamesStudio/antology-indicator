using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Лужа, натёкшая из ванной. Лужи не появляются разом: в ванной забыли закрыть
/// кран, и вода растекается по гостиной постепенно — по очереди, по номеру
/// <see cref="Order"/>, с интервалом между ними (см. StoryManager.PuddleInterval).
/// Каждая вырастает скейлом из нуля, поэтому появление видно боковым зрением и
/// ведёт игрока к источнику. Высыхают все разом, когда игрок перекрывает кран
/// (<see cref="RunningWater"/>).
///
/// Сам GameObject лужи никто не включает и не выключает — он живёт вместе с
/// комнатой, а «есть вода или нет» — это масштаб. Пока лужа не натекла, её
/// масштаб нулевой, а триггер мокрых шагов выключен: наступать не на что.
///
/// Активные лужи держат себя в общем статическом списке, поэтому ни крану, ни
/// сюжету не нужны ссылки на них. Это не украшательство, а единственный рабочий
/// способ: лужи лежат в livingroom, а кран — внутри вложенного префаба
/// watercloset, и сослаться из одного на другое в инспекторе нельзя. Луж при
/// этом может быть сколько угодно и в скольких угодно копиях комнаты — добавил
/// лужу в комнату, проставил ей номер, и всё работает.
///
/// Про мокрые шаги знает не лужа, а <see cref="PuddleZone"/> на дочернем триггере.
/// </summary>
[DisallowMultipleComponent]
public class Puddle : MonoBehaviour {
    [Tooltip("Порядок появления: 1 натекает первой, 2 — через интервал после неё и т.д. " +
             "Лужи с одинаковым номером появляются одновременно — так синхронно " +
             "натекает вода во всех копиях гостиной в лабиринте")]
    public int Order = 1;

    [Tooltip("Сколько секунд лужа растекается из нуля в свой размер")]
    public float AppearDuration = 1.5f;

    [Tooltip("Плавность растекания")]
    public Ease AppearEase = Ease.OutQuad;

    [Tooltip("Сколько секунд лужа сжимается перед тем, как исчезнуть")]
    public float DryDuration = 3.5f;

    [Tooltip("До какой доли исходного размера съёживается лужа перед выключением")]
    [Range(0f, 1f)]
    public float DryScale = 0.05f;

    [Tooltip("Плавность высыхания")]
    public Ease DryEase = Ease.InQuad;

    // Только те лужи, что сейчас в активной комнате: регистрация идёт по
    // OnEnable/OnDisable, поэтому список сам чистится и при переключении комнат
    // (NormalRooms ↔ LabirintRooms), и при выгрузке сцены.
    private static readonly List<Puddle> Active = new();

    // Очередь натекания. Живёт отдельно от жизни конкретной лужи: перекрытый
    // кран должен отменить не только те лужи, что уже есть, но и те, что ещё
    // собирались натечь.
    private static CancellationTokenSource _floodCts;

    private SpriteRenderer _renderer;
    private PuddleZone _zone;
    private Vector3 _startScale;
    private Color _startColor;
    private Sequence _drying;
    private Tween _appearing;
    private bool _isFlooded;

    /// <summary>
    /// Пустить воду: лужи натекают по очереди по <see cref="Order"/> с интервалом
    /// <paramref name="interval"/> секунд между номерами. Первая появляется сразу.
    /// Токен — жизнь сюжета: уход в меню не должен оставлять тикающую очередь.
    /// </summary>
    public static void FloodAll(float interval, CancellationToken ct) {
        StopFlood();

        // Снимок на момент старта: дальше комнаты могут переключиться, и лужи
        // погасшей копии квартиры натекать уже не должны.
        List<Puddle> ordered = new(Active);
        ordered.Sort((a, b) => a.Order.CompareTo(b.Order));

        // Один шаг очереди — все лужи с одним номером.
        List<List<Puddle>> steps = new();
        foreach (Puddle puddle in ordered) {
            if (steps.Count == 0 || steps[steps.Count - 1][0].Order != puddle.Order) {
                steps.Add(new List<Puddle>());
            }

            steps[steps.Count - 1].Add(puddle);
        }

        _floodCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Flood(steps, interval, _floodCts.Token).Forget();
    }

    /// <summary>Высушить все лужи, которые сейчас лежат на полу, и отменить те, что ещё не натекли.</summary>
    public static void DryAll() {
        StopFlood();

        for (int i = Active.Count - 1; i >= 0; i--) {
            Active[i].Dry();
        }
    }

    /// <summary>Убрать воду мгновенно и без высыхания — этим сюжет начинает игру с сухого пола.</summary>
    public static void HideAll() {
        StopFlood();

        for (int i = Active.Count - 1; i >= 0; i--) {
            Active[i].Hide();
        }
    }

    /// <summary>Растечься из нуля в свой размер.</summary>
    public void Appear() {
        if (!gameObject.activeInHierarchy || _isFlooded) {
            return;
        }

        _isFlooded = true;
        KillTweens();

        // Зону гасим ДО обнуления масштаба: включённый коллайдер с нулевым
        // размером — это ошибка в консоли («BoxCollider does not support
        // negative scale or size»), а не просто лишняя работа.
        SetZoneActive(false);
        transform.localScale = Vector3.zero;
        if (_renderer != null) {
            _renderer.color = _startColor;
        }

        _appearing = transform.DOScale(_startScale, AppearDuration).SetEase(AppearEase);
        _appearing.OnComplete(() => {
            _appearing = null;
            // Шлёпать по растущему пятну рано — мокрым пол становится, когда
            // лужа натекла целиком. Заодно триггер не проходит через нулевой
            // масштаб, на котором физике нечего считать.
            SetZoneActive(true);
        });
    }

    /// <summary>Высушить эту лужу: сжать и выцветить.</summary>
    public void Dry() {
        if (!gameObject.activeInHierarchy || !_isFlooded || _drying != null) {
            return;
        }

        // Лужа, которую застали на середине растекания, досыхает с того размера,
        // до которого успела дорасти.
        if (_appearing != null) {
            _appearing.Kill();
            _appearing = null;
        }

        SetZoneActive(false);

        _drying = DOTween.Sequence();
        _drying.Join(transform.DOScale(_startScale * DryScale, DryDuration).SetEase(DryEase));
        if (_renderer != null) {
            _drying.Join(_renderer.DOFade(0f, DryDuration).SetEase(DryEase));
        }

        _drying.OnComplete(() => {
            // Обнуляем ДО Hide: тот убивает твин, — а он в этот момент как раз
            // доигрывает сам себя.
            _drying = null;
            Hide();
        });
    }

    /// <summary>Сухой пол: ни воды, ни мокрых шагов. Состояние, с которого лужа начинает.</summary>
    private void Hide() {
        KillTweens();
        _isFlooded = false;

        // Сначала зона, потом масштаб — см. Appear.
        SetZoneActive(false);
        transform.localScale = Vector3.zero;
        if (_renderer != null) {
            _renderer.color = _startColor;
        }
    }

    private static async UniTaskVoid Flood(List<List<Puddle>> steps, float interval, CancellationToken ct) {
        for (int i = 0; i < steps.Count; i++) {
            // Первая лужа натекает сразу — вместе с включённой водой в ванной.
            if (i > 0) {
                await UniTask.WaitForSeconds(interval, cancellationToken: ct);
            }

            foreach (Puddle puddle in steps[i]) {
                if (puddle != null) {
                    puddle.Appear();
                }
            }
        }
    }

    private static void StopFlood() {
        if (_floodCts == null) {
            return;
        }

        _floodCts.Cancel();
        _floodCts.Dispose();
        _floodCts = null;
    }

    private void SetZoneActive(bool isActive) {
        if (_zone != null && _zone.gameObject.activeSelf != isActive) {
            // Именно объектом, а не компонентом: на выключении PuddleZone
            // отпускает игрока из мокрых шагов, и без OnDisable он остался бы
            // «мокрым» после того, как лужа высохла у него под ногами.
            _zone.gameObject.SetActive(isActive);
        }
    }

    private void KillTweens() {
        if (_appearing != null) {
            _appearing.Kill();
            _appearing = null;
        }

        if (_drying != null) {
            _drying.Kill();
            _drying = null;
        }
    }

    private void Awake() {
        _renderer = GetComponent<SpriteRenderer>();
        _zone = GetComponentInChildren<PuddleZone>(true);
        _startScale = transform.localScale;
        if (_renderer != null) {
            _startColor = _renderer.color;
        }
    }

    private void OnEnable() {
        // Комнату могли включить посреди игры (подмена NormalRooms ↔ LabirintRooms)
        // или это вообще первый кадр: пол сухой, пока сюжет не пустит воду.
        Hide();

        if (!Active.Contains(this)) {
            Active.Add(this);
        }
    }

    private void OnDisable() {
        Active.Remove(this);
        KillTweens();
    }
}
