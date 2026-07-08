using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour {
    public CanvasGroup FadePanel;
    public GameObject LosePanel, EscapePanel, TaskPanel, OtherUi, TalkUi;
    public FirstPersonController FirstPersonController;
    public GameObject Crosshair;
    public WinPanel WinPanel;
    public BarsPanel BarsPanel;
    private bool canMove;
    private bool canRotate;

    public void ShowLoseScreen() {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        LosePanel.SetActive(true);
        OtherUi.SetActive(false);
        TalkUi.SetActive(false);
        TaskPanel.SetActive(false);
    }

    public void ShowTitlesScreen() {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        WinPanel.gameObject.SetActive(true);
        TaskPanel.SetActive(false);
        OtherUi.SetActive(false);
        Crosshair.gameObject.SetActive(false);
    }

    private void Update() {
        if (WinPanel.gameObject.activeSelf || LosePanel.activeSelf) {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) {
            OnPressedEsc();
        }

        if (!MadnessManager.instance.IsVolumesFixed) {
            BarsPanel.SetClicking(MadnessManager.instance.ClickingPower/100f);
            BarsPanel.SetHumming(MadnessManager.instance.HummingPower/100f);
        } else {
            BarsPanel.SetClicking(1f);
            BarsPanel.SetHumming(1f);
        }
     
    }

    public async UniTask ShowFade(float fin, float duration) {
        FadePanel.gameObject.SetActive(true);
        FadePanel.blocksRaycasts = fin > 0f;

        await FadePanel.DOFade(fin, duration).SetUpdate(true).WithCancellation(this.GetCancellationTokenOnDestroy());

        if (Mathf.Approximately(fin, 0f)) {
            FadePanel.gameObject.SetActive(false);
        }
    }

    public void OnPressedEsc() {
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
        TalkUi.SetActive(!EscapePanel.gameObject.activeSelf);
        TaskPanel.SetActive(!EscapePanel.gameObject.activeSelf);
        HUD.instance.UpdateHammer();
        Cursor.visible = EscapePanel.gameObject.activeSelf;
        Cursor.lockState = EscapePanel.gameObject.activeSelf ? CursorLockMode.None : CursorLockMode.Locked;
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

    public void ChangeLanguage(string langCode) {
        Language.ChangeLanguage(Enum.Parse<LangCode>(langCode));
    }
}

