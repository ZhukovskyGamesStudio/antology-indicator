using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ограничивает ширину подписи, которая живёт внутри «обнимающей» плашки
/// (<see cref="HorizontalLayoutGroup"/> + <see cref="ContentSizeFitter"/>): пока строка
/// короче предела, плашка обнимает её как раньше; длиннее — текст переносится на
/// следующую строку, а плашка перестаёт расти.
///
/// Зачем: плашка тянулась ровно по длине строки, и предела у неё не было. Русские
/// реплики помещались, а более длинные английские вылезали за края экрана и
/// обрезались с ОБЕИХ сторон (плашка субтитров отцентрована) — например
/// «There was something else here before...» просилась на 1948 px при экране 1920.
///
/// Перенос, а не автоуменьшение кегля: субтитры и задачи должны читаться одним
/// размером весь фильм, прыгающий кегль заметнее второй строки.
/// </summary>
public static class TextWidthLimit {
    /// <summary>
    /// Пересчитать ограничение под текущий текст. Звать сразу после присваивания
    /// <c>text.text</c> и ДО <c>ForceMeshUpdate</c>.
    /// </summary>
    public static void Apply(TMP_Text text, LayoutElement element, float maxWidth) {
        if (text == null || element == null || maxWidth <= 0f) {
            return;
        }

        float natural = text.GetPreferredValues(text.text).x;
        if (natural <= maxWidth) {
            // -1 = ширину не навязываем: плашка обнимает строку, как и раньше.
            element.preferredWidth = -1f;
        } else {
            // Ширина ПОСЛЕ переноса — это длина самой длинной получившейся строки,
            // она обычно заметно меньше предела. Иначе плашка всегда раздувалась бы
            // до максимума, даже когда вторая строка короткая.
            element.preferredWidth = text.GetPreferredValues(text.text, maxWidth, 0f).x;
        }

        // Пересобираем разметку немедленно: следом идёт ForceMeshUpdate, а он считает
        // переносы по ТЕКУЩЕЙ ширине прямоугольника. Без пересборки первый кадр реплики
        // верстался бы по ширине от предыдущей строки.
        RectTransform panel = text.transform.parent as RectTransform;
        if (panel != null) {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }
    }

    /// <summary>
    /// Достать <see cref="LayoutElement"/> подписи, создав его при необходимости.
    /// Компонент нужен только чтобы навязать ширину, поэтому держать его в сцене
    /// руками незачем.
    /// </summary>
    public static LayoutElement EnsureElement(TMP_Text text) {
        if (text == null) {
            return null;
        }

        LayoutElement element = text.GetComponent<LayoutElement>();
        return element != null ? element : text.gameObject.AddComponent<LayoutElement>();
    }
}
