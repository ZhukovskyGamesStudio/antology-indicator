using UnityEngine;
using UnityEngine.Events;

public class HittableObj : MonoBehLogger {
    public int Hp;

    public UnityEvent OnHit;
    public UnityEvent OnDeath;

    public void Hit() {
        Hp--;
        if (Hp > 0) {
            OnHit?.Invoke();
        } else {
            OnDeath?.Invoke();
        }
    }
}