using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Раскладывает собранные книги неаккуратными стопками на столе в главном меню.
/// Книги настоящие: их можно взять в руки и повертеть, как в игре, —
/// но в коллекцию они не уходят (<see cref="CollectableBook.CollectOnDrop"/> = false).
/// Заодно снимает сборку и лимит дистанции со всех остальных предметов сцены —
/// см. <see cref="PrepareScenePickables"/>.
///
/// Места стопок задаются вручную точками <c>_stackAnchors</c>: каждая точка —
/// низ своей стопки. Гизмо в сцене рисует, как книги лягут, — можно двигать точки
/// прямо в редакторе и сразу видеть результат.
///
/// Стопка — живая: взял книгу снизу или из середины, и всё, что лежало выше,
/// оседает вниз, а не висит в воздухе. Кладут книгу всегда на вершину той стопки,
/// откуда её взяли, — возвращать её в середину некуда, там уже нет места.
/// </summary>
public class MenuBookStacks : MonoBehaviour {
    [SerializeField]
    private BookCatalog _catalog;

    [Tooltip("Точки стопок: одна точка — одна стопка, позиция = низ стопки. " +
             "Книги раскладываются по стопкам по очереди, чтобы они росли равномерно")]
    [SerializeField]
    private Transform[] _stackAnchors;

    [Header("Раскладка")]
    [Tooltip("Толщина книги (м) — шаг по высоте внутри стопки")]
    [SerializeField]
    private float _bookThickness = 0.08f;

    [Tooltip("Разброс книги внутри стопки по горизонтали (м)")]
    [SerializeField]
    private float _positionJitter = 0.035f;

    [Tooltip("Разброс поворота книги вокруг вертикали (градусы)")]
    [SerializeField]
    private float _angleJitter = 14f;

    [Tooltip("Поворот лежащей книги относительно точки. По умолчанию модель кладётся плашмя")]
    [SerializeField]
    private Vector3 _flatEuler = new(90f, 0f, 0f);

    [Tooltip("Зерно случайности: одинаковое — стопки всегда складываются одинаково")]
    [SerializeField]
    private int _seed = 20260727;

    [Header("Осадка стопки")]
    [Tooltip("Сколько падает книга, оставшаяся без соседа снизу")]
    [SerializeField]
    private float _settleDuration = 0.22f;

    [Tooltip("Пауза перед осадкой: книгу сначала вынимают из стопки, и только потом " +
             "верхние проваливаются на её место")]
    [SerializeField]
    private float _settleDelay = 0.1f;

    [Header("Гизмо")]
    [Tooltip("Сколько книг рисовать в каждой стопке при расстановке точек")]
    [SerializeField]
    private int _gizmoBooksPerStack = 6;

    [SerializeField]
    private Color _gizmoColor = new(1f, 0.85f, 0.4f, 0.9f);

    // Что сейчас лежит в каждой стопке, снизу вверх. Индекс списка = индекс точки.
    private readonly List<List<Pickable>> _stacks = new();

    // Из какой стопки книга — чтобы вернуть её именно туда, откуда взяли.
    private readonly Dictionary<Pickable, int> _stackOf = new();

    private void Start() {
        if (_catalog == null || _catalog.BookPrefab == null) {
            Debug.LogError("[MenuBookStacks] Не задан каталог книг или префаб", this);
            return;
        }

        if (StackCount == 0) {
            Debug.LogError("[MenuBookStacks] Не заданы точки стопок", this);
            return;
        }

        PrepareScenePickables();

        for (int i = 0; i < StackCount; i++) {
            _stacks.Add(new List<Pickable>());
        }

        int index = 0;
        foreach (string id in BookCollection.Found) {
            BookCatalog.Entry entry = _catalog.Find(id);
            if (entry == null) {
                // Книга есть в сохранении, но её нет в каталоге — каталог устарел.
                Debug.LogWarning($"[MenuBookStacks] Книги '{id}' нет в каталоге, пропускаю", this);
                continue;
            }

            Spawn(entry, index);
            index++;
        }

        for (int i = 0; i < StackCount; i++) {
            Relayout(i, instant: true);
        }
    }

    /// <summary>
    /// Приводит всё, что можно взять в руки в этой сцене, к «меню-правилам».
    ///
    /// Дистанция: в меню курсор свободный и прицела нет, поэтому лимит в 1.6 м только
    /// мешает — стопки стоят там, где их поставил художник, и до дальней уже «не дотянуться».
    ///
    /// Сборка: в меню лежит декоративная копия комнаты, и книги в ней — обычные
    /// инстансы BookGeneral с включённым <see cref="CollectableBook.CollectOnDrop"/>.
    /// Пока плашки коллекции в меню не было, это ничем не грозило, но теперь она есть —
    /// и такая книга при опускании ушла бы в коллекцию и выключилась прямо на столе.
    /// В меню не собирается ничего.
    /// </summary>
    private void PrepareScenePickables() {
        foreach (Pickable pickable in FindObjectsByType<Pickable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (pickable.gameObject.scene != gameObject.scene) {
                continue;
            }

            // Сам компонент тоже включаем, и это не то же самое, что включить
            // интерактив. С выключенным Pickable предмет прекрасно берётся
            // (PickUp зовут напрямую из UnityEvent), но положить его уже нельзя:
            // за опускание отвечает Update, а он у выключенного компонента не
            // тикает — предмет залипал в руках навсегда вместе с isHolding.
            // Ровно так вела себя раскрытая книга на столе.
            pickable.enabled = true;

            InteractiveObj interactive = pickable.GetComponent<InteractiveObj>();
            if (interactive != null) {
                interactive.IgnoreRange = true;
                // В меню берётся всё, что лежит на столе. Часть предметов
                // приезжает из игровой сцены с выключенным интерактивом (там его
                // включает сюжет) — например раскрытая книга на столе.
                interactive.enabled = true;
            }

            CollectableBook book = pickable.GetComponent<CollectableBook>();
            if (book != null) {
                book.CollectOnDrop = false;
            }
        }
    }

    private int StackCount => _stackAnchors == null ? 0 : _stackAnchors.Length;

    /// <summary>Книги идут по стопкам по кругу, чтобы стопки росли вместе, а не одна за другой.</summary>
    private int StackIndexOf(int index) => index % StackCount;

    private void Spawn(BookCatalog.Entry entry, int index) {
        int stack = StackIndexOf(index);
        Transform anchor = _stackAnchors[stack];
        if (anchor == null) {
            return;
        }

        SlotPose(stack, _stacks[stack].Count, out Vector3 position, out Quaternion rotation);

        GameObject book = Instantiate(_catalog.BookPrefab, position, rotation, anchor);
        book.name = "MenuBook_" + entry.Id;

        Apply(book, entry);

        Pickable pickable = book.GetComponent<Pickable>();
        if (pickable == null) {
            return;
        }

        _stacks[stack].Add(pickable);
        _stackOf[pickable] = stack;

        // Стопка должна знать, что книгу из неё вынули: иначе всё, что лежало
        // выше, останется висеть в воздухе, а сама книга вернётся в дырку,
        // которой к тому моменту уже нет.
        pickable.OnPick.AddListener(() => OnBookPicked(pickable));
        pickable.OnDrop.AddListener(() => OnBookDropped(pickable));
    }

    private void OnBookPicked(Pickable book) {
        if (!_stackOf.TryGetValue(book, out int stack)) {
            return;
        }

        // Книгу теперь ведёт Pickable — осадка стопки не должна тянуть её обратно.
        book.transform.DOKill();

        _stacks[stack].Remove(book);
        Relayout(stack, instant: false);
    }

    private void OnBookDropped(Pickable book) {
        if (!_stackOf.TryGetValue(book, out int stack)) {
            return;
        }

        book.transform.DOKill();

        if (!_stacks[stack].Contains(book)) {
            _stacks[stack].Add(book);
        }

        // Опускаемую книгу к её новому месту тянет сам Pickable (MoveTo), поэтому
        // здесь ей достаточно назначить «дом» — вершину стопки.
        Relayout(stack, instant: false, skip: book);
    }

    /// <summary>
    /// Пересобрать стопку снизу вверх: каждая книга занимает своё место по порядку.
    /// Место — за слотом, а не за книгой, поэтому осевшая книга наследует наклон
    /// того места, куда легла, и стопка остаётся такой же неаккуратной, как была.
    /// </summary>
    private void Relayout(int stack, bool instant, Pickable skip = null) {
        List<Pickable> books = _stacks[stack];
        for (int level = 0; level < books.Count; level++) {
            Pickable book = books[level];
            if (book == null) {
                continue;
            }

            SlotPose(stack, level, out Vector3 position, out Quaternion rotation);
            book.SetHomePose(position, rotation);

            // Книга в руках (или та, что как раз летит из рук) едет сама.
            if (book == skip || book.IsPicked) {
                continue;
            }

            if (instant) {
                book.transform.SetPositionAndRotation(position, rotation);
                continue;
            }

            if (book.transform.position == position && book.transform.rotation == rotation) {
                continue;
            }

            book.transform.DOKill();
            book.transform.DOMove(position, _settleDuration).SetDelay(_settleDelay).SetEase(Ease.InQuad);
            book.transform.DORotateQuaternion(rotation, _settleDuration).SetDelay(_settleDelay).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// Куда встаёт книга на уровне <paramref name="level"/> в стопке. Разброс
    /// детерминированный (зерно + номер стопки + номер уровня): одно и то же место
    /// всегда получает один и тот же наклон, поэтому раскладка не прыгает ни между
    /// запусками, ни когда стопка оседает.
    /// </summary>
    private void SlotPose(int stack, int level, out Vector3 position, out Quaternion rotation) {
        Transform anchor = _stackAnchors[stack];

        Random.State saved = Random.state;
        Random.InitState(_seed + stack * 7919 + level * 104729);
        float yaw = Random.Range(-_angleJitter, _angleJitter);
        float right = Random.Range(-_positionJitter, _positionJitter);
        float forward = Random.Range(-_positionJitter, _positionJitter);
        Random.state = saved;

        rotation = Rotation(anchor, yaw);
        Vector3 center = Center(anchor, level, right, forward);

        // Пивот книги — у корешка, а не в центре, поэтому сдвигаем точку заранее.
        BoxCollider prefabBox = _catalog != null && _catalog.BookPrefab != null
            ? _catalog.BookPrefab.GetComponent<BoxCollider>()
            : null;
        position = prefabBox != null ? center - rotation * prefabBox.center : center;
    }

    private Quaternion Rotation(Transform anchor, float yawJitter) {
        return anchor.rotation * Quaternion.Euler(0f, yawJitter, 0f) * Quaternion.Euler(_flatEuler);
    }

    /// <summary>Середина книги нужного уровня в стопке.</summary>
    private Vector3 Center(Transform anchor, int level, float rightJitter, float forwardJitter) {
        return anchor.position
            + anchor.right * rightJitter
            + anchor.forward * forwardJitter
            + anchor.up * (level * _bookThickness + _bookThickness * 0.5f);
    }

    private static void Apply(GameObject book, BookCatalog.Entry entry) {
        // В меню книга — просто предмет в руках: в коллекцию она не уходит
        // и не выключается при опускании.
        CollectableBook collectable = book.GetComponent<CollectableBook>();
        if (collectable != null) {
            collectable.Id = entry.Id;
            collectable.CollectOnDrop = false;
        }

        // Прицела в меню нет, курсор свободный — ограничение по дистанции не нужно.
        InteractiveObj interactive = book.GetComponent<InteractiveObj>();
        if (interactive != null) {
            interactive.IgnoreRange = true;
        }

        Pickable pickable = book.GetComponent<Pickable>();
        if (pickable != null) {
            pickable.shiftPos = new Vector3(-0.15f, 0, pickable.shiftPos.z);
        }

        Transform model = book.transform.childCount > 0 ? book.transform.GetChild(0) : null;
        if (model == null) {
            return;
        }

        model.name = entry.Id;

        if (entry.Mesh != null) {
            MeshFilter filter = model.GetComponent<MeshFilter>();
            if (filter != null) {
                filter.sharedMesh = entry.Mesh;
            }
        }

        LocalizedTexture localized = book.GetComponent<LocalizedTexture>();
        if (localized != null) {
            localized.SetTextures(entry.Ru, entry.En);
        }
    }

    private void OnDrawGizmos() {
        if (StackCount == 0) {
            return;
        }

        Vector3 size = BookSize();

        for (int stack = 0; stack < StackCount; stack++) {
            Transform anchor = _stackAnchors[stack];
            if (anchor == null) {
                continue;
            }

            // Основание стопки — крестик на столе, чтобы точку было видно даже пустой.
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawLine(anchor.position - anchor.right * 0.1f, anchor.position + anchor.right * 0.1f);
            Gizmos.DrawLine(anchor.position - anchor.forward * 0.1f, anchor.position + anchor.forward * 0.1f);

            for (int level = 0; level < _gizmoBooksPerStack; level++) {
                // Те же слоты, что и в игре, — гизмо показывает ровно ту стопку,
                // которая получится.
                SlotPose(stack, level, out Vector3 position, out Quaternion rotation);
                Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
                Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, _gizmoColor.a * (level == 0 ? 1f : 0.55f));
                Gizmos.DrawWireCube(PrefabBoxCenter(), size);
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private Vector3 PrefabBoxCenter() {
        if (_catalog != null && _catalog.BookPrefab != null) {
            BoxCollider box = _catalog.BookPrefab.GetComponent<BoxCollider>();
            if (box != null) {
                return box.center;
            }
        }

        return Vector3.zero;
    }

    /// <summary>Габариты книги берём с коллайдера префаба, чтобы гизмо не врало.</summary>
    private Vector3 BookSize() {
        if (_catalog != null && _catalog.BookPrefab != null) {
            BoxCollider box = _catalog.BookPrefab.GetComponent<BoxCollider>();
            if (box != null) {
                return box.size;
            }
        }

        return new Vector3(0.3f, 0.46f, 0.08f);
    }
}
