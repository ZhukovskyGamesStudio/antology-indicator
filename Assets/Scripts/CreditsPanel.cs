using DG.Tweening;
using UnityEngine;

/// <summary>
/// Выезжающая панель «О нас» в правом нижнем углу главного меню. В закрытом
/// виде наружу торчит только язычок-заголовок; клик по нему выдвигает панель
/// целиком, повторный — убирает.
///
/// Пока игрок держит книгу, панель прячется совсем: в этот же угол выезжает
/// плашка коллекции (<see cref="BookCollectedUI"/>), да и рассматривать
/// книгу поверх раскрытых титров незачем. Ловим не конкретную книгу, а
/// статический <see cref="FirstPersonController.isHolding"/> — самого
/// контроллера в меню нет, а статика есть (так же устроен MenuBooksHint).
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class CreditsPanel : MonoBehaviour {
    

    [Tooltip("Зазор от нижнего края экрана до язычка, когда панель закрыта")]
    public float ClosedMargin = 8f;

    [Tooltip("Отступ от нижнего края экрана, когда панель выдвинута")]
    public float OpenMargin = 98f;

    [Tooltip("Длительность выезда, сек")]
    public float SlideDuration = 0.45f;

    public Ease SlideEase = Ease.OutCubic;

    [Tooltip("За сколько панель гаснет и возвращается, когда книгу берут в руки")]
    public float FadeDuration = 0.3f;

    [SerializeField] private CanvasGroup _group;

    private RectTransform _rect;
    private bool _isOpen;
    private bool _isHiddenByBook;

    /// <summary>Y выдвинутой панели (pivot в её нижнем правом углу).</summary>
    private float OpenY => OpenMargin;

    /// <summary>Y закрытой панели: тело уезжает за нижний край, язычок остаётся.</summary>
    private float ClosedY => ClosedMargin;

    /// <summary>Открыть/закрыть — на это повешен клик по язычку.</summary>
    public void Toggle() {
        SetOpen(!_isOpen);
    }

    public void SetOpen(bool isOpen) {
        _isOpen = isOpen;
        _rect.DOKill();
        // SetUpdate(true): меню не зависит от timeScale, а в паузе игра его
        // обнуляет — панель должна ездить в любом случае.
        _rect.DOAnchorPosY(isOpen ? OpenY : ClosedY, SlideDuration)
            .SetEase(SlideEase)
            .SetUpdate(true);
    }

    private void Awake() {
        _rect = (RectTransform)transform;
        if (_group == null) {
            _group = GetComponent<CanvasGroup>();
        }
    }

    private void OnEnable() {
        // Каждый заход в меню панель закрыта и видна.
        _isOpen = false;
        _isHiddenByBook = false;
        _rect.DOKill();
        _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, ClosedY);

        if (_group != null) {
            _group.DOKill();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
        }
    }

    private void Update() {
        bool holding = FirstPersonController.isHolding;
        if (holding == _isHiddenByBook || _group == null) {
            return;
        }

        _isHiddenByBook = holding;
        // Клики отпускаем сразу, не дожидаясь конца фейда: панель уже «не тут».
        _group.blocksRaycasts = !holding;
        _group.interactable = !holding;
        _group.DOKill();
        _group.DOFade(holding ? 0f : 1f, FadeDuration).SetUpdate(true);
    }

    private void OnDisable() {
        _rect.DOKill();
        if (_group != null) {
            _group.DOKill();
        }
    }
}
