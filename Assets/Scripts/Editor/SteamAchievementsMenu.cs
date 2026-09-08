using UnityEditor;
using UnityEngine;

/// <summary>Отладка достижений Steam из редактора: работает только в Play Mode при запущенном Steam.</summary>
public static class SteamAchievementsMenu {
    [MenuItem("Tools/Steam/Сбросить достижения")]
    private static void ResetAchievements() {
        if (!Application.isPlaying) {
            Debug.LogWarning("[Steam] Сброс достижений работает только в Play Mode.");
            return;
        }

        SteamAchievements.ResetAll();
    }

    [MenuItem("Tools/Steam/Показать состояние")]
    private static void ShowState() {
        if (!Application.isPlaying) {
            Debug.LogWarning("[Steam] Состояние видно только в Play Mode.");
            return;
        }

        Debug.Log($"[Steam] available={SteamAchievements.IsAvailable}, " +
                  $"{SteamAchievements.FinishGame}={SteamAchievements.IsUnlocked(SteamAchievements.FinishGame)}, " +
                  $"{SteamAchievements.AllBooks}={SteamAchievements.IsUnlocked(SteamAchievements.AllBooks)}");
    }
}
