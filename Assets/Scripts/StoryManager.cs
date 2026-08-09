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

    /// <summary>Открыт ли выход из квартиры (ставится в финале). До этого дверь реагирует «нет ключей».</summary>
    public static bool CanExit;

    public TalkUI TalkUI;

    public RadioAudio radioAudio;
    public MadnessManager madnessManager;
    public StoryObjectsContainer storyObjectsContainer;
    public HUD HUD;

    [Header("Финал: моргание света под подмену знака в заголовке")]
    [Tooltip("Сколько раз моргнуть светом в самом конце")]
    public int FinalFlickers = 3;

    [Tooltip("Длительность тёмной фазы моргания")]
    public float FinalFlickerDark = 0.12f;

    [Tooltip("Длительность светлой фазы между морганиями")]
    public float FinalFlickerLit = 0.35f;

    [Header("Финал (диалог ГГ и дяди)")]
    [Tooltip("Уличный эмбиент, играет фоном во время титров/диалога")]
    public AudioSource finalOutdoors;
    [Tooltip("Шаги вбегающего ГГ в начале диалога")]
    public AudioSource finalSteps;

    public bool isDropProgress = true;
    public int StartingChapter;

    // Токен жизни этого StoryManager: все ожидания сценария привязаны к нему.
    // Без него Story() прошлого запуска переживала смену сцены и продолжала
    // тикать «зомби»-цепочкой: трогала уничтоженные объекты и могла писать
    // PlayerPrefs["Chapter"] поверх нового прохождения.
    private CancellationToken _lifetimeCt;

    private void Awake() {
        instance = this;
        _lifetimeCt = this.GetCancellationTokenOnDestroy();
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
        SetPuddles(false);

        FirstPersonController.isHolding = false;
        CanExit = false;

        // Статика, которая переживает смену сцены: из финала игрок теперь уходит
        // в главное меню, а не из приложения, поэтому второй заход должен
        // начинаться с чистого листа.
        MonoBehLogger.ResetReactions();
        Openable.ResetRunState();
        ReactionZone.ResetSaid();
        PuddleZone.ResetSaid();

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
        await UniTask.WaitForSeconds(4f, cancellationToken: _lifetimeCt);
        await TalkUI.Say("\"Феномен меметического гипноза...\"\nХах, ну и бред. Кто вообще верит в такое?");
        // Плашки «Пропустить текст (Пробел)» в углу больше нет: проматыванию учит
        // сама иконка пробела, которая появляется рядом с дочитанной репликой
        // (см. TalkUI.ShowSpaceIcon).
        await TalkUI.Say("Такое только мой дядька и читает.\nАтлантида!.. Планета Нибиру...");
        await TalkUI.Say("Сам-то в больницу уехал — как на похороны, жутко ему от врачей...\nА я после операции как новенький!");
        await TalkUI.Say("Поразвлекаюсь пока с его книжками, всё равно он нескоро вернётся.");

        await TalkUI.Say("Надоели уже эти подкасты, включу-ка я лоу-фай");
        storyObjectsContainer.RadioChange.enabled = true;
        
        UI.TaskPanel.SetActive(true);
        tasksUI.ShowTask(EventsLogged.Any(l => l == "BookPicked")
            ? "Найдите музыкальную волну (<b>ЛКМ</b> положить предмет)"
            : "Найдите музыкальную волну");
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);

        await UniTask.WaitUntil(() => EventsLogged.All(l => l != "BookPicked"), cancellationToken: _lifetimeCt);
        hintUI.Hide();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioMusic"), cancellationToken: _lifetimeCt);
        storyObjectsContainer.RadioChange.enabled = false;
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);

        tasksUI.ShowTask("Подпевайте радиостанции (<b>E</b>)");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "Hummed"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);

        tasksUI.ShowTask("Щёлкайте в ритм радиостанции (<b>Q</b>)");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "Clicked"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);

        await TalkUI.Say("Найду лучше что-то нейтральное");

        tasksUI.ShowTask("Найдите расслабляющую волну");
        storyObjectsContainer.RadioChange.enabled = true;
        await UniTask.WaitForSeconds(0.5f, cancellationToken: _lifetimeCt);
        EventsLogged.Clear();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioMusic2"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        TalkUI.Say("То что нужно").Forget();

        // Обучение вращению: игрок сам берёт книгу (клик) и рассматривает обложку.
        tasksUI.ShowTask("Посмотрите обложку (вращать предмет с зажатой <b>ЛКМ</b>)");
        TalkUI.Say("Кто это написал вообще?").Forget();
        storyObjectsContainer.RadioChange.enabled = false;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "BookRotated"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1f, cancellationToken: _lifetimeCt);

        // Книгу игрок кладёт сам (клик ЛКМ), когда захочет вернуться к радио.
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioNoise"), cancellationToken: _lifetimeCt);

        madnessManager.IsVolumesFixed = false;
        madnessManager.IsMadnessRaising = true;
        madnessManager.TmpMaxMadness = 25;

        await TalkUI.Say("Старое барахло, постоянно ломается");
        tasksUI.ShowTask("Восстановите волну");
        storyObjectsContainer.RadioChange.enabled = true;
        EventsLogged.Clear();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l == "RadioSwitched") >= 2, cancellationToken: _lifetimeCt);
        madnessManager.TmpMaxMadness = 35;

        TalkUI.Say("Хм, странно").Forget();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l == "RadioSwitched") >= 4, cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1f, cancellationToken: _lifetimeCt);
        madnessManager.TmpMaxMadness = 45;

        await TalkUI.Say("Ладно, почитаю в тишине");
        tasksUI.ShowTask("Выключите радио");
        storyObjectsContainer.RadioOnOff.enabled = true;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioDisabled"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await TalkUI.Say("Что?! Почему оно работает?");
        madnessManager.TmpMaxMadness = 55;
        madnessManager.IsMadnessRaising = false;

        await TalkUI.Say("Голова начинает кружиться, надо отвлечься");

        tasksUI.ShowTask("Отвлекитесь от шума (<b>Q</b>) или (<b>E</b>)");
        await UniTask.WaitUntil(() => madnessManager.Madness < 10, cancellationToken: _lifetimeCt);
        madnessManager.IsMadnessRaising = true;
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);
    }

    private async UniTask ElecticityChapter() {
        madnessManager.TmpMaxMadness = 100;
        await TalkUI.Say("Может, кнопка выключения сломалась?");

        playerMovement.playerCanMove = true;
        tasksUI.ShowTask("Отключите радио от питания (WASD)");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "LampDisabled"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();

        //Отключился свет и переключился провод
        storyObjectsContainer.FakeRadioWire.SetActive(false);
        storyObjectsContainer.KitchenWire.SetActive(true);
        storyObjectsContainer.LampWire.SetActive(true);
        storyObjectsContainer.Lamp.Set(false);
        storyObjectsContainer.LampEmission.Set(false);
        storyObjectsContainer.LampInteractive.enabled = false;
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);

        await TalkUI.Say("Хм, я же точно видел, это радио-провод...");

        tasksUI.ShowTask("Отключите РАДИО от питания");
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "KitchenDisabled"), cancellationToken: _lifetimeCt);
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

        // FridgeWork держит дверцу открытой и идёт мимо Openable, поэтому его надо
        // об этом предупредить. Иначе первый клик игрока по холодильнику считает
        // дверцу закрытой: она рывком захлопывается и открывается заново, а шум при
        // этом засчитывается как убранный — хотя холодильник остался открыт и гудит.
        Openable fridge = storyObjectsContainer.FridgeDoor.GetComponent<Openable>();
        if (fridge != null) {
            fridge.SetOpenState(true);
        }

        TalkUI.Say("Ааа! Как громко!").Forget();

        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);

        tasksUI.ShowTask("Избавьтесь от шума");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 1, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Избавьтесь от шума (1 из 3)");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 2, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Избавьтесь от шума (2 из 3)");
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("KitchenNoise")) >= 3, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Избавьтесь от шума (3 из 3)");
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(5f, cancellationToken: _lifetimeCt);

        storyObjectsContainer.Watertap.enabled = false;
        storyObjectsContainer.FridgeDoor.enabled = false;
        storyObjectsContainer.MicrowaveDoor.enabled = false;

        madnessManager.TmpMaxMadness = 100;

        radioAudio.gameObject.SetActive(true);
    }

    private async UniTask BreakRadioChapter() {
        await TalkUI.Say("Снова оно... Как же раскалывается головааа...");
        tasksUI.ShowTask("Найдите способ остановить радио");

        // Зонт в шкафу (FakeUmbrella) — тоже BlendItem, но его интерактив здесь
        // НЕ включается: событие FakeUmbrella логается при опускании, и если игрок
        // доберётся до зонта раньше обманки-молотка (капсула зонта торчит из
        // закрытого шкафа), рельса главы сложится задом наперёд — «молоток»
        // исчезнет на глазах, а подъём обманки мгновенно доиграет всю секвенцию.
        // Включаем зонт вместе с открытием шкафа, ниже.
        List<InteractiveObj> umbrellaInteractives = new();
        foreach (BlendItem VARIABLE in FindObjectsByType<BlendItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if(VARIABLE.CompareTag("Sneeze")) {
                continue;
            }

            VARIABLE.Blend(false);
            InteractiveObj interactive = VARIABLE.GetComponent<InteractiveObj>();
            if (VARIABLE.name.Contains("FakeUmbrella")) {
                umbrellaInteractives.Add(interactive);
                continue;
            }

            interactive.enabled = true;
        }

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "FakeHammer"), cancellationToken: _lifetimeCt);
        storyObjectsContainer.WardrobeAnim.Play();
        storyObjectsContainer.WardrobeAnim.GetComponent<InteractiveObj>().enabled = true;
        foreach (InteractiveObj umbrella in umbrellaInteractives) {
            umbrella.enabled = true;
        }

        await UniTask.WaitForSeconds(0.5f, cancellationToken: _lifetimeCt);
        // Фраза про молоток в прихожей теперь звучит при каждом подъёме фейкового
        // молотка, пока нет настоящего (FakeHammer.OnPick -> ReactWhileNoHammer).

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "FakeUmbrella"), cancellationToken: _lifetimeCt);

        // NormalRooms сейчас погаснет целиком. Если игрок в этот момент держит
        // предмет из её иерархии (обманку-молоток), тот выключится прямо в руках —
        // ждём пустых рук. Даже если игрок что-то и удержит, Pickable.OnDisable
        // подстрахует, но лучше не дёргать предмет у него из рук вовсе.
        await UniTask.WaitUntil(() => !FirstPersonController.isHolding, cancellationToken: _lifetimeCt);

        HUD.SetHammer(true);
        storyObjectsContainer.NormalRooms.SetActive(false);
        storyObjectsContainer.LabirintRooms.SetActive(true);
        TeleportRadio();

        await UniTask.WaitForSeconds(1f, cancellationToken: _lifetimeCt);
        // «А ванная где..?» отсюда убрана: в этот момент дверь в лабиринт ещё
        // заперта на замок, и реплика звучала до того, как игрок вообще увидел
        // подмену. Теперь её говорит ReactionZone в LabirintRooms/midhallway —
        // когда игрок сбил замок, открыл дверь и вошёл внутрь.
        await TalkUI.Say("Пора с ним кончать");

        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("RadioHit")) >= 1, cancellationToken: _lifetimeCt);
        TalkUI.Say("Заткнись").Forget();
        await UniTask.WaitUntil(() => EventsLogged.Count(l => l.Contains("RadioHit")) >= 3, cancellationToken: _lifetimeCt);
        TalkUI.Say("Заткнись, заткнись, заткнись").Forget();
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "RadioBroken"), cancellationToken: _lifetimeCt);

        tasksUI.CompleteTask();
        // Пауза на осознание. Реплика «АААААААА, неееееет.» убрана — на слух она
        // читалась карикатурно, тишина после удара работает лучше.
        await UniTask.WaitForSeconds(4.5f, cancellationToken: _lifetimeCt);
    }

    private async UniTask ChipChapter() {
       
        tasksUI.ShowTask("Прочитайте записку");
        
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "NoteFound"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Найдите, как избавиться от чипа");
        
        foreach (InteractiveObj dust in storyObjectsContainer.SneezeObjects) {
            dust.enabled = true;
        }

        Openable.IsChangeAllowed = true;
        SetPuddles(true);

        // Подсказка, если игрок долго тупит и не находит предметы для чихания.
        // Токен связан с жизнью StoryManager: иначе цикл переживал бы уход в меню
        // и раз в 40 секунд дёргал уничтоженный TalkUI.
        CancellationTokenSource sneezeHintCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCt);
        SneezeHintLoop(sneezeHintCts.Token).Forget();

        await UniTask.WaitUntil(() => EventsLogged.Count(IsSneezeItem) >= 1, cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Заставьте себя чихнуть (1 из 3)");
      
        await UniTask.WaitUntil(() => EventsLogged.Count(IsSneezeItem) >= 2, cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Заставьте себя чихнуть (2 из 3)");
        await UniTask.WaitUntil(() => EventsLogged.Count(IsSneezeItem) >= 3, cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await UniTask.WaitForSeconds(1.5f, cancellationToken: _lifetimeCt);
        tasksUI.ShowTask("Заставьте себя чихнуть (3 из 3)");
        tasksUI.CompleteTask();
        sneezeHintCts.Cancel();

        playerMovement.playerCanMove = false;
        storyObjectsContainer.BookMoved.gameObject.SetActive(true);
        storyObjectsContainer.BookUnmoved.gameObject.SetActive(false);
        HUD.TriggerSneeze();
        //teleport player to starting pos
        
        madnessManager.IsMadnessRaising = false;
        madnessManager.DropMadness(3f).Forget();
        await UniTask.WaitForSeconds(6f, cancellationToken: _lifetimeCt);
    }

    private void SetPuddles(bool isActive) {
        foreach (GameObject puddle in storyObjectsContainer.Puddles) {
            puddle.SetActive(isActive);
        }

        // Лужа и текущий кран — одно событие: вода в ванной идёт ровно с того
        // момента, как из-под шторы натекло. Дальше кран в руках игрока — он его
        // перекроет, и лужи высохнут сами (RunningWater.StopAll -> Puddle.DryAll).
        if (storyObjectsContainer.BathroomWaters != null) {
            foreach (RunningWater water in storyObjectsContainer.BathroomWaters) {
                if (water != null) {
                    water.SetFlowing(isActive);
                }
            }
        }

        // Ванная за шторой открывается ровно тогда же, когда из-под двери натекает
        // лужа: до этого штору не сдвинуть, и лезть туда рано.
        if (storyObjectsContainer.BathroomCurtains != null) {
            foreach (InteractiveObj curtain in storyObjectsContainer.BathroomCurtains) {
                if (curtain != null) {
                    curtain.enabled = isActive;
                }
            }
        }
    }

    private bool IsSneezeItem(string l) {
        return l.Contains("PepperDust") || l.Contains("Earstick") || l.Contains("Feather");
    }

    /// <summary>
    /// Сколько предметов для чиханья игрок уже применил (1..3). По этому номеру
    /// <see cref="HUD.PlaySneeze"/> выбирает силу чиха.
    /// </summary>
    public int SneezeItemsUsed => EventsLogged.Count(IsSneezeItem);

    // Пока идёт этап чихания: если за интервал не появилось новых «чихательных»
    // предметов — ненавязчиво напоминаем, что искать. Останавливается по токену.
    private async UniTaskVoid SneezeHintLoop(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            int before = EventsLogged.Count(IsSneezeItem);
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(40), cancellationToken: token)
                .SuppressCancellationThrow();
            if (canceled || token.IsCancellationRequested) {
                return;
            }

            if (EventsLogged.Count(IsSneezeItem) == before && before < 3) {
                TalkUI.Say("Перец, ватная палочка, перо… Перец, ватная палочка, перо…").Forget();
            }
        }
    }

    private async UniTask FinalChapter() {
        playerMovement.playerCanMove = false;
        storyObjectsContainer.BookMoved.gameObject.SetActive(true);
        storyObjectsContainer.BookUnmoved.gameObject.SetActive(false);
        
        
        storyObjectsContainer.ChipOnTable.gameObject.SetActive(true);
        storyObjectsContainer.ChipOnTable.PickUp();
        await TalkUI.Say("...Всё закончилось?");
        await TalkUI.Say("Чип?! Я же думал, что это бред.");
        await TalkUI.Say("Что дядя просто фанатик…");
        await TalkUI.Say("Хах, чёрт. А если он был прав и к чему-то готовился?");
        tasksUI.ShowTask("Отложите чип");

        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "ChipPuttedAway"), cancellationToken: _lifetimeCt);
        tasksUI.CompleteTask();
        await TalkUI.Say("Всё. Чип оставлю, чтобы не отследили.");
        await TalkUI.Say("Потом посмотрим, что у него внутри…");
        await TalkUI.Say("Нужно к дяде. Срочно! Пока ему тоже не засунули чип в башку!");

        tasksUI.ShowTask("Отправьтесь к дяде");
        playerMovement.playerCanMove = true;
        storyObjectsContainer.ApartmentsExit.enabled = true;
        CanExit = true;
        await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "ApartmentsExit"), cancellationToken: _lifetimeCt);
        
        //await TalkUI.Say("не помню как запирал дверь, но ключ точно где-то рядом");
        //await UniTask.WaitUntil(() => EventsLogged.Any(l => l == "ApartmentsOpened"), cancellationToken: _lifetimeCt);
    }
    
    
   

    private async UniTask WinChapter() {
        playerMovement.playerCanMove = false;

        // Глушим игровое аудио перед финалом: радио-шум/проклятия и весь кухонный
        // шум/шёпот (NormalRooms снова активна после TeleportBack). Кухню гасим
        // целиком (все её источники), но НЕ всю NormalRooms — на её столе идут титры.
        madnessManager.IsVolumesFixed = true;
        if (radioAudio != null) {
            radioAudio.gameObject.SetActive(false);
        }
        if (storyObjectsContainer.NormalKitchen != null) {
            storyObjectsContainer.NormalKitchen.SetActive(false);
        }
        storyObjectsContainer.LabirintRooms.SetActive(false);

        await UI.ShowFade(1, 0.5f);
        UI.ShowTitlesScreen();
        UI.WinPanel.SetText("Вы спасли свой разум!");
        await UniTask.WaitForSeconds(1f, cancellationToken: _lifetimeCt);
        storyObjectsContainer.TitlesAnimation.Play();
        if (finalOutdoors != null) {
            finalOutdoors.Play();
        }

        await UI.ShowFade(0, 3f);
        // ГГ как бы вбегает в сцену — шаги в самом начале диалога.
        if (finalSteps != null) {
            finalSteps.Play();
        }

        await TalkUI.Say("ГГ: Дядя?.. Ты почему здесь? Я бежал к тебе в больницу!");
        await TalkUI.Say("Дядя: Тише. Всё нормально.");
        await TalkUI.Say("ГГ: Ты не представляешь!");
        await TalkUI.Say("ГГ: У меня в голове был чип! Я вытащил его.");
        await TalkUI.Say("Дядя: Понятно.");
        await TalkUI.Say("ГГ: Я слышал голоса, видел всякое…");
        await TalkUI.Say("ГГ: Твоя квартира стала лабиринтом!");
        await TalkUI.Say("ГГ: Радио сводило меня с ума. Но я его разбил!");
        await UniTask.WaitForSeconds(1f, cancellationToken: _lifetimeCt);
        await TalkUI.Say("ГГ: Погоди... А больница? Они хотели тебя оперировать.");
        await TalkUI.Say("Дядя: Хотели.");
        await TalkUI.Say("ГГ: И?..");
        await TalkUI.Say("Дядя: Я ушёл.");
        await TalkUI.Say("ГГ: Подожди. Тебя вообще не удивляет это?");
        await TalkUI.Say("Дядя: Что именно?");
        await TalkUI.Say("ГГ: Да всё! Я только что сказал, что достал чип!");
        await TalkUI.Say("ГГ: Ты же всю жизнь пытался что-то паранормальное разыскать.");
        await TalkUI.Say("ГГ: Книги собирал. Теории строил.");
        await TalkUI.Say("ГГ: И сейчас ты даже ничего не спрашиваешь?");
        await TalkUI.Say("Дядя: Твоего рассказа достаточно.");
        await TalkUI.Say("ГГ: Нет, ТЕБЕ было бы недостаточно!");
        await TalkUI.Say("Дядя: …");
        await TalkUI.Say("ГГ: Ты кто?! Это снова галлюцинации?");
        await TalkUI.Say("ГГ: Неужели…");
        // Диалог обрывается: вся картинка (кроме заголовка и кнопки выхода)
        // рассыпается в помехи. Заголовок и кнопка остаются поверх шума.
        if (UI.ScreenStatic != null) {
            UI.ScreenStatic.Show(1.2f).Forget();
        }

        await UniTask.WaitForSeconds(3f, cancellationToken: _lifetimeCt);
        await FlickerAndDoubt();
    }

    /// <summary>
    /// Финальная подмена: «Вы спасли свой разум!» → «…разум?». Заголовок меняется
    /// не сам по себе, а на моргании света — свет уходит, и в тот же кадр вместо
    /// восклицательного знака стоит вопросительный. Иначе подмена читается как
    /// опечатка, а не как то, что с картинкой что-то не так.
    /// </summary>
    private async UniTask FlickerAndDoubt() {
        bool changed = false;
        for (int i = 0; i < FinalFlickers; i++) {
            SetFinalLight(false);
            // Знак подменяем в темноте, на предпоследнем моргании: к моменту,
            // когда свет вернётся, надпись уже другая.
            if (!changed && i >= FinalFlickers - 2) {
                changed = true;
                UI.WinPanel.SetText("Вы спасли свой разум?");
            }

            await UniTask.WaitForSeconds(FinalFlickerDark, cancellationToken: _lifetimeCt);
            SetFinalLight(true);
            await UniTask.WaitForSeconds(FinalFlickerLit, cancellationToken: _lifetimeCt);
        }

        if (!changed) {
            UI.WinPanel.SetText("Вы спасли свой разум?");
        }
    }

    /// <summary>Настольная лампа на титрах — единственный источник света в кадре.</summary>
    private void SetFinalLight(bool isOn) {
        if (storyObjectsContainer.Lamp != null) {
            storyObjectsContainer.Lamp.Set(isOn);
        }

        if (storyObjectsContainer.LampEmission != null) {
            storyObjectsContainer.LampEmission.Set(isOn);
        }
    }

    public async UniTask Lose() {
        _deathCts?.Cancel();
        playerMovement.playerCanMove = false;
        HUD.TriggerDeath();
        TalkUI.Say("Нет... Голова...").Forget();
        await UniTask.WaitForSeconds(5f, cancellationToken: _lifetimeCt);
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