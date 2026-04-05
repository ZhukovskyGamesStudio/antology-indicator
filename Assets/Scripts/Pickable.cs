using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Pickable : MonoBehLogger, IDragHandler {
    public bool IsPicked = false;

    private Vector3 startingPos;
    private Quaternion startingRot;

    public Vector3 shiftRot;
    public Vector3 shiftPos;

    private CancellationTokenSource _cts = new();
    public UnityEvent OnPick;
    public UnityEvent OnDrop;
    public float rotateSpeed = 100;
    private void Awake() {
        startingPos = transform.position;
        startingRot = transform.rotation;
    }

    public void PickUp() {
        if (!IsPicked) {
            TogglePick();
        }
    }

    public void TogglePick() {
        IsPicked = !IsPicked;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        MoveTo().Forget();

        if (IsPicked) {
            OnPick?.Invoke();
            FirstPersonController.isHolding = true;
            HUD.instance.SetCursorAndHand(false);
        } else {
            OnDrop?.Invoke();
            FirstPersonController.isHolding = false;
            HUD.instance.SetCursorAndHand(true);
        }
    }

    private void Update() {
        if (IsPicked && Input.GetMouseButtonDown(1)) {
            TogglePick();
        }
    }

    private Vector3 end =>
        IsPicked
            ? PlayerPicker.instance.pickedPos.position + PlayerPicker.instance.pickedPos.right * shiftPos.x +
              PlayerPicker.instance.pickedPos.up * shiftPos.y + PlayerPicker.instance.pickedPos.forward * shiftPos.z
            : startingPos;
    private Quaternion endq => IsPicked
        ? PlayerPicker.instance.pickedPos.rotation * Quaternion.Euler(PlayerPicker.instance.pickedPos.right * shiftRot.x +
                                                                      PlayerPicker.instance.pickedPos.up * shiftRot.y +
                                                                      PlayerPicker.instance.pickedPos.forward * shiftRot.z)
        : startingRot;

    private async UniTask MoveTo() {
        await UniTask.WaitForSeconds(0.1f);

        float t = 0;
        float max = 0.5f;
        while (t <= max) {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, end, t / max);
            transform.rotation = Quaternion.Lerp(transform.rotation, endq, t / max);
            await UniTask.WaitForEndOfFrame(_cts.Token);
        }
    }

    public void OnDrag(PointerEventData eventData) {
        if (!IsPicked) {
            return;
        }
        if (eventData.delta.magnitude > 0) {
            transform.RotateAround(Vector3.up, eventData.delta.x * rotateSpeed);
            transform.RotateAround(Vector3.right, eventData.delta.y * rotateSpeed);
        }
    }
}