using System;
using System.Globalization;
using UnityEngine;
using Random = UnityEngine.Random;

public class SaveLoadManager : PreloadableSingleton<SaveLoadManager> {
    public static GameSaveProfile CurrentSave;
    private static bool _needToSave;

    // Пока в игре происходят какие-то действия, игрок не может ничего сделать
    // По окончанию этих действий игрок снова может что-то делать, а игра сохраняется. Если последовательность не была завершена - то игра не сохранится и откатится назад при след. загрузке

    public override int InitPriority => -1000;

    private static string GenerateJsonString() {
        CurrentSave.SavedDate = DateTime.Now.Date.ToString(CultureInfo.InvariantCulture);

        return JsonUtility.ToJson(CurrentSave, false);
    }

    public static void SaveGame() {
        _needToSave = true;
    }

    private static void RewriteGameSavedData() {
        string jsonData = GenerateJsonString();
        PlayerPrefs.SetString("saveProfile", jsonData);
    }

    private void LateUpdate() {
        if (_needToSave) {
            _needToSave = false;
            RewriteGameSavedData();
        }
    }

    public static void LoadGame() {
        if (PlayerPrefs.HasKey("saveProfile")) {
            string jsonData = PlayerPrefs.GetString("saveProfile");

            CurrentSave = JsonUtility.FromJson<GameSaveProfile>(jsonData);
            TryUpdateSave();
        } else {
            GenerateGame();
            Debug.Log("Generating finished. Saving started");
            RewriteGameSavedData();
            Debug.Log("New profile is saved");
        }
    }

    private static void TryUpdateSave() {
        if (string.IsNullOrEmpty(CurrentSave.Nickname)) {
            CurrentSave.Nickname = "Farmer #" + Random.Range(999, 10000);
        }
    }

    private static void GenerateGame() {
        CurrentSave = new GameSaveProfile() {
            Nickname = "Farmer #" + Random.Range(999, 10000)
        };
    }

    public static void ClearSave() {
        PlayerPrefs.DeleteKey("saveProfile");
    }
}