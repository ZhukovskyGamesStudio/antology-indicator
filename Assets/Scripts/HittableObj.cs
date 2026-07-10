using UnityEngine;
using UnityEngine.Events;

public class HittableObj : MonoBehLogger {
    public int Hp;

    public UnityEvent OnHit;
    public UnityEvent OnDeath;

    [Header("Звук удара (случайный клип из набора)")]
    [Tooltip("Клип на каждый удар, пока объект жив")]
    public AudioClip[] hitClips;

    [Tooltip("Клип при разрушении. Пусто — используются hitClips")]
    public AudioClip[] deathClips;

    public void Hit() {
        Hp--;
        if (Hp > 0) {
            SoundUtil.PlayRandom(null, hitClips, transform.position);
            OnHit?.Invoke();
        } else {
            AudioClip[] set = deathClips != null && deathClips.Length > 0 ? deathClips : hitClips;
            SoundUtil.PlayRandom(null, set, transform.position);
            OnDeath?.Invoke();
        }
    }
}