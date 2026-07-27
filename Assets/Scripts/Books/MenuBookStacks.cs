using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Раскладывает собранные книги неаккуратными стопками на столе в главном меню.
/// Книги настоящие: их можно взять в руки и повертеть, как в игре, —
/// но в коллекцию они не уходят (<see cref="CollectableBook.CollectOnDrop"/> = false).
///
/// Места стопок задаются вручную точками <c>_stackAnchors</c>: каждая точка —
/// низ своей стопки. Гизмо в сцене рисует, как книги лягут, — можно двигать точки
/// прямо в редакторе и сразу видеть результат.
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

    [Header("Гизмо")]
    [Tooltip("Сколько книг рисовать в каждой стопке при расстановке точек")]
    [SerializeField]
    private int _gizmoBooksPerStack = 6;

    [SerializeField]
    private Color _gizmoColor = new(1f, 0.85f, 0.4f, 0.9f);

    private void Start() {
        if (_catalog == null || _catalog.BookPrefab == null) {
            Debug.LogError("[MenuBookStacks] Не задан каталог книг или префаб", this);
            return;
        }

        if (StackCount == 0) {
            Debug.LogError("[MenuBookStacks] Не заданы точки стопок", this);
            return;
        }

        Random.State savedState = Random.state;
        Random.InitState(_seed);

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

        Random.state = savedState;
    }

    private int StackCount => _stackAnchors == null ? 0 : _stackAnchors.Length;

    /// <summary>Книги идут по стопкам по кругу, чтобы стопки росли вместе, а не одна за другой.</summary>
    private Transform StackOf(int index) => _stackAnchors[index % StackCount];

    private int LevelOf(int index) => index / StackCount;

    private void Spawn(BookCatalog.Entry entry, int index) {
        Transform anchor = StackOf(index);
        if (anchor == null) {
            return;
        }

        Quaternion rotation = Rotation(anchor, Random.Range(-_angleJitter, _angleJitter));
        Vector3 center = Center(anchor, LevelOf(index),
            Random.Range(-_positionJitter, _positionJitter),
            Random.Range(-_positionJitter, _positionJitter));

        // Пивот книги — у корешка, а не в центре, поэтому сдвигаем точку заранее:
        // Pickable в Awake запоминает стартовую позицию, и двигать объект после
        // Instantiate уже поздно — книга возвращалась бы не на своё место.
        BoxCollider prefabBox = _catalog.BookPrefab.GetComponent<BoxCollider>();
        Vector3 position = prefabBox != null ? center - rotation * prefabBox.center : center;

        GameObject book = Instantiate(_catalog.BookPrefab, position, rotation, anchor);
        book.name = "MenuBook_" + entry.Id;

        Apply(book, entry);
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
                // Тот же «случайный» наклон, что и в игре: индекс книги по кругу,
                // так что гизмо показывает примерно ту же стопку, что получится.
                float yaw = Mathf.Sin((stack + 1) * 12.9898f + level * 78.233f) * _angleJitter;
                Gizmos.matrix = Matrix4x4.TRS(Center(anchor, level, 0f, 0f), Rotation(anchor, yaw), Vector3.one);
                Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, _gizmoColor.a * (level == 0 ? 1f : 0.55f));
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
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
