using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour {
    private void Awake() {
        // isHolding — статик, и он переживает и смену сцены, и (в редакторе) выход
        // из плей-мода. Если игра закончилась с предметом в руках, в меню он бы
        // блокировал вообще все клики по предметам: InteractiveObj молча выходит,
        // пока «что-то в руках». В игре это чинит StoryManager, меню чинит себя само.
        FirstPersonController.isHolding = false;
    }

    private void Start() {
        // Коллекция книг переживает прохождения и живёт в PlayerPrefs, а достижение
        // могло не открыться: собирали до появления Steam-версии или без запущенного
        // Steam. Добираем именно в меню — здесь собранные книги лежат на столе
        // перед игроком, и плашка Steam приходится к месту. Повторный Unlock
        // на уже открытом достижении безвреден.
        if (BookCollection.IsComplete) {
            SteamAchievements.Unlock(SteamAchievements.AllBooks);
        }
    }

    public void Play() {
        SceneManager.LoadScene("GameScene");
    }
    
    public void ChangeLanguage(string langCode) {
        Language.ChangeLanguage(Enum.Parse<LangCode>(langCode));
    }

    public void Exit() {
        Application.Quit();
    }
}