using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class InteractiveObj : MonoBehLogger {
    public UnityEvent OnClick;

    [Tooltip("Не проверять дистанцию до предмета. Нужно там, где курсор свободный и прицела нет " +
             "(книги, разложенные на столе в главном меню): OnMouseDown и так срабатывает только " +
             "на том объекте, который реально под курсором.")]
    public bool IgnoreRange;

    private static readonly List<RaycastResult> UiHits = new();

    /// <summary>
    /// Перехватывает ли клик интерфейс. <see cref="OnMouseDown"/> приходит от
    /// физики и про UI не знает вообще ничего: книгу на столе в меню можно было
    /// взять прямо сквозь панель, а мир — потыкать сквозь экран паузы.
    ///
    /// Считаем сами через <see cref="EventSystem.RaycastAll"/>, а не через
    /// <c>IsPointerOverGameObject()</c>: тот отдаёт состояние с прошлого кадра
    /// и зависит от того, какой input-модуль стоит на EventSystem.
    /// </summary>
    public static bool IsPointerOverUI() {
        EventSystem es = EventSystem.current;
        if (es == null) {
            return false;
        }

        PointerEventData pointer = new PointerEventData(es) { position = Input.mousePosition };
        UiHits.Clear();
        es.RaycastAll(pointer, UiHits);
        return UiHits.Count > 0;
    }

    private void OnMouseDown() {
        // Пока игрок держит предмет в руке — с остальным миром взаимодействовать
        // нельзя: ЛКМ целиком отдана предмету (драг — вращение, клик — положить).
        // Иначе клик-вращение проходил бы рейкастом сквозь и открывал двери / брал
        // второй предмет. MadnessManager так же гейтит удар молотком по isHolding.
        if (FirstPersonController.isHolding) {
            return;
        }

        if (!enabled) {
            return;
        }

        // Под курсором интерфейс — клик его, а не предмета за ним.
        if (IsPointerOverUI()) {
            return;
        }

        if (IgnoreRange) {
            OnClick?.Invoke();
            return;
        }

        if (CursorRaycast.Raycast(out RaycastHit hit) && hit.distance <= CursorRaycast.RangeStatic) {
            OnClick?.Invoke();
        }
    }
}
