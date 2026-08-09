using UnityEngine;

public class ToggleOnOff : MonoBehLogger {
    public GameObject target;

    [Header("Звук (опционально)")]
    [Tooltip("Щелчок при включении. Пусто — без звука")]
    public AudioClip onClip;

    [Tooltip("Щелчок при выключении. Пусто — без звука")]
    public AudioClip offClip;

    public void Toggle() {
        Set(!target.activeSelf);
    }

    public void Set(bool isOn) {
        // Звук только на реальной смене состояния: повторный Set в то же
        // состояние не должен щёлкать.
        if (target.activeSelf != isOn) {
            AudioClip clip = isOn ? onClip : offClip;
            if (clip != null) {
                AudioSource.PlayClipAtPoint(clip, target.transform.position);
            }
        }

        target.SetActive(isOn);
    }
}
