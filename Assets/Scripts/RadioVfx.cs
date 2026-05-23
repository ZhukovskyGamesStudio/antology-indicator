using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class RadioVfx : MonoBehaviour {
    public Transform RotateButtonO, Line;
    private CancellationTokenSource _rotateCts = new();

    public Animation Animation;
    public AnimationClip hitClip, breakClip;
    private bool isBroken;
    public GameObject radioMain, note;

    public void RotateButton() {
        _rotateCts?.Cancel();
        _rotateCts = new CancellationTokenSource();

        float dur = 0.5f;

        RotateButtonO.DOLocalRotate(Vector3.forward * Random.Range(10, 360), dur).WithCancellation(_rotateCts.Token);
        Line.transform.DOLocalMoveX(Random.Range(-0.264f, 0.216f), dur);
    }

    public void LogRadioHit() {
        if (isBroken) {
            return;
        }

        Animation.Play(hitClip.name);
        StoryManager.instance.LogEvent("RadioHit");
    }


    public void LogRadioBroken() {
        if (isBroken) {
            return;
        }

        isBroken = true;
        DisableHittables();
        BreakAsync().Forget();
    }

    private void DisableHittables() {
        // Сразу гасим Hittable на Base и на активирующемся в анимации Broken,
        // чтобы новые удары не прервали breakClip и не оставили Broken
        // в промежуточном (маленьком) масштабе.
        foreach (HittableObj hittable in GetComponentsInChildren<HittableObj>(true)) {
            hittable.enabled = false;
        }
    }

    private async UniTask BreakAsync() {
        Animation.Stop();
        Animation.Play(breakClip.name);
        await UniTask.WaitWhile(() => Animation.isPlaying);
        radioMain.SetActive(false);
        note.SetActive(true);
        StoryManager.instance.LogEvent("RadioBroken");
    }
}