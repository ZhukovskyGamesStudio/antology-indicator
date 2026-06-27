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
#if !UNITY_EDITOR
       StartingChapter = 0;
#endif
    }

    private void Start() {
        playerMovement.playerCanMove = false;
        hintUI.ShowHint("");
        UI.TaskPanel.SetActive(false);
        tasksUI.ShowTask("");
        madnessManager.IsMadnessRaising = false;
        madnessManager.IsVolumesFixed = true;
        storyObjectsContainer.WardrobeAnim.GetComponent<InteractiveObj>().enabled = false;

        storyObjectsContainer.FakeRadioWire.SetActive(true);
        storyObjectsContainer.KitchenWire.SetActive(false);
        storyObjectsContainer.LampWire.SetActive(false);
        storyObjectsContainer.ChipOnTable.gameObject.SetActive(false);
        
        storyObjectsContainer.Watertap.enabled = false;
        storyObjectsContainer.FridgeDoor.enabled = false;
        storyObjectsContainer.MicrowaveDoor.enabled = false;
        storyObjectsContainer.RadioOnOff.enabled = false;
        storyObjectsContainer.RadioChange.enabled = false;
        foreach (BlendItem blendItem in FindObjectsByType<BlendItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            blendItem.GetComponent<InteractiveObj>().enabled = false;
            blendItem.Blend(true);
        }

        foreach (InteractiveObj dust in storyObjectsContainer.SneezeObjects) {
            dust.enabled = false;
        }

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
        playerMovement.playerCanMove = true;
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
        
        if (currentChapter <= 4) {
            await FinalChapter();
            Save(5);
        }

        await WinChapter();
    }

    private async UniTask TableChapter() {
        playerMovement.playerCanMove = false;

        book.TogglePick();

        //поработать над анимацией вступления
        await UniTask.WaitForSeconds(4f);
        await TalkUI.Say("\"В мемах есть гипнотическое послание...\"\nХах, ну и бред. Кто вообще верит в такое?");
        await TalkUI.Say("\"Такое только мой дядька и читает.\n  Атлантида!.. Планета нибиру...");
        await TalkUI.Say("\"Сам то в больницу уехал - как на похороны, жутко ему от врачей..\n А я после перелома как новенький!");
        await TalkUI.Say("\"Поразвлекаюсь пока с его книжками, всё равно он нескоро вернётся.");

        await TalkUI.Say("Надоели уже эти подкасты, включу ка я лоу-фай");
        storyObjectsContainer.RadioChange.enabled = true;
        
        UI.TaskPanel.SetActive(true);
        tasksUI.ShowTask("Найдите музыкальную волну " + (EventsLogged.Any(l => l == "BookPicked") ? "(<b>ПКМ</b> положить предмет)" : ""));
        await UniTask.WaitForSeconds(1.5f);

        await UniTask.WaitUntil(() => EventsLogged.All(l => l != "BookPicked"));
        hintUI.Hide();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioMusic"));
        storyObjectsContainer.RadioChange.enabled = false;
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);

        tasksUI.ShowTask("Подпевайте радиостанции (<b>E</b>)");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "Hummed"));
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);

        tasksUI.ShowTask("Щёлкайте в ритм радиостанции (<b>Q</b>)");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "Clicked"));
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);

        await TalkUI.Say("Найду лучше что-то нейтральное");

        tasksUI.ShowTask("Найдите расслабляющую волну");
        storyObjectsContainer.RadioChange.enabled = true;
        await UniTask.WaitForSeconds(0.5f);
        EventsLogged.Clear();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioMusic2"));
        tasksUI.CompleteTask();
        TalkUI.Say("То что нужно").Forget();
        tasksUI.ShowTask("Продолжите читать");
        storyObjectsContainer.RadioChange.enabled = false;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioNoise"));

        madnessManager.IsVolumesFixed = false;
        madnessManager.IsMadnessRaising = true;
        madnessManager.TmpMaxMadness = 25;

        await TalkUI.Say("Старое барахло, постоянно ломается");
        tasksUI.ShowTask("Восстановите волну");
        storyObjectsContainer.RadioChange.enabled = true;
        EventsLogged.Clear();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l == "RadioSwitched") >= 2);
        madnessManager.TmpMaxMadness = 35;

        TalkUI.Say("Хм, странно").Forget();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l == "RadioSwitched") >= 4);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1f);
        madnessManager.TmpMaxMadness = 45;

        await TalkUI.Say("Ладно, почитаю в тишине");
        tasksUI.ShowTask("Выключите радио");
        storyObjectsContainer.RadioOnOff.enabled = true;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioDisabled"));
        tasksUI.CompleteTask();
        await TalkUI.Say("Что?! Почему оно работает?");
        madnessManager.TmpMaxMadness = 55;
        madnessManager.IsMadnessRaising = false;

        await TalkUI.Say("Голова начинает кружится, надо отвлечься");

        tasksUI.ShowTask("Отвлекитеcь от шума (<b>Q</b>) или (<b>E</b>)");
        await UniTask.WaitUntil(() => madnessManager.Madness < 10);
        madnessManager.IsMadnessRaising = true;
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);
    }

    private async UniTask ElecticityChapter() {
        madnessManager.TmpMaxMadness = 100;
        await TalkUI.Say("Может кнопка выключения сломалась?");

        playerMovement.playerCanMove = true;
        tasksUI.ShowTask("Отключите радио от питания (WASD)");
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

        await TalkUI.Say("Хм, я же точно видел, это радио-провод...");

        tasksUI.ShowTask("Отключите РАДИО от питания");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "KitchenDisabled"));
        tasksUI.CompleteTask();
        storyObjectsContainer.KitchenWire.SetActive(false);

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
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 1);
        tasksUI.ShowTask("Избавьтесь от шума (1 из 3)");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 2);
        tasksUI.ShowTask("Избавьтесь от шума (2 из 3)");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 3);
        tasksUI.ShowTask("Избавьтесь от шума (3 из 3)");
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(5f);

        storyObjectsContainer.Watertap.enabled = false;
        storyObjectsContainer.FridgeDoor.enabled = false;
        storyObjectsContainer.MicrowaveDoor.enabled = false;

        madnessManager.TmpMaxMadness = 100;

        radioAudio.gameObject.SetActive(true);
    }

    private async UniTask BreakRadioChapter() {
        await TalkUI.Say("Снова оно... Как же раскалывается головааа...");
        tasksUI.ShowTask("Найдите способ остановить радио");

        foreach (BlendItem VARIABLE in FindObjectsByType<BlendItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if(VARIABLE.CompareTag("Sneeze")) {
                continue;
            }
            VARIABLE.GetComponent<InteractiveObj>().enabled = true;
            VARIABLE.Blend(false);
            
        }

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "FakeHammer"));
        storyObjectsContainer.WardrobeAnim.Play();
        storyObjectsContainer.WardrobeAnim.GetComponent<InteractiveObj>().enabled = true;
        await UniTask.WaitForSeconds(0.5f);
        await TalkUI.Say("Померещилось, но кажется молоток был в прихожей");

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "FakeUmbrella"));

        HUD.SetHammer(true);
        storyObjectsContainer.NormalRooms.SetActive(false);
        storyObjectsContainer.LabirintRooms.SetActive(true);
        TeleportRadio();

        await UniTask.WaitForSeconds(1f);
        await TalkUI.Say("Пора с ним кончать");

        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("RadioHit")) >= 1);
        TalkUI.Say("Заткнись").Forget();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("RadioHit")) >= 3);
        TalkUI.Say("Заткнись, заткнись, заткнись").Forget();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioBroken"));

        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(2.5f);
        await TalkUI.Say("АААААААА, неееееет.");
        await UniTask.WaitForSeconds(2f);
    }

    private async UniTask ChipChapter() {
       
        tasksUI.ShowTask("Прочитайте записку.");
        
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "NoteFound"));
        tasksUI.CompleteTask();
        
        await UniTask.WaitForSeconds(1.5f);
        tasksUI.ShowTask("Найдите как избавится от чипа.");
        
        foreach (InteractiveObj dust in storyObjectsContainer.SneezeObjects) {
            dust.enabled = true;
        }

        Openable.IsChangeAllowed = true;
        
        await UniTask.WaitUntil(() => EventsLogged.Count(IsSneezeItem) >= 1);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);
        tasksUI.ShowTask("Заставьте себя чихнуть (1 из 3)");
      
        await UniTask.WaitUntil(() => EventsLogged.Count(IsSneezeItem) >= 2);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);
        tasksUI.ShowTask("Заставьте себя чихнуть  (2 из 3)");
        await UniTask.WaitUntil(() => EventsLogged.Count(IsSneezeItem) >= 3);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f);
        tasksUI.ShowTask("Заставьте себя чихнуть  (3 из 3)");
        tasksUI.CompleteTask();
        
        playerMovement.playerCanMove = false;
        storyObjectsContainer.BookMoved.gameObject.SetActive(true);
        storyObjectsContainer.BookUnmoved.gameObject.SetActive(false);
        HUD.TriggerSneeze();
        //teleport player to starting pos
        
        madnessManager.IsMadnessRaising = false;
        madnessManager.DropMadness(3f).Forget();
        await UniTask.WaitForSeconds(6f);
    }


    private bool IsSneezeItem(string l) {
        return l.Contains("PepperDust") || l.Contains("Earstick") || l.Contains("Feather");
    }

    private async UniTask FinalChapter() {
        playerMovement.playerCanMove = false;
        storyObjectsContainer.BookMoved.gameObject.SetActive(true);
        storyObjectsContainer.BookUnmoved.gameObject.SetActive(false);
        
        
        storyObjectsContainer.ChipOnTable.gameObject.SetActive(true);
        storyObjectsContainer.ChipOnTable.PickUp();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "ChipPuttedAway"));
        await TalkUI.Say("Теперь они меня не отследят");
        
        tasksUI.ShowTask("Отправьтесь к дяде.");
        playerMovement.playerCanMove = true;
        await TalkUI.Say("надо добраться до дяди, и поскорее");
        storyObjectsContainer.ApartmentsExit.enabled = true;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "ApartmentsExit"));
        
        //await TalkUI.Say("не помню как запирал дверь, но ключ точно где-то рядом");
        //await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "ApartmentsOpened"));
    }
    
    
   

    private async UniTask WinChapter() {
        playerMovement.playerCanMove = false;
        await UI.ShowFade(1, 0.5f);
        UI.ShowTitlesScreen();
        storyObjectsContainer.TitlesAnimation.Play();
        UI.WinPanel.SetText("Вы спасли свой разум!");
        await UniTask.WaitForSeconds(1f);
        await UI.ShowFade(0, 3f);
        await UniTask.WaitForSeconds(10f);
        UI.WinPanel.SetText("Вы спасли свой разум?");
    }

    public async UniTask Lose() {
        _deathCts?.Cancel();
        playerMovement.playerCanMove = false;
        HUD.TriggerDeath();
        await UniTask.WaitForSeconds(5f);
        UI.ShowLoseScreen();
    }
    
    private void TeleportRadio() {
        storyObjectsContainer.Radio.transform.SetParent(storyObjectsContainer.NextRadioPoint.transform);
        storyObjectsContainer.Radio.transform.localPosition = Vector3.zero;
        storyObjectsContainer.Radio.transform.localRotation = Quaternion.identity;
    }
    public void TeleportRadioBack() {
        storyObjectsContainer.Radio.transform.SetParent(storyObjectsContainer.FirstRadioPoint.transform);
        storyObjectsContainer.Radio.transform.localPosition = Vector3.zero;
        storyObjectsContainer.Radio.transform.localRotation = Quaternion.identity;
        storyObjectsContainer.Lamp.Set(true);
        storyObjectsContainer.LampEmission.Set(true);
    }
}