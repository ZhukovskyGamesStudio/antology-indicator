using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour {
    public void Play() {
        SceneManager.LoadScene(2);
    }

    public void Rate() {
        Application.OpenURL("https://grafenters.itch.io/zhukovsky-games");
    }

    public void Exit() {
        Application.Quit();
    }
}