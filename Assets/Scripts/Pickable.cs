using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class Pickable : MonoBehaviour {
    public bool IsPicked = false;

    private Vector3 startingPos;
    private Quaternion startingRot;

    private CancellationTokenSource _cts = new CancellationTokenSource();
    public UnityEvent OnPick;
    public UnityEvent OnDrop;
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
        MoveTo(IsPicked ? PlayerPicker.instance.pickedPos.position : startingPos,
            IsPicked ? PlayerPicker.instance.pickedPos.rotation : startingRot
        ).Forget();
        
        if (IsPicked) {
            OnPick?.Invoke();
        } else {
            OnDrop?.Invoke();
        }
    }

    private void Update() {
        if (IsPicked && Input.GetMouseButtonDown(1)) {
            TogglePick();
        }
    }

    private async UniTask MoveTo(Vector3 target, Quaternion targetRot) {
         transform.DORotate(targetRot.eulerAngles, 0.5f).WithCancellation(_cts.Token);
        await transform.DOMove(target, 0.5f).WithCancellation(_cts.Token);
    }
}