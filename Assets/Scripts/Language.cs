using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Статический движок локализации.
///
/// Принцип: <b>русский исходник — это и есть ключ</b>. В коде, префабах и сценах
/// пишем русские строки как обычно (TalkUI.Say, ShowTask, React, ...), а в момент
/// показа они прогоняются через <see cref="Get"/>. Если для строки есть перевод —
/// показывается он, если нет — показывается русский (ничего не ломается).
///
/// Чтобы добавить перевод новой реплики/задачи/реакции — добавьте пару в
/// <see cref="LocalizationData"/>. Менять вызовы Say/ShowTask/React не нужно.
///
/// Язык можно переключать в любой момент: <see cref="ChangeLanguage"/> поднимает
/// <see cref="OnLanguageChanged"/>, на который подписаны все локализованные UI
/// (TalkUI, TasksUI, HintUI, WinPanel, LocalizedText, LocalizedTexture, LocalizedSprite),
/// и они мгновенно перерисовываются. Выбор языка хранится в PlayerPrefs.
/// </summary>
public static class Language {
    private const string PrefsKey = "Language";

    private static Dictionary<string, string> _ru2en;
    private static LangCode _current = LangCode.RU;
    private static bool _initialized;

    /// <summary>Вызывается при каждой смене языка.</summary>
    public static event Action OnLanguageChanged;

    /// <summary>Текущий язык (читается из PlayerPrefs при первом обращении).</summary>
    public static LangCode Current {
        get {
            EnsureInit();
            return _current;
        }
    }

    private static void EnsureInit() {
        if (_initialized) {
            return;
        }

        _initialized = true;

        _ru2en = new Dictionary<string, string>(LocalizationData.Pairs.Length);
        foreach ((string ru, string en) in LocalizationData.Pairs) {
            // Индексатор, а не Add: случайные дубли ключей просто перезапишутся,
            // а не уронят инициализацию исключением.
            _ru2en[ru] = en;
        }

        _current = (LangCode)PlayerPrefs.GetInt(PrefsKey, (int)LangCode.RU);
    }

    /// <summary>
    /// Перевести русскую строку-ключ на текущий язык.
    /// Если язык русский, перевода нет или он пустой — возвращает исходную строку.
    /// </summary>
    public static string Get(string ru) {
        EnsureInit();

        if (ru == null) {
            return null;
        }

        if (_current == LangCode.RU) {
            return ru;
        }

        return _ru2en.TryGetValue(ru, out string en) && !string.IsNullOrEmpty(en) ? en : ru;
    }

    /// <summary>Установить язык и оповестить подписчиков. Вызывается из кнопок RU/EN.</summary>
    public static void ChangeLanguage(LangCode langCode) {
        EnsureInit();

        _current = langCode;
        PlayerPrefs.SetInt(PrefsKey, (int)langCode);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    /// <summary>Переключить RU &lt;-&gt; EN одной кнопкой.</summary>
    public static void Toggle() {
        ChangeLanguage(Current == LangCode.RU ? LangCode.EN : LangCode.RU);
    }
}

[Serializable]
public enum LangCode {
    RU,
    EN
}
