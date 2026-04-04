using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class Pickable : MonoBehaviour {
    public bool IsPicked = false;

    private Vector3 startingPos;
    private Quaternion startingRot;

    private CancellationTokenSource _cts = new();
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
        MoveTo().Forget();

        if (IsPicked) {
            OnPick?.Invoke();
            FirstPersonController.isHolding = true;
        } else {
            OnDrop?.Invoke();
            FirstPersonController.isHolding = false;
        }
    }

    private void Update() {
        if (IsPicked && Input.GetMouseButtonDown(1)) {
            TogglePick();
        }
    }
    
    Vector3 start => !IsPicked ? PlayerPicker.instance.pickedPos.position : startingPos;
    Vector3 end => IsPicked ? PlayerPicker.instance.pickedPos.position : startingPos;
    Quaternion startq => !IsPicked ? PlayerPicker.instance.pickedPos.rotation : startingRot;
    Quaternion endq => IsPicked ? PlayerPicker.instance.pickedPos.rotation : startingRot;
    
    private async UniTask MoveTo() {
        await UniTask.WaitForSeconds(0.1f);
       

        float t = 0;
        float max = 0.5f;
        while (t <= max) {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, t / max);
            transform.rotation = Quaternion.Lerp(startq, endq, t / max);
            await UniTask.WaitForEndOfFrame(_cts.Token);
        }
    }
}