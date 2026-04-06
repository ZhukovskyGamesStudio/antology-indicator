using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class RadioChanger : MonoBehLogger {
    public AudioSource change, normal, noise;

    public List<AudioClip> clips;

    private bool isChanging;
    private int _clipIndex;
    private bool isBlending;

    public int RadioMusic = 3;

    private void Start() {
        change.Play();
        change.Pause();
        normal.clip = clips[0];
        normal.Play();
        noise.Play();
        noise.Pause();
    }

    public void ChangeToNext() {
        if (isChanging || isBlending) {
            return;
        }

        isChanging = true;
        ChangeToNextAsync().Forget();
    }

    private async UniTask ChangeToNextAsync() {
        _clipIndex++;
        noise.Pause();
        normal.Stop();
        change.UnPause();
        await UniTask.WaitForSeconds(0.75f);
        change.Pause();
        LogOnce("RadioSwitched");

        if (_clipIndex < clips.Count - 2) {
            normal.clip = clips[_clipIndex];
            normal.time = Random.Range(0.1f, 10f);
            normal.Play();
        } else if (_clipIndex == clips.Count - 1) {
            normal.clip = clips[_clipIndex];
            normal.Play();
            normal.time = Random.Range(0.1f, 10f);
            noise.UnPause();
            LogOnce("RadioMusic2");
            BlendToNoise();
        } else {
            noise.UnPause();
        }

        if (_clipIndex == RadioMusic) {
            LogOnce("RadioMusic");
            MadnessManager.instance.SyncHumming(normal.time);
        }

        isChanging = false;
    }

    private async UniTask BlendToNoise() {
        isBlending = true;
        await UniTask.WaitForSeconds(4f);
        float time = 10;
        normal.DOFade(0, time);
        await noise.DOFade(0.5f, time);
        LogOnce("RadioNoise");
        isBlending = false;
    }
}