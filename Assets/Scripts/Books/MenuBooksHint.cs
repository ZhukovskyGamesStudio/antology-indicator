using DG.Tweening;
using UnityEngine;

/// <summary>
/// Подсказка в правом верхнем углу главного меню: книги на столе можно брать
/// в руки и рассматривать. Висит, пока игрок ни разу этого не сделал, и уезжает
/// в тот момент, когда предмет оказался в руках.
///
/// Ловим не конкретную книгу, а общий факт «что-то взяли»: в меню всё, что
/// лежит на столе, поднимается через <see cref="Pickable"/>, и статический
/// <see cref="FirstPersonController.isHolding"/> есть даже без самого
/// контроллера — в меню его на сцене нет.
/// </summary>
public class MenuBooksHint : MonoBehaviour {
    [SerializeField]
    private CanvasGroup _group;

    [Tooltip("Плавность появления и ухода подсказки, секунды")]
    [SerializeField]
    private float _fadeDuration = 0.4f;

    [Tooltip("Показать не сразу, а через столько секунд после входа в меню")]
    [SerializeField]
    private float _showDelay = 1.2f;

    [Tooltip("Помнить, что подсказку уже видели: показывать только до первого " +
             "поднятого предмета за всё время. Выключить — будет показываться " +
             "каждый заход в меню")]
    [SerializeField]
    private bool _rememberSeen = true;

    private const string SeenKey = "MenuBooksHintSeen";

    private bool _hidden;

    private void Awake() {
        if (_group == null) {
            _group = GetComponent<CanvasGroup>();
        }

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    private void OnEnable() {
        if (_rememberSeen && PlayerPrefs.GetInt(SeenKey, 0) == 1) {
            _hidden = true;
            gameObject.SetActive(false);
            return;
        }

        // Игрок мог зайти в меню с предметом «в руках» — статика переживает
        // смену сцены, а Pickable.OnDisable снимает флаг уже после Awake.
        FirstPersonController.isHolding = false;
        _group.DOKill();
        _group.alpha = 0f;
        _group.DOFade(1f, _fadeDuration).SetDelay(_showDelay).SetUpdate(true);
    }

    private void Update() {
        if (_hidden || !FirstPersonController.isHolding) {
            return;
        }

        _hidden = true;
        if (_rememberSeen) {
            PlayerPrefs.SetInt(SeenKey, 1);
        }

        _group.DOKill();
        _group.DOFade(0f, _fadeDuration).SetUpdate(true);
    }

    private void OnDisable() {
        _group.DOKill();
    }

    /// <summary>Показать подсказку заново (например, после сброса прогресса).</summary>
    public static void ResetSeen() {
        PlayerPrefs.DeleteKey(SeenKey);
    }
}
