using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Лужа, натёкшая из ванной. Появляется вместе с текущей водой
/// (<see cref="RunningWater"/>) и высыхает, когда игрок перекрывает кран:
/// сжимается, выцветает и в конце выключается.
///
/// Активные лужи держат себя в общем списке, поэтому крану не нужны ссылки на
/// них. Это не украшательство, а единственный рабочий способ: лужи лежат в
/// LabirintRooms, а кран — внутри вложенного префаба watercloset, и сослаться
/// из одного на другое в инспекторе нельзя. Луж при этом может быть сколько
/// угодно и в скольких угодно комнатах — <see cref="DryAll"/> высушит все.
///
/// Про мокрые шаги знает не лужа, а <see cref="PuddleZone"/> на дочернем триггере.
/// </summary>
[DisallowMultipleComponent]
public class Puddle : MonoBehaviour {
    [Tooltip("Сколько секунд лужа сжимается перед тем, как исчезнуть")]
    public float DryDuration = 3.5f;

    [Tooltip("До какой доли исходного размера съёживается лужа перед выключением")]
    [Range(0f, 1f)]
    public float DryScale = 0.05f;

    [Tooltip("Плавность высыхания")]
    public Ease DryEase = Ease.InQuad;

    // Только те лужи, что сейчас реально лежат на полу: регистрация идёт по
    // OnEnable/OnDisable, поэтому список сам чистится и при высыхании, и при
    // выключении комнаты, и при выгрузке сцены.
    private static readonly List<Puddle> Active = new();

    private SpriteRenderer _renderer;
    private Vector3 _startScale;
    private Color _startColor;
    private Sequence _drying;

    /// <summary>Высушить все лужи, которые сейчас есть на сцене.</summary>
    public static void DryAll() {
        // С конца: высохшая лужа выключается и вычёркивает себя из списка.
        for (int i = Active.Count - 1; i >= 0; i--) {
            Active[i].Dry();
        }
    }

    /// <summary>Высушить эту лужу: сжать, выцветить и выключить в конце.</summary>
    public void Dry() {
        if (!gameObject.activeInHierarchy || _drying != null) {
            return;
        }

        _drying = DOTween.Sequence();
        _drying.Join(transform.DOScale(_startScale * DryScale, DryDuration).SetEase(DryEase));
        if (_renderer != null) {
            _drying.Join(_renderer.DOFade(0f, DryDuration).SetEase(DryEase));
        }

        _drying.OnComplete(() => {
            // Обнуляем ДО SetActive: выключение дёрнет OnDisable, который убивает
            // твин, — а тот в этот момент как раз доигрывает сам себя.
            _drying = null;
            gameObject.SetActive(false);
        });
    }

    private void Awake() {
        _renderer = GetComponent<SpriteRenderer>();
        _startScale = transform.localScale;
        if (_renderer != null) {
            _startColor = _renderer.color;
        }
    }

    private void OnEnable() {
        // С прошлого высыхания лужа осталась сжатой и прозрачной — возвращаем как было.
        transform.localScale = _startScale;
        if (_renderer != null) {
            _renderer.color = _startColor;
        }

        if (!Active.Contains(this)) {
            Active.Add(this);
        }
    }

    private void OnDisable() {
        Active.Remove(this);
        if (_drying != null) {
            _drying.Kill();
            _drying = null;
        }
    }
}
