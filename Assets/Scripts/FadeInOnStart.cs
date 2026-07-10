using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Затемняющая панель, которая в начале сцены плавно уходит в прозрачность
/// (alpha 1 → 0) за <see cref="duration"/> секунд. По завершении отключается,
/// чтобы не перехватывать клики. Использует <see cref="CanvasGroup"/>, если он есть,
/// иначе — альфу <see cref="Image"/>.
/// </summary>
public class FadeInOnStart : MonoBehaviour {
    [Tooltip("Длительность растворения, сек")]
    public float duration = 1f;

    [Tooltip("Отключить объект после растворения (чтобы не перехватывал клики)")]
    public bool disableWhenDone = true;

    private void Start() {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null) {
            group.alpha = 1f;
            group.DOFade(0f, duration).SetEase(Ease.Linear).OnComplete(Done);
            return;
        }

        Image image = GetComponent<Image>();
        if (image != null) {
            Color c = image.color;
            c.a = 1f;
            image.color = c;
            image.DOFade(0f, duration).SetEase(Ease.Linear).OnComplete(Done);
        }
    }

    private void Done() {
        if (disableWhenDone) {
            gameObject.SetActive(false);
        }
    }
}
