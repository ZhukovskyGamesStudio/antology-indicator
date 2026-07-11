using System.Collections.Generic;

/// <summary>
/// Подсветка важных для прохождения слов в репликах ГГ оранжевым.
/// Применяется в момент показа (см. <see cref="TalkUI"/>.BuildDisplay), поверх
/// уже переведённой строки — сами строки и ключи локализации не меняются.
///
/// Ключ таблицы — русский исходник реплики (тот же, что в <see cref="LocalizationData"/>).
/// Слова для RU и EN разные, поэтому хранятся отдельно; при смене языка
/// TalkUI перерисовывает реплику и подсветка пересобирается автоматически.
/// </summary>
public static class HighlightData {
    public const string Color = "#FF7A00";

    private static readonly Dictionary<string, (string[] ru, string[] en)> Words =
        new Dictionary<string, (string[] ru, string[] en)> {
            { "Померещилось, но кажется молоток был в прихожей",
                (new[] { "молоток", "прихожей" }, new[] { "hammer", "hallway" }) },
            { "А ванная где..?",
                (new[] { "ванная" }, new[] { "bathroom" }) },
            { "Откуда вода? На улице ж нет дождя! Что за дверью?..",
                (new[] { "дверью" }, new[] { "door" }) },
            { "Не работает! Надо искать что-то ещё.",
                (new[] { "искать что-то ещё" }, new[] { "find something else" }) },
            { " Карандаши?! Да должны же где-то быть нормальные подушки!",
                (new[] { "нормальные подушки" }, new[] { "normal pillows" }) },
            { "И давно на ванной замок?",
                (new[] { "замок" }, new[] { "lock" }) },
            { "Издеваетесь?.. Теперь я двигаю мебель щелчками?",
                (new[] { "двигаю мебель щелчками" }, new[] { "move furniture by snapping my fingers" }) },
            { "Транзисторы... Лучше напишите, как вырубить чёртово радио!",
                (new[] { "радио" }, new[] { "radio" }) },
            { "Это почерк дяди! Что за?.. Микрочипы? Чихание?",
                (new[] { "Микрочипы", "Чихание" }, new[] { "Microchips", "Sneezing" }) },
            { "У меня аллергия на перец...",
                (new[] { "перец" }, new[] { "pepper" }) },
            { "Перец, ватная палочка, перо… Перец, ватная палочка, перо…",
                (new[] { "Перец", "ватная палочка", "перо" }, new[] { "Pepper", "cotton swab", "feather" }) },
        };

    /// <summary>
    /// Обернуть важные слова строки в оранжевый цвет. <paramref name="ruKey"/> — русский
    /// исходник реплики (по нему выбираем набор слов), <paramref name="translated"/> — уже
    /// переведённый текст, в котором заменяем слова текущего языка.
    /// </summary>
    public static string Apply(string ruKey, string translated) {
        if (ruKey == null || translated == null || !Words.TryGetValue(ruKey, out (string[] ru, string[] en) entry)) {
            return translated;
        }

        string[] words = Language.Current == LangCode.EN ? entry.en : entry.ru;
        foreach (string w in words) {
            if (!string.IsNullOrEmpty(w)) {
                translated = translated.Replace(w, "<color=" + Color + ">" + w + "</color>");
            }
        }

        return translated;
    }
}
