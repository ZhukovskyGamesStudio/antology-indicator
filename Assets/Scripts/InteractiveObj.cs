using UnityEngine;
using UnityEngine.Events;

public class InteractiveObj : MonoBehLogger {
    public UnityEvent OnClick;

    [Tooltip("Не проверять дистанцию до предмета. Нужно там, где курсор свободный и прицела нет " +
             "(книги, разложенные на столе в главном меню): OnMouseDown и так срабатывает только " +
             "на том объекте, который реально под курсором.")]
    public bool IgnoreRange;

    private void OnMouseDown() {
        // Пока игрок держит предмет в руке — с остальным миром взаимодействовать
        // нельзя (ЛКМ занята вращением предмета, ПКМ кладёт его обратно). Иначе
        // клик-вращение проходил бы рейкастом сквозь и открывал двери / брал второй
        // предмет. MadnessManager так же гейтит удар молотком по isHolding.
        if (FirstPersonController.isHolding) {
            return;
        }

        if (!enabled) {
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