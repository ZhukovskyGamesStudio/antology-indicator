using UnityEngine;

/// <summary>
/// Держит камеру поднятых предметов (overlay) в соответствии с основной камерой.
/// FOV у основной камеры меняется динамически (GallucinationManager, зум/спринт
/// FirstPersonController), поэтому каждый кадр копируем его на эту камеру.
/// Транзформ синхронизировать не нужно, если эта камера — дочерняя к основной
/// с нулевым локальным смещением.
///
/// Near/Far у overlay-камеры обычно специально другие (предметы рендерятся вблизи),
/// поэтому их копирование вынесено в отдельные флаги и по умолчанию выключено.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MatchCamera : MonoBehaviour {
    [Tooltip("Основная камера, под которую подстраиваемся.")]
    public Camera source;

    [Header("Что синхронизировать")]
    public bool matchFov = true;
    public bool matchProjection = true;
    public bool matchNearFar = false;
    public bool matchPhysical = false;

    private Camera _cam;

    private void Awake() {
        _cam = GetComponent<Camera>();
        if (source == null && transform.parent != null) {
            source = transform.parent.GetComponentInParent<Camera>();
        }
    }

    // LateUpdate — после того как FirstPersonController/GallucinationManager
    // уже выставили fieldOfView основной камеры в этом кадре.
    private void LateUpdate() {
        if (source == null) {
            return;
        }

        if (matchProjection) {
            _cam.orthographic = source.orthographic;
            _cam.orthographicSize = source.orthographicSize;
        }

        if (matchFov) {
            _cam.fieldOfView = source.fieldOfView;
        }

        if (matchPhysical) {
            _cam.usePhysicalProperties = source.usePhysicalProperties;
            if (source.usePhysicalProperties) {
                _cam.focalLength = source.focalLength;
                _cam.sensorSize = source.sensorSize;
                _cam.lensShift = source.lensShift;
                _cam.gateFit = source.gateFit;
            }
        }

        if (matchNearFar) {
            _cam.nearClipPlane = source.nearClipPlane;
            _cam.farClipPlane = source.farClipPlane;
        }
    }
}
