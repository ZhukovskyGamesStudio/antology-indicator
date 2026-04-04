using UnityEngine;
using UnityEngine.SceneManagement;

public class UI: MonoBehaviour {


    public GameObject WinPanel, LosePanel;
    
    public void ShowLoseScreen() {
        LosePanel.gameObject.SetActive(true);
    }
    public void ShowWinScreen() {
        WinPanel.gameObject.SetActive(true);
    }

    public void Restart() {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LeaveALike() {
        Application.OpenURL("https://grafenters.itch.io/zhukovsky-games");
    }

    public void ExitGame() {
        Application.Quit();
    }

    public void DropProgress() {
        PlayerPrefs.SetInt("Chapter", 0);
    }
    
}
