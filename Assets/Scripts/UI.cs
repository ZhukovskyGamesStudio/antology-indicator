using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour {
    public GameObject WinPanel, LosePanel, EscapePanel,OtherUi;
    public FirstPersonController FirstPersonController;
    bool canMove;
    bool canRotate;
    
    public void ShowLoseScreen() {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        LosePanel.SetActive(true);
        OtherUi.SetActive(false);
    }

    public void ShowWinScreen() {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        WinPanel.SetActive(true);
        OtherUi.SetActive(false);
    }

    private void Update() {
        if (WinPanel.activeSelf || LosePanel.activeSelf) {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            EscapePanel.gameObject.SetActive(!EscapePanel.gameObject.activeSelf);

            if (EscapePanel.activeSelf) {
                canMove = FirstPersonController.playerCanMove;
                canRotate = FirstPersonController.cameraCanMove;
                FirstPersonController.playerCanMove = false;
                FirstPersonController.cameraCanMove = false;
            } else {
                FirstPersonController.playerCanMove = canMove;
                FirstPersonController.cameraCanMove = canRotate;
            }
            
            
            Time.timeScale = EscapePanel.gameObject.activeSelf ? 0 : 1;
            //AudioListener.volume = EscapePanel.gameObject.activeSelf ? 0.2f : 1;
            OtherUi.SetActive(!EscapePanel.gameObject.activeSelf);
            Cursor.visible = EscapePanel.gameObject.activeSelf;
            Cursor.lockState = EscapePanel.gameObject.activeSelf ? CursorLockMode.None : CursorLockMode.Locked;
        }
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
        Restart();
    }
}