using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Коллекция найденных книг. Живёт в PlayerPrefs и переживает и смерть,
/// и рестарт, и выход в меню: книги на уровне каждый раз стоят на своих местах,
/// а счётчик — общий на все прохождения.
///
/// Идентификатор книги — имя дочерней модели (book_3, book_11 и т.п.),
/// см. <see cref="CollectableBook"/>.
/// </summary>
public static class BookCollection {
    private const string FoundKey = "BooksFound";
    private const string TotalKey = "BooksTotal";
    private const char Separator = ';';

    private static List<string> _found;

    /// <summary>Строка, из которой собран текущий кэш, — чтобы заметить правку PlayerPrefs со стороны.</summary>
    private static string _cachedRaw;

    /// <summary>Найденные книги в порядке нахождения.</summary>
    public static IReadOnlyList<string> Found {
        get {
            Load();
            return _found;
        }
    }

    public static int Count {
        get {
            Load();
            return _found.Count;
        }
    }

    /// <summary>
    /// Сколько всего уникальных книг на уровне. Считается в игровой сцене
    /// (<see cref="BookCollectedUI"/>) и сохраняется, чтобы меню знало число
    /// без загрузки уровня.
    /// </summary>
    public static int Total {
        get => PlayerPrefs.GetInt(TotalKey, 0);
        set {
            if (value > 0 && value != PlayerPrefs.GetInt(TotalKey, 0)) {
                PlayerPrefs.SetInt(TotalKey, value);
                PlayerPrefs.Save();
            }
        }
    }

    public static bool IsFound(string id) {
        Load();
        return _found.Contains(id);
    }

    /// <summary>Добавить книгу. Возвращает true, если она найдена впервые.</summary>
    public static bool Add(string id) {
        if (string.IsNullOrEmpty(id)) {
            return false;
        }

        Load();
        if (_found.Contains(id)) {
            return false;
        }

        _found.Add(id);
        Save();
        return true;
    }

    public static void Clear() {
        _found = new List<string>();
        Save();
    }

    private static void Load() {
        string raw = PlayerPrefs.GetString(FoundKey, "");
        if (_found != null && raw == _cachedRaw) {
            return;
        }

        _found = new List<string>();
        _cachedRaw = raw;

        if (string.IsNullOrEmpty(raw)) {
            return;
        }

        foreach (string id in raw.Split(Separator)) {
            if (!string.IsNullOrEmpty(id) && !_found.Contains(id)) {
                _found.Add(id);
            }
        }
    }

    private static void Save() {
        _cachedRaw = string.Join(Separator.ToString(), _found);
        PlayerPrefs.SetString(FoundKey, _cachedRaw);
        PlayerPrefs.Save();
    }
}
