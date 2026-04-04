using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class RadioChanger : MonoBehaviour {
    public AudioSource change, normal;

    public List<AudioClip> clips;

    private bool isChanging;
    private int _clipIndex;

    private void Start() {
        change.Play();
        change.Pause();
    }

    public void ChangeToNext() {
        if (isChanging) {
            return;
        }

        isChanging = true;
        ChangeToNextAsync().Forget();
    }

    private async UniTask ChangeToNextAsync() {
        _clipIndex++;
        normal.Stop();
        change.UnPause();
        await UniTask.WaitForSeconds(0.75f);
        change.Pause();
        normal.clip = clips[Mathf.Min(_clipIndex, clips.Count - 1)];
        normal.Play();
        isChanging = false;
    }
}