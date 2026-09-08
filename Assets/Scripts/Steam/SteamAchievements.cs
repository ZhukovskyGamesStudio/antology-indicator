// Steamworks.NET собирается только под Standalone (см. asmdef пакета), в WebGL
// его нет вовсе — поэтому всё, что трогает Steamworks, спрятано за STEAM_AVAILABLE.
// Снаружи класс существует всегда: вызовы Unlock из сюжета и книг на WebGL
// просто ничего не делают.
#if (UNITY_STANDALONE || UNITY_EDITOR) && !DISABLESTEAMWORKS
#define STEAM_AVAILABLE
#endif

using System.Collections.Generic;
using UnityEngine;
#if STEAM_AVAILABLE
using Steamworks;
#endif

/// <summary>
/// Достижения Steam. Поднимает Steam API при запуске игры (до первой сцены),
/// крутит колбэки и открывает достижения по API-имени.
///
/// Намеренно НЕ зовёт <c>SteamAPI.RestartAppIfNecessary</c>: та же сборка
/// уходит на itch.io, и там перезапуск через Steam был бы обрывом игры.
/// Если Steam не запущен или игра стартовала не из него — достижения
/// тихо отключены (<see cref="IsAvailable"/> = false).
///
/// App ID для запуска из редактора берётся из <c>steam_appid.txt</c> в корне
/// проекта (в билд этот файл не попадает — Steam подставляет ID сам).
/// </summary>
public static class SteamAchievements {
    /// <summary>Прошёл игру: даётся на старте титров.</summary>
    public const string FinishGame = "CT_WIN_GAME";

    /// <summary>Собрал все книги: даётся при сборе последней книги.</summary>
    public const string AllBooks = "CT_ALL_BOOKS";

    /// <summary>
    /// Тестовое приложение Valve. Доступно любому аккаунту Steam, и у него уже
    /// заведены свои достижения — на нём проверяется вся цепочка, пока
    /// настоящие достижения не заведены в Steamworks.
    /// </summary>
    private const uint SpacewarAppId = 480;

    /// <summary>Наши имена → достижения Spacewar (Winner и Orbiter). Работает только при App ID 480.</summary>
    private static readonly Dictionary<string, string> SpacewarNames = new() {
        { FinishGame, "ACH_WIN_ONE_GAME" },
        { AllBooks, "ACH_TRAVEL_FAR_SINGLE" },
    };

    /// <summary>Сколько раз пробовать открыть отложенное достижение, прежде чем сдаться.</summary>
    private const int MaxRetries = 30;

    /// <summary>Пауза между попытками, сек (по unscaledTime — пауза игры не мешает).</summary>
    private const float RetryInterval = 1f;

    public static bool IsAvailable { get; private set; }

    /// <summary>
    /// Достижения, которые не удалось открыть сразу: статистика Steam приходит
    /// асинхронно после Init, и SetAchievement до неё возвращает false.
    /// </summary>
    private static readonly List<string> _pending = new();

    private static int _retries;

    /// <summary>Запущены под Spacewar (App ID 480) — имена достижений подменяются на его.</summary>
    private static bool _isSpacewar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() {
        // Статика переживает отключённый domain reload — приводим к нулю руками.
        IsAvailable = false;
        _isSpacewar = false;
        _pending.Clear();
        _retries = 0;

#if STEAM_AVAILABLE
        ESteamAPIInitResult result = SteamAPI.InitEx(out string error);
        if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK) {
            Debug.Log($"[Steam] Недоступен ({result}): {error}. Достижения выключены.");
            return;
        }

        IsAvailable = true;
        GameObject go = new GameObject("SteamRunner");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<SteamRunner>();

        uint appId = SteamUtils.GetAppID().m_AppId;
        _isSpacewar = appId == SpacewarAppId;
#if UNITY_EDITOR
        WarnIfAppIdFileIsStale(appId);
#endif
        if (_isSpacewar) {
            Debug.LogWarning($"[Steam] App ID {appId} — это Spacewar. Достижения подменены на тестовые: " +
                             $"{FinishGame} → {SpacewarNames[FinishGame]}, {AllBooks} → {SpacewarNames[AllBooks]}.");
        } else {
            Debug.Log($"[Steam] Подключён, App ID {appId}.");
        }
#endif
    }

#if STEAM_AVAILABLE && UNITY_EDITOR
    /// <summary>
    /// Steam читает steam_appid.txt только при ПЕРВОМ Init в процессе, а дальше
    /// берёт ID из переменных окружения SteamAppId/SteamGameId, которые сам же
    /// и выставил. Поэтому правка файла вступает в силу только после перезапуска
    /// редактора — и без этой проверки расхождение ловилось бы вслепую.
    /// </summary>
    private static void WarnIfAppIdFileIsStale(uint actualAppId) {
        string path = System.IO.Path.Combine(Application.dataPath, "..", "steam_appid.txt");
        if (!System.IO.File.Exists(path)) {
            return;
        }

        string raw = System.IO.File.ReadAllText(path).Trim();
        if (uint.TryParse(raw, out uint fileAppId) && fileAppId != actualAppId) {
            Debug.LogWarning($"[Steam] В steam_appid.txt записан {fileAppId}, а Steam работает с {actualAppId}: " +
                             "App ID читается из файла только при первом запуске Steam API в процессе. " +
                             "Перезапусти Unity, чтобы новый ID вступил в силу.");
        }
    }
#endif

    /// <summary>Имя, под которым достижение реально живёт в текущем App ID.</summary>
    private static string Resolve(string id) {
        return _isSpacewar && SpacewarNames.TryGetValue(id, out string spacewarId) ? spacewarId : id;
    }

    /// <summary>
    /// Открыть достижение. Повторный вызов на уже открытом — ничего не делает.
    /// Без Steam — тоже ничего не делает.
    /// </summary>
    public static void Unlock(string id) {
        if (!IsAvailable || string.IsNullOrEmpty(id)) {
            return;
        }

        if (TryUnlock(id)) {
            return;
        }

        if (!_pending.Contains(id)) {
            _pending.Add(id);
        }
    }

    public static bool IsUnlocked(string id) {
#if STEAM_AVAILABLE
        return IsAvailable && SteamUserStats.GetAchievement(Resolve(id), out bool achieved) && achieved;
#else
        return false;
#endif
    }

    /// <summary>Сбросить все достижения и статистику — только для отладки.</summary>
    public static void ResetAll() {
#if STEAM_AVAILABLE
        if (!IsAvailable) {
            Debug.LogWarning("[Steam] Не подключён — сбрасывать нечего.");
            return;
        }

        SteamUserStats.ResetAllStats(true);
        SteamUserStats.StoreStats();
        Debug.Log("[Steam] Достижения сброшены.");
#endif
    }

    private static bool TryUnlock(string id) {
#if STEAM_AVAILABLE
        string steamId = Resolve(id);

        // GetAchievement возвращает false, пока статистика не пришла
        // с сервера, — и тогда SetAchievement тоже не сработал бы.
        if (!SteamUserStats.GetAchievement(steamId, out bool achieved)) {
            return false;
        }

        if (achieved) {
            return true;
        }

        if (!SteamUserStats.SetAchievement(steamId)) {
            return false;
        }

        // StoreStats и показывает плашку Steam, и отправляет на сервер.
        // Офлайн Steam-клиент запоминает сам и дошлёт, когда сможет.
        SteamUserStats.StoreStats();
        Debug.Log(steamId == id
            ? $"[Steam] Достижение открыто: {id}"
            : $"[Steam] Достижение открыто: {id} (в Spacewar это {steamId})");
        return true;
#else
        return true;
#endif
    }

    private static void FlushPending() {
        for (int i = _pending.Count - 1; i >= 0; i--) {
            if (TryUnlock(_pending[i])) {
                _pending.RemoveAt(i);
            }
        }

        if (_pending.Count == 0) {
            _retries = 0;
            return;
        }

        if (++_retries >= MaxRetries) {
            Debug.LogWarning($"[Steam] Не удалось открыть достижения: {string.Join(", ", _pending)}. " +
                             "Проверь, что такие API-имена заведены в Steamworks для этого App ID.");
            _pending.Clear();
            _retries = 0;
        }
    }

    private static void Shutdown() {
        if (!IsAvailable) {
            return;
        }

        IsAvailable = false;
        _pending.Clear();
#if STEAM_AVAILABLE
        SteamAPI.Shutdown();
#endif
    }

    /// <summary>Крутит колбэки Steam и добивает отложенные достижения.</summary>
    private class SteamRunner : MonoBehaviour {
        private float _nextRetryAt;

        private void Update() {
#if STEAM_AVAILABLE
            SteamAPI.RunCallbacks();
#endif
            if (_pending.Count > 0 && Time.unscaledTime >= _nextRetryAt) {
                _nextRetryAt = Time.unscaledTime + RetryInterval;
                FlushPending();
            }
        }

        // OnApplicationQuit приходит и при выходе из Play Mode в редакторе,
        // OnDestroy — на всякий случай, если объект снесут раньше.
        private void OnApplicationQuit() {
            Shutdown();
        }

        private void OnDestroy() {
            Shutdown();
        }
    }
}
