using System;
using UnityEngine;

public class MadnessManager : MonoBehaviour {
    public AudioSource humming, clicking;

    public float HummingPower;
    public float ClickingPower;

    public float Madness;

    public float MaxMadness = 100;

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

    private void Start() {
        humming.Play();
        humming.Pause();
    }

    private void Update() {
        if (Input.GetKeyDown(ClickKey) && (DateTime.Now - LastClickTime).TotalSeconds > ClickCooldown) {
            LastClickTime = DateTime.Now;
            Click();
        }

        IsHumming = Input.GetKey(HummingKey);
        if (IsHumming) {
            HummingPower -= HummingChillPerS * 1.5f * Time.deltaTime;
            humming.UnPause();
        } else {
            humming.Pause();
            HummingPower += RefillHumming * Time.deltaTime;
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
        Madness = Mathf.Clamp(Madness, 0, MaxMadness);

        UpdateSounds();
    }

    private void UpdateSounds() {
        RadioAudio.SetPercent(Madness / MaxMadness);
        humming.volume = ClickingPower / 100f;
        clicking.volume = HummingPower / 100f;
    }

    public void Click() {
        Madness -= ClickChillPerClick * 0.01f * ClickingPower;
        ClickingPower -= ClickChillPerClick * 1.5f;
        clicking.Play();
    }
}