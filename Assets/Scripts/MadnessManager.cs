using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class MadnessManager : MonoBehaviour {
    public AudioSource humming, clicking;

    public float HummingPower;
    public float ClickingPower;

    public float Madness;

    public float MaxMadness = 100;
    public float TmpMaxMadness = 100;
    public float MadnessSpeedPerS = 1;

    public bool IsMadnessRaising;
    public float HummingChillPerS = 2;

    public float ClickChillPerClick = 10;

    public bool IsHumming;

    public float RefillHumming = 1;
    public float RefillClicking = 1;

    public DateTime LastClickTime;
    public float ClickCooldown = 1.5f;
    public KeyCode HummingKey = KeyCode.E;
    public KeyCode ClickKey = KeyCode.Q;

    public RadioAudio RadioAudio;

    public StoryManager StoryManager;
    public HUD hud;
    private bool isDead;
    public bool IsVolumesFixed;
    public AudioSource FakeHummingFade;
    private CancellationTokenSource humCts = new();

    private void Start() {
        humming.Play();
        humming.Pause();
    }

    private bool _wasHumming;
    private void Update() {
        if (!isDead && !FirstPersonController.isHolding) {
            if (Input.GetKeyDown(ClickKey) && (DateTime.Now - LastClickTime).TotalSeconds > ClickCooldown) {
                LastClickTime = DateTime.Now;
                Click();
            }

            if (Input.GetMouseButtonDown(0) && hud.HasHammer) {
                if (CursorRaycast.Raycast(out RaycastHit hit)) {
                    if (CursorRaycast.CanHit(hit, out HittableObj obj)) {
                        obj.Hit();
                    }
                    hud.TriggerHit();
                } else {
                    hud.TriggerSwing();
                }
               
            }
        }
        

        if (HummingPower <= 80) {
            StoryManager.LogOnce("Hummed");
        }

        if (ClickingPower <= 70) {
            StoryManager.LogOnce("Clicked");
        }

        IsHumming = Input.GetKey(HummingKey);
        if (!isDead && IsHumming) {
            HummingPower -= HummingChillPerS * 1.5f * Time.deltaTime;
            if (!_wasHumming) {
                humming.UnPause();
                hud.SetMelody(true);
                BlendHumming(true);
            }
         
        } else {
            HummingPower += RefillHumming * Time.deltaTime;
          
            if (_wasHumming) {
                humming.Pause();
                hud.SetMelody(false);
                BlendHumming(false);
            }
        }

        if (IsMadnessRaising) {
            Madness += MadnessSpeedPerS * Time.deltaTime;
        }

        if (IsHumming) {
            Madness -= HummingChillPerS * 0.01f * HummingPower * Time.deltaTime;
        }

        ClickingPower += RefillClicking * Time.deltaTime;

        HummingPower = Mathf.Clamp(HummingPower, 0, 100);
        ClickingPower = Mathf.Clamp(ClickingPower, 0, 100);
        Madness = Mathf.Clamp(Madness, 0, TmpMaxMadness);

        if (!IsVolumesFixed) {
            UpdateSounds();
        }

        if (!isDead && Madness >= MaxMadness) {
            StoryManager.Lose();
            isDead = true;
        }

        _wasHumming = IsHumming;
    }

    private void UpdateSounds() {
        RadioAudio.SetPercent(Madness / MaxMadness);
        humming.volume = FakeHummingFade.volume * HummingPower / 100f;
        clicking.volume = ClickingPower / 100f;
    }

    public void Click() {
        Madness -= ClickChillPerClick * 0.01f * ClickingPower;
        ClickingPower -= ClickChillPerClick * 1.5f;
        hud.TriggerClick();
    }

    private void BlendHumming(bool isOn) {
        humCts?.Cancel();
        humCts = new CancellationTokenSource();
        FakeHummingFade.DOFade(isOn ? 1 : 0f, 0.3f).WithCancellation(humCts.Token);
    }
}