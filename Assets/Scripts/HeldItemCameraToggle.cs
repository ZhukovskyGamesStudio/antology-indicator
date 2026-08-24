using UnityEngine;

/// <summary>
/// Оверлей-камера предмета в руках рисует единственный слой — <see cref="Pickable.HeldLayerName"/>,
/// а объекты появляются там только на время, пока предмет поднят. С пустыми руками
/// камера каждый кадр гоняла полный проход впустую: DepthNormals prepass, четыре прохода
/// SSAO, опаковый проход и блит — ради кадра, в котором нечего рисовать. Держим её
/// выключенной, пока руки пусты.
///
/// Картинка от этого не меняется — проверено попиксельным сравнением кадров при пустом
/// слое: разница на уровне шума плёночного зерна (maxΔ=6 при пороге зерна 6).
///
/// Ловим статический <see cref="FirstPersonController.isHolding"/>, а не конкретный
/// Pickable: флаг выставляется в <c>TogglePick</c> ровно перед переносом объекта на слой
/// и сбрасывается перед возвратом слоя обратно, то есть совпадает с состоянием слоя
/// кадр в кадр. Проверка идёт в LateUpdate — после Update-ов, где предмет берут и кладут,
/// но до отрисовки.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HeldItemCameraToggle : MonoBehaviour {
    private Camera _camera;

    private void Awake() {
        _camera = GetComponent<Camera>();
        Apply();
    }

    private void LateUpdate() {
        Apply();
    }

    private void Apply() {
        bool needed = FirstPersonController.isHolding;
        if (_camera.enabled != needed) {
            _camera.enabled = needed;
        }
    }
}
