using UnityEngine;
using UnityEngine.Events;

public class InteractiveObj : MonoBehLogger {
    public UnityEvent OnClick;
    
    private void OnMouseDown() {
        // Пока игрок держит предмет в руке — с остальным миром взаимодействовать
        // нельзя (ЛКМ занята вращением предмета, ПКМ кладёт его обратно). Иначе
        // клик-вращение проходил бы рейкастом сквозь и открывал двери / брал второй
        // предмет. MadnessManager так же гейтит удар молотком по isHolding.
        if (FirstPersonController.isHolding) {
            return;
        }

        if (enabled && CursorRaycast.Raycast(out RaycastHit hit) && hit.distance <= CursorRaycast.RangeStatic) {
            OnClick?.Invoke();
        }
    }
}