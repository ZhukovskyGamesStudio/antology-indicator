using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class Pickable : MonoBehLogger {
    /// <summary>
    /// Имя слоя, на который переключается поднятый предмет.
    /// Через этот слой работает рендеринг "поверх остального":
    /// основная камера его исключает, отдельная HeldItemCamera
    /// рисует только его (Clear Flags = Depth only, depth выше).
    /// </summary>
    public const string HeldLayerName = "HeldItem";

    public bool IsPicked = false;

    private Vector3 startingPos;
    private Quaternion startingRot;

    public Vector3 shiftRot;
    public Vector3 shiftPos;

    private CancellationTokenSource _cts = new();
    public UnityEvent OnPick;
    public UnityEvent OnDrop;

    [Header("Звук (случайный клип из набора)")]
    [Tooltip("Случайный клип при поднятии. Пусто — без звука")]
    public AudioClip[] pickClips;

    [Tooltip("Случайный клип при опускании. Пусто — используются pickClips")]
    public AudioClip[] dropClips;

    [Tooltip("Скорость вращения предмета мышью (°/сек на единицу mouse delta).")]
    public float rotateSpeed = 200f;

    [Header("Обучение вращению")]
    [Tooltip("Событие, которое логируется один раз, когда игрок повернёт удерживаемый предмет " +
             "суммарно на rotateLogThreshold градусов. Пусто — ничего не логировать.")]
    public string rotateLogEvent = "";

    [Tooltip("Сколько суммарно градусов нужно повернуть, чтобы засчитать вращение.")]
    public float rotateLogThreshold = 90f;

    private float _rotatedAmount;

    private int _heldLayer = -1;
    private readonly Dictionary<Transform, int> _originalLayers = new();
    private readonly Dictionary<Collider, bool> _originalColliderEnabled = new();
    private bool _isMoving;

    private void Awake() {
        FixStartingPos();
        _heldLayer = LayerMask.NameToLayer(HeldLayerName);
    }

    private void FixStartingPos() {
        startingPos = transform.position;
        startingRot = transform.rotation;
    }

    public void PickUp() {
        if (!IsPicked) {
            if ((transform.position - startingPos).magnitude > 3f) {
                FixStartingPos();
            }

            TogglePick();
        }
    }

    public void TogglePick() {
        IsPicked = !IsPicked;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        MoveTo().Forget();

        if (IsPicked) {
            _rotatedAmount = 0f;
            ApplyHeldLayer();
            DisableColliders();
            OnPick?.Invoke();
            FirstPersonController.isHolding = true;
        } else {
            RestoreOriginalLayer();
            RestoreColliders();
            OnDrop?.Invoke();
            FirstPersonController.isHolding = false;
        }

        // В главном меню HUD-а нет — там книги берутся тем же кодом, но без руки и прицела.
        if (HUD.instance != null) {
            HUD.instance.SetCursorAndHand(!IsPicked);
        }

        // Звук берётся/кладётся. Дистанция взаимодействия короткая (CursorRaycast),
        // поэтому позиционный PlayClipAtPoint в точке предмета всегда слышен рядом.
        AudioClip[] soundSet = IsPicked
            ? pickClips
            : (dropClips != null && dropClips.Length > 0 ? dropClips : pickClips);
        SoundUtil.PlayRandom(null, soundSet, transform.position);
    }

    private void DisableColliders() {
        // На время удержания предмет не должен физически взаимодействовать
        // с игроком — иначе при вращении он толкает капсулу. Гасим все
        // коллайдеры предмета и восстанавливаем при опускании.
        _originalColliderEnabled.Clear();
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) {
            _originalColliderEnabled[c] = c.enabled;
            c.enabled = false;
        }
    }

    private void RestoreColliders() {
        foreach (KeyValuePair<Collider, bool> kv in _originalColliderEnabled) {
            if (kv.Key != null) {
                kv.Key.enabled = kv.Value;
            }
        }

        _originalColliderEnabled.Clear();
    }

    private void Update() {
        if (!IsPicked) {
            return;
        }

        if (Input.GetMouseButtonDown(1)) {
            TogglePick();
            return;
        }

        // Вращение драгом ЛКМ. Пока MoveTo тянет предмет к точке в руке -
        // не вмешиваемся, чтобы лерп не дёргало.
        if (!_isMoving && Input.GetMouseButton(0)) {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            if (dx != 0f || dy != 0f) {
                Camera cam = Camera.main;
                Vector3 upAxis = cam != null ? cam.transform.up : Vector3.up;
                Vector3 rightAxis = cam != null ? cam.transform.right : Vector3.right;
                float frame = rotateSpeed * Time.deltaTime;
                transform.RotateAround(transform.position, upAxis, -dx * frame);
                transform.RotateAround(transform.position, rightAxis, dy * frame);

                // Обучение вращению: копим суммарный поворот и один раз логируем событие.
                if (!string.IsNullOrEmpty(rotateLogEvent)) {
                    _rotatedAmount += (Mathf.Abs(dx) + Mathf.Abs(dy)) * frame;
                    if (_rotatedAmount >= rotateLogThreshold) {
                        LogOnce(rotateLogEvent);
                    }
                }
            }
        }
    }

    private void ApplyHeldLayer() {
        if (_heldLayer < 0) {
            return;
        }

        _originalLayers.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) {
            _originalLayers[t] = t.gameObject.layer;
            t.gameObject.layer = _heldLayer;
        }
    }

    private void RestoreOriginalLayer() {
        foreach (KeyValuePair<Transform, int> kv in _originalLayers) {
            if (kv.Key != null) {
                kv.Key.gameObject.layer = kv.Value;
            }
        }

        _originalLayers.Clear();
    }

    private Vector3 end =>
        IsPicked
            ? PlayerPicker.instance.pickedPos.position + PlayerPicker.instance.pickedPos.right * shiftPos.x +
              PlayerPicker.instance.pickedPos.up * shiftPos.y + PlayerPicker.instance.pickedPos.forward * shiftPos.z
            : startingPos;
    private Quaternion endq => IsPicked
        ? PlayerPicker.instance.pickedPos.rotation * Quaternion.Euler(shiftRot)
        : startingRot;

    private async UniTask MoveTo() {
        _isMoving = true;
        try {
            await UniTask.WaitForSeconds(0.1f);

            float t = 0;
            float max = 0.5f;
            while (t <= max) {
              
                transform.position = Vector3.Slerp(transform.position, end, t / max);
                transform.rotation = Quaternion.Slerp(transform.rotation, endq, t / max);
                await UniTask.WaitForEndOfFrame(_cts.Token);
                t += Time.deltaTime;
            }
        }
        finally {
            _isMoving = false;
        }
    }
}
