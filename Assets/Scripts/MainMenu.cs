using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour {
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