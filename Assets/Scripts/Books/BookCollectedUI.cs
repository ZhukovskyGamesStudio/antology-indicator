using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Плашка «книга в коллекции» в правом нижнем углу: выезжает, висит пару секунд
/// и уезжает обратно. Текст — сколько книг собрано из скольких.
/// </summary>
public class BookCollectedUI : MonoBehaviour {
    public static BookCollectedUI instance;

    [SerializeField]
    private CanvasGroup _group;

    [SerializeField]
    private TextMeshProUGUI _text;

    [Header("Анимация")]
    [Tooltip("На сколько плашка уезжает вправо в спрятанном состоянии")]
    [SerializeField]
    private float _hideOffset = 320f;

    [SerializeField]
    private float _showDuration = 0.45f;

    [SerializeField]
    private float _hideDuration = 0.35f;

    [Tooltip("Сколько плашка висит на экране, прежде чем уехать")]
    [SerializeField]
    private float _holdDuration = 3f;

    [Header("Счётчик")]
    [Tooltip("Сколько всего книг на уровне. 0 — посчитать самому по сцене")]
    [SerializeField]
    private int _totalOverride;

    private RectTransform _rect;
    private Vector2 _shownPos;
    private Sequence _sequence;
    private int _total;

    private void Awake() {
        instance = this;
        _rect = (RectTransform)transform;
        _shownPos = _rect.anchoredPosition;

        if (_group == null) {
            _group = GetComponent<CanvasGroup>();
        }

        _total = _totalOverride > 0 ? _totalOverride : CountBooksInScene();
        BookCollection.Total = _total;

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _rect.anchoredPosition = _shownPos + new Vector2(_hideOffset, 0f);
        Redraw();
    }

    private void OnEnable() {
        Language.OnLanguageChanged += Redraw;
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= Redraw;
    }

    private void OnDestroy() {
        _sequence?.Kill();
        if (instance == this) {
            instance = null;
        }
    }

    /// <summary>Показать плашку с текущим состоянием коллекции.</summary>
    /// <param name="isNew">Книга найдена впервые — плашка дополнительно «дёргается».</param>
    public void Show(bool isNew) {
        Redraw();

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Append(_rect.DOAnchorPos(_shownPos, _showDuration).SetEase(Ease.OutBack));
        _sequence.Join(_group.DOFade(1f, _showDuration * 0.6f));

        if (isNew) {
            _sequence.Append(_rect.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.6f));
        }

        _sequence.AppendInterval(_holdDuration);
        _sequence.Append(_rect.DOAnchorPos(_shownPos + new Vector2(_hideOffset, 0f), _hideDuration).SetEase(Ease.InBack));
        _sequence.Join(_group.DOFade(0f, _hideDuration));
    }

    private void Redraw() {
        if (_text != null) {
            _text.text = string.Format(Language.Get("{0} из {1}"), BookCollection.Count, _total);
        }
    }

    /// <summary>
    /// Считает уникальные книги на уровне: одна и та же книга встречается
    /// и в обычных комнатах, и в лабиринте — это по-прежнему одна книга.
    /// </summary>
    private static int CountBooksInScene() {
        CollectableBook[] books = FindObjectsByType<CollectableBook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Collections.Generic.HashSet<string> ids = new();
        for (int i = 0; i < books.Length; i++) {
            if (!books[i].CollectOnDrop) {
                continue;
            }

            string id = string.IsNullOrEmpty(books[i].Id) ? CollectableBook.ResolveId(books[i].transform) : books[i].Id;
            ids.Add(id);
        }

        return ids.Count;
    }
}
