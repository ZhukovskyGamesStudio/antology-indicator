using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class StoryManager : MonoBehaviour {
    public FirstPersonController playerMovement;
    public UI UI;
    private CancellationTokenSource _deathCts;

    public List<string> EventsLogged;
    public TasksUI tasksUI;

    public Pickable book;
    public HintUI hintUI;
    public static StoryManager instance;
    public TalkUI TalkUI;

    public RadioAudio radioAudio;
    public MadnessManager madnessManager;
    public StoryObjectsContainer storyObjectsContainer;
    public HUD HUD;
    public bool isDropProgress = true;
    public int StartingChapter;

    private void Awake() {
        instance = this;
    }

    private void Start() {
        hintUI.ShowHint("");
        tasksUI.ShowTask("");
        madnessManager.IsMadnessRaising = false;
        madnessManager.IsVolumesFixed = true;

        storyObjectsContainer.FakeRadioWire.SetActive(true);
        storyObjectsContainer.KitchenWire.SetActive(false);
        storyObjectsContainer.LampWire.SetActive(false);

        storyObjectsContainer.Watertap.enabled = false;
        storyObjectsContainer.FridgeDoor.enabled = false;
        storyObjectsContainer.MicrowaveDoor.enabled = false;
        storyObjectsContainer.RadioOnOff.enabled = false;
        
        
        FirstPersonController.isHolding = false;

        if (isDropProgress) {
            PlayerPrefs.SetInt("Chapter", StartingChapter);
        }

        LoadSave();
        Story().Forget();
    }

    private void LoadSave() {
        int chapter = PlayerPrefs.GetInt("Chapter", 0);
    }

    private void Save(int chapter) {
        PlayerPrefs.SetInt("Chapter", chapter);
    }

    public void LogEvent(string eventName) {
        EventsLogged.Add(eventName);
    }

    public void LogOnce(string eventName) {
        if (EventsLogged.Contains(eventName)) {
            return;
        }

        EventsLogged.Add(eventName);
    }

    public void LogClear(string eventName) {
        if (EventsLogged.Contains(eventName)) {
            EventsLogged.Remove(eventName);
        }
    }

    private async UniTask Story() {
        playerMovement.enabled = true;
        int currentChapter = PlayerPrefs.GetInt("Chapter", 0);
        if (currentChapter <= 0) {
            await TableChapter();
            Save(1);
        }

        if (currentChapter <= 1) {
            await ElecticityChapter();
            Save(2);
        }

        if (currentChapter <= 2) {
            await BreakRadioChapter();
            Save(3);
        }

        if (currentChapter <= 3) {
            await ChipChapter();
            Save(4);
        }

        await WinChapter();
    }

    private async UniTask TableChapter() {
        playerMovement.playerCanMove = false;

        book.TogglePick();
        
        //поработать над анимацией вступления
        
        await UniTask.WaitForSeconds(3f);
        TalkUI.Say("Надоели уже эти подкасты, включу ка я лоу-фай");
        await UniTask.WaitForSeconds(3f);
        tasksUI.ShowTask("Найдите музыкальную волну" + (EventsLogged.Any(l => l == "BookPicked")? "[<b>ПКМ</b> положить предмет]" :""));
        await UniTask.WaitForSeconds(1.5f);
        
        await UniTask.WaitUntil(() => EventsLogged.All(l => l != "BookPicked"));
        hintUI.Hide();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioMusic"));
        storyObjectsContainer.RadioChange.enabled = false;
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);

        tasksUI.ShowTask("Подпевайте радиостанции [<b>E</b>]");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "Hummed"));
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);

        tasksUI.ShowTask("Щёлкайте в ритм радиостанции [<b>Q</b>]");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "Clicked"));
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);

        TalkUI.Say("Найду лучше что-то нейтральное");

        tasksUI.ShowTask("Найдите расслабляющую волну");
        storyObjectsContainer.RadioChange.enabled = true;
        await UniTask.WaitForSeconds(0.5f);
        EventsLogged.Clear();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioMusic2"));
        tasksUI.CompleteTask();
        TalkUI.Say("То что нужно");
        storyObjectsContainer.RadioChange.enabled = false;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioNoise"));
    
        madnessManager.IsVolumesFixed = false;
        madnessManager.IsMadnessRaising = true;
        madnessManager.TmpMaxMadness = 25;

        TalkUI.Say("Старое барахло, постоянно ломается");
        await UniTask.WaitForSeconds(1f);
        tasksUI.ShowTask("Восстановите волну");
        storyObjectsContainer.RadioChange.enabled = true;
        EventsLogged.Clear();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l == "RadioSwitched") >= 2);
        madnessManager.TmpMaxMadness = 35;

        TalkUI.Say("Хм, странно");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l == "RadioSwitched") >= 4);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1f);
        madnessManager.Madness += 10;
        madnessManager.TmpMaxMadness = 45;

        TalkUI.Say("Ладно, почитаю в тишине");
        await UniTask.WaitForSeconds(1f);
        tasksUI.ShowTask("Выключите радио");
        storyObjectsContainer.RadioOnOff.enabled = true;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioDisabled"));
        tasksUI.CompleteTask();
        TalkUI.Say("Что?! Почему оно работает?");
        madnessManager.Madness += 10;
        madnessManager.TmpMaxMadness = 55;
        madnessManager.IsMadnessRaising = false;
        await UniTask.WaitForSeconds(1.5f);

        TalkUI.Say("Голова начинает кружится, надо отвлечься");

        tasksUI.ShowTask("Отвлекитеcь от шума [<b>Q</b>] или [<b>E</b>]");
        await UniTask.WaitUntil(() => madnessManager.Madness < 10);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);
    }

    private async UniTask ElecticityChapter() {
        madnessManager.TmpMaxMadness = 100;
        playerMovement.playerCanMove = true;
        TalkUI.Say("Может кнопка выключения сломалась?");
        await UniTask.WaitForSeconds(0.5f);
        
        tasksUI.ShowTask("Отключите радио от питания");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "LampDisabled"));
        tasksUI.CompleteTask();

        //Отключился свет и переключился провод
        storyObjectsContainer.FakeRadioWire.SetActive(false);
        storyObjectsContainer.KitchenWire.SetActive(true);
        storyObjectsContainer.LampWire.SetActive(true);
        storyObjectsContainer.Lamp.Set(false);
        storyObjectsContainer.LampEmission.Set(false);
        storyObjectsContainer.LampInteractive.enabled = false;
        await UniTask.WaitForSeconds(1.5f);

        TalkUI.Say("Хм, я же точно видел, это радио-провод...");
        
        await UniTask.WaitForSeconds(1.5f);
        tasksUI.ShowTask("Отключите РАДИО от питания");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "KitchenDisabled"));
        tasksUI.CompleteTask();

        storyObjectsContainer.Watertap.enabled = true;
        storyObjectsContainer.FridgeDoor.enabled = true;
        storyObjectsContainer.MicrowaveDoor.enabled = true;
        storyObjectsContainer.Watertap.enabled = true;
        storyObjectsContainer.KitchenWater.SetActive(true);
        storyObjectsContainer.KitchenWater.SetActive(true);
        radioAudio.gameObject.SetActive(false);
        madnessManager.TmpMaxMadness = 50;
        storyObjectsContainer.microwaveAnim.Play();
        storyObjectsContainer.FridgeAnim.Play();
        storyObjectsContainer.fridgeOpen.Play();

        await UniTask.WaitForSeconds(1.5f);

        tasksUI.ShowTask("Избавьтесь от шума");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 3);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(5f);

        storyObjectsContainer.Watertap.enabled = false;
        storyObjectsContainer.FridgeDoor.enabled = false;
        storyObjectsContainer.MicrowaveDoor.enabled = false;

        madnessManager.TmpMaxMadness = 100;
        radioAudio.gameObject.SetActive(true);
    }

    private async UniTask BreakRadioChapter() {
        TalkUI.Say("Снова оно... Как же раскалывается головааа...");
        await UniTask.WaitForSeconds(1.5f);
        tasksUI.ShowTask("Найдите способ остановить радио");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "FakeHammer"));
        await UniTask.WaitForSeconds(0.5f);
        TalkUI.Say("Померещилось, но кажется молоток был в прихожей");

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "FakeUmbrella"));

        HUD.SetHammer(true);
        storyObjectsContainer.NormalRooms.SetActive(false);
        storyObjectsContainer.LabirintRooms.SetActive(true);
        TeleportRadio();
        
        await UniTask.WaitForSeconds(1f);
        TalkUI.Say("Пора с ним кончать");

        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("RadioHit")) >= 1);
        TalkUI.Say("Заткнись");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("RadioHit")) >= 3);
        TalkUI.Say("Заткнись, заткнись, заткнись");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioBroken"));
        
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1f);
        TalkUI.Say("АААААААА, неееееет. *звуки отчаяния*");
        await UniTask.WaitForSeconds(1f);
    }

    private async UniTask ChipChapter() {
        TalkUI.Say("Что это за бумажка?");

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "NoteFound"));
        tasksUI.ShowTask("Избавьтесь от чипа.");

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "PepperFound"));
        await UniTask.WaitForSeconds(5f);
    }

    private void TeleportRadio() {
        storyObjectsContainer.Radio.transform.SetParent(storyObjectsContainer.NextRadioPoint.transform);
        storyObjectsContainer.Radio.transform.localPosition = Vector3.zero;
        storyObjectsContainer.Radio.transform.localRotation = Quaternion.identity;
    }

    private async UniTask WinChapter() {
        await UniTask.WaitForSeconds(1.5f);
        playerMovement.enabled = false;
        UI.ShowWinScreen();
    }

    public void Lose() {
        _deathCts?.Cancel();
        playerMovement.enabled = false;
        UI.ShowLoseScreen();
    }
}