# Antology Indicator — анализ проекта

## Общая информация

- **Движок:** Unity **6000.3.9f1** (Unity 6).
- **Render Pipeline:** Universal RP (URP) 17.3.0.
- **Компания / название продукта:** Zhukovsky Games / `Catchy Tune` (по `ProjectSettings.asset`).
- **Тип игры:** короткая нарративная игра от первого лица с упором на аудио и психологическое давление. Все диалоги и UI на русском языке.
- **Целевые платформы (CI):** StandaloneWindows64, StandaloneOSX, WebGL — собираются GitHub Actions и публикуются через butler на itch.io (`grafenters.itch.io/zhukovsky-games`).
- **Android-подписи:** в корне лежит `zhukovskyGames.keystore`, есть `androidjni` модуль — то есть когда-то готовился и Android-билд.

## Структура билдов

`EditorBuildSettings`:

1. `Scenes/LoadingScene.unity` — бутстрап и прелоад менеджеров.
2. `Scenes/MainMenu.unity` — главное меню.
3. `Scenes/GameScene.unity` — основная игровая сцена.

`AutoBootstrap` ([Assets/Scripts/SceneManagement/AutoBootstrap.cs](Assets/Scripts/SceneManagement/AutoBootstrap.cs)) через `[RuntimeInitializeOnLoadMethod]` при запуске форсит загрузку сцены с buildIndex = 0 (LoadingScene), чтобы инициализация менеджеров всегда проходила в правильном порядке независимо от того, с какой сцены стартовали в редакторе.

## Зависимости (Packages/manifest.json)

- `com.cysharp.unitask` — асинхронность (используется почти везде вместо корутин).
- `DOTween` (Demigiant) — твины для UI, аудио fade, поворотов.
- `ayellowpaper.serialized-dictionary` — сериализуемые словари.
- `com.unity.inputsystem` 1.18.0 (хотя сам код использует `Input.GetKey/MouseButton...` напрямую — старый Input Manager).
- `com.unity.render-pipelines.universal`, `ai.navigation`, `visualscripting`, `timeline`, `ugui`.

В `Assets/Plugins`:
- `Demigiant/` — DOTween.
- `ModularFirstPersonController/` — внешний асет от Jess Case, скрипт [Assets/Plugins/ModularFirstPersonController/FirstPersonController/FirstPersonController.cs](Assets/Plugins/ModularFirstPersonController/FirstPersonController/FirstPersonController.cs) (749 строк) — ходьба, спринт, headbob, прыжок, зум. Локально расширен статическим флагом `isHolding`, который блокирует движение и поворот камеры, когда игрок держит предмет.

## Сценный поток и инициализация

### LoadingScene
[Assets/Scripts/SceneManagement/LoadingSceneEntryPoint.cs](Assets/Scripts/SceneManagement/LoadingSceneEntryPoint.cs) запускает [LoadingManager.StartLoading()](Assets/Scripts/SceneManagement/LoadingManager.cs).

`LoadingManager.LoadManagers()`:
1. Находит все `CustomMonoBehaviour` на сцене, сортирует по `InitPriority` (низкий — раньше).
2. Для каждого, реализующего `IPreloadable`, дёргает `Init()` (через `await UniTask.Yield()` между ними, чтобы не блокировать кадр).
3. Дёргает `SaveLoadManager.LoadGame()` → `SaveGame()`.
4. Аддитивно грузит `MainMenu`, дожидается прогресса ≥ 0.9, активирует и выгружает `LoadingScene`.

В коде стоит TODO: `// отрефакторить чтобы зависимости сами решались, написать норм DI` — текущая инициализация ручная.

### MainMenu
[Assets/Scripts/MainMenu.cs](Assets/Scripts/MainMenu.cs) — три кнопки: `Play` (грузит `GameScene` по имени), `Rate` (ссылка на itch.io), `Exit`. Сюда же игрок возвращается с финального экрана и с экрана смерти — смотреть собранные книги на столе.

**Панель «О нас»** — [CreditsPanel](Assets/Scripts/CreditsPanel.cs) на `UICanvas/CreditsPanel`, правый нижний угол. Закрытой наружу торчит только язычок-заголовок, клик по нему выдвигает панель целиком (DOTween по `anchoredPosition.y`), повторный убирает.

- Задник — один спрайт [credits_paper.png](Assets/Sprites/Ui/newUI/credits_paper.png) (713×666): язычок нарисован сверху справа, ниже три полосы под три блока текста. Панель на канвасе 562×525, пивот и якоря в правом нижнем углу, поэтому позиции считаются от края экрана.
- **Закрытое положение считается, а не забито числом:** `ClosedMargin + TabHeight - высота панели`. Поменяешь размер панели — язычок останется на месте.
- Текст — восемь отдельных TMP-строк с [LocalizedText](Assets/Scripts/Localization/LocalizedText.cs) на каждой, выровненных по правому краю, с якорями в долях панели. Отдельные строки, а не один блок с rich-text: так русские ключи в [LocalizationData](Assets/Scripts/LocalizationData.cs) остаются читаемыми, а двигать строки можно поштучно. Русская вёрстка длиннее английской (строка про CC0 переносится на две) — поэтому у строк включён перенос и высота с запасом.
- Кликабельная зона — прозрачный `Image` + `Button` поверх нарисованного язычка (`Tab`), а не вся панель: щёлкать по титрам, чтобы их закрыть, неудобно.
- **Пока игрок держит книгу, панель прячется совсем** (`CanvasGroup` в ноль, `blocksRaycasts` снимается сразу). В этот же угол в этот момент выезжает плашка коллекции `BookCollected`, и они бы наложились. Ловится статический `FirstPersonController.isHolding` — так же, как в [MenuBooksHint](Assets/Scripts/Books/MenuBooksHint.cs); самого контроллера в меню нет, а статика есть.
- `UICanvas/Ref` — выключенный слой с эталонным скриншотом меню (`Assets/Sprites/Ui/Refs/screen_menu.png`) поверх канваса. Не удалять: по нему сверяется вёрстка.

### GameScene
Стартует [StoryManager](Assets/Scripts/StoryManager.cs).

## Менеджеры и инфраструктура

### ZhukovskyGamesPlugin
Микроплагин-инфраструктура автора:

- [CustomMonoBehaviour](Assets/Scripts/ZhukovskyGamesPlugin/CustomMonoBehaviour.cs) — базовый MonoBehaviour с `InitPriority` и хелпером `DestroyChildren`.
- [SingletonBase<T>](Assets/Scripts/ZhukovskyGamesPlugin/SingletonBase.cs) — базовый синглтон, опционально `DontDestroyOnLoad` через `DdolContainer`.
- [Singleton<T>](Assets/Scripts/ZhukovskyGamesPlugin/Singleton.cs) — версия с автосозданием в `Awake`.
- [DdolContainer](Assets/Scripts/ZhukovskyGamesPlugin/DdolContainer.cs) — ленивый контейнер для DDOL-объектов.
- [SafeArea](Assets/Scripts/ZhukovskyGamesPlugin/SafeArea.cs) — мобильный safe area для `RectTransform`, обновляет себя `InvokeRepeating` раз в 5 секунд. Содержит подозрительную строку `Vector2 anchorMax = safeArea.position = safeArea.size;` — присваивание выглядит как баг (anchorMax получает `size`, но заодно затирает `safeArea.position`).

### Abstract
- [IPreloadable](Assets/Scripts/Abstract/IPreloadable.cs) — интерфейс с `Init()`.
- [PreloadableSingleton<T>](Assets/Scripts/Abstract/PreloadableSingleton.cs) — `SingletonBase<T> + IPreloadable`, чей `Init()` создаёт синглтон.

### SaveLoadManager
[Assets/Scripts/Managers/SaveLoadManager.cs](Assets/Scripts/Managers/SaveLoadManager.cs) — `PreloadableSingleton`, `InitPriority = -1000` (грузится первым).

- Хранит JSON-сериализованный [GameSaveProfile](Assets/Scripts/DataStorage/GameSaveProfile.cs) в `PlayerPrefs["saveProfile"]`.
- `_needToSave` ставится `SaveGame()`, реальный сейв происходит в `LateUpdate` — батч-запись раз за кадр.
- Профиль примитивный: только `Nickname` (генерируется `"Farmer #<rand>"` — артефакт шаблона) и `SavedDate`.
- **Этот сейв в текущей игре не используется по делу** — реальный прогресс глав хранится в `PlayerPrefs["Chapter"]`, который пишется/читается прямо в `StoryManager`. То есть инфраструктура есть, но игра её обходит.

## Игровая логика

### StoryManager — режиссёр всего
[Assets/Scripts/StoryManager.cs](Assets/Scripts/StoryManager.cs) — статический синглтон, дирижирует всем сценарием через одну `async UniTask Story()`. Сценарий — четыре главы плюс финал:

1. **TableChapter** ([StoryManager.cs:118](Assets/Scripts/StoryManager.cs:118)) — обучение: игрок берёт книгу (`Pickable`), листает радио, учится мычать (`Q`) и щёлкать (`E`) в такт, ловит шум, безумие растёт.
2. **ElecticityChapter** ([StoryManager.cs:207](Assets/Scripts/StoryManager.cs:207)) — отключает свет/радио по проводам, кухонный шум (кран, холодильник, микроволновка).
3. **BreakRadioChapter** ([StoryManager.cs:267](Assets/Scripts/StoryManager.cs:267)) — поиск молотка, переключение `NormalRooms` → `LabirintRooms` (галлюцинаторный лабиринт), `TeleportRadio()`, разбивание радио ударами.
4. **ChipChapter** ([StoryManager.cs:304](Assets/Scripts/StoryManager.cs:304)) — записка, перец, чиханье ×3.
5. **WinChapter** — отключает движение, показывает экран победы.

Прогресс между главами сохраняется через `PlayerPrefs["Chapter"]` (0..4), флаг `isDropProgress` в инспекторе сбрасывает прогресс при `Start`, `StartingChapter` позволяет начать с любой главы (дев-инструмент).

**Открываешь что-то сюжетным клипом — синхронизируй `Openable`.** В кухонной главе холодильник открывается клипом `FridgeWork` через `PlayAnim`, то есть мимо [Openable](Assets/Scripts/Openable.cs). Пока `StoryManager` не сообщал об этом дверце, её `IsOpen` оставался `false`, и первый клик игрока трактовался как «открыть»: дверца рывком захлопывалась и открывалась заново, **а шум при этом засчитывался как убранный** — хотя холодильник оставался открытым и гудел (`InteractiveObj.LogOnce` стоит в `OnClick` перед `Openable.Interact`). Лечится строкой `fridge.SetOpenState(true)` сразу после `FridgeAnim.Play()`. Та же ловушка ждёт любой другой `Openable`, который сюжет откроет анимацией напрямую.
- Все три клипа холодильника (`FridgeWork` Loop, `FridgeDoorOpen`, `FridgeDoorClose`) живут на **одном** `Animation` на `fridge_final` в слое 0, поэтому `anim.Play` каждого из них останавливает предыдущий — специально гасить `FridgeWork` не нужно.

**Холодильник и микроволновка после кухонной главы остаются рабочими дверцами.** Раньше `ElecticityChapter` в конце гасил их `InteractiveObj` (а у микроволновки последним вызовом в `OnClick` стоял ещё и `set_enabled(false)` на самой себе — «один клик, один хлопок, дальше мертва»). На плейтесте это читалось как поломка: дверцы, которые только что открывались, переставали реагировать вообще. Теперь обе открываются и закрываются сколько угодно, как дверцы шкафчиков. Шум от этого не возвращается: клипы `FridgeDoorOpen` / `FridgeDoorClose` держат `Audio Source` холодильника выключенным, а `MicrowaveDoorSlam` останавливает луп `MicrowaveWork` и `OnClick` гасит объект с шумом.
- У микроволновки для этого появился собственный [Openable](Assets/Scripts/Openable.cs) на `microwave_door` (в базовом [microwave_final.prefab](Assets/Prefabs/microwave_final.prefab)): `openClip` — старый `MicrowaveDoorSlam` (он и правда ОТКРЫВАЕТ дверцу и зажигает лампу, несмотря на имя), `closeClip` — новый [MicrowaveDoorClose.anim](Assets/Animations/MicrowaveDoorClose.anim), развёрнутая копия первого. Отдельный клип, а не `openClip` наоборот (`Openable` так умеет): реверс легаси-клипа в проекте больше нигде не используется и не проверен, а обычный клип ведёт себя предсказуемо и правится в редакторе. Клип добавлен в список `m_Animations` того же `Animation` на `microwave_final` — иначе `anim[clip.name]` вернёт null и `Openable` промолчит.
- `ReactsToSnap` у микроволновки выключен: щелчок пальцем (`E`) не должен захлопывать ей дверцу, менять там нечего.
- На `MeshCollider` дверцы микроволновки выставлен `excludeLayers = Default` — по той же причине, что у дверец шкафа и холодильника: теперь ею можно махать сколько угодно, а анимированный кинематик-коллайдер у лица вжимает капсулу игрока в стену.

**Рельса третьей главы защищена от обхода (софтлок с плейтеста).** Два слоя поверх защитного `Pickable.OnDisable` (см. Pickable):
- Интерактив зонта-обманки (`FakeUmbrella`, лежит в шкафу прихожей) в общем цикле включения BlendItem-ов **пропускается по имени** и включается только вместе с открытием шкафа. Иначе игрок мог дотянуться до зонта раньше обманки-молотка (капсула зонта торчит из закрытого шкафа), событие `FakeUmbrella` (логается **при опускании**) ложилось в лог заранее — и позже подъём обманки-молотка мгновенно доигрывал секвенцию до `NormalRooms.SetActive(false)` с обманкой прямо в руках игрока. Переименуешь префаб `FakeUmbrella` — поправь проверку имени в `BreakRadioChapter`.
- Перед сменой `NormalRooms` → `LabirintRooms` сценарий ждёт `!FirstPersonController.isHolding`: комнату нельзя гасить, пока игрок держит предмет из её иерархии.

**Финальный чих защищён от обрыва (софтлок с плейтеста).** Весь переход из лабиринта в настоящую квартиру висит на **одном событии анимации**: `TeleportBack` на 3.1 сек клипа [HandWin.anim](Assets/Animations/Hand/HandWin.anim). А переходы к анимациям предметов для чиханья (`HandPepper` / `HandStick` / `HandFeather`) идут в контроллере [Hand.controller](Assets/Animations/Hand/Hand.controller) **из Any State, без exit time**, то есть обрывают `HandWin` в любой момент. Предметов для чиханья по квартире одиннадцать (`LogOnce` засчитывает только по одному на тип, но лежат они во всех копиях комнат), поэтому игрок мог взять и уронить лишнюю палочку ровно в эти 3 секунды. Дальше всё разваливалось насмерть: возврата в `NormalRooms` нет → чип из `FinalChapter` лежит в выключенной комнате → у неактивного `Pickable` не тикает `Update`, положить его нельзя, событие `ChipPuttedAway` не приходит, сценарий стоит навсегда, а `isHolding` от принудительного `ChipOnTable.PickUp()` блокирует вообще все клики. Три слоя защиты:
- Как только применён третий предмет, `ChipChapter` **гасит `InteractiveObj` у всех оставшихся** `SneezeObjects` — взять в руки больше нечего.
- Перед `HUD.TriggerSneeze()` сценарий ждёт `!FirstPersonController.isHolding` (тот же приём, что перед сменой комнат в главе с молотком): предмет кладут кликом, и этот клик пришёлся бы ровно на старт финальной анимации.
- `HUD.TeleportBack()` продублирован из сценария после ожидания в 6 сек и сделан идемпотентным (второй вызов выходит сразу, если `LabirintRooms` уже выключена, — иначе игрока дёрнуло бы на стартовую точку второй раз). **Вешаешь на анимацию событие, без которого сюжет не едет, — дублируй его из сценария**: анимацию можно прервать.

**Все ожидания сценария привязаны к `_lifetimeCt`** (`GetCancellationTokenOnDestroy` в `Awake`). Раньше `Story()` прошлого запуска переживала уход в меню и продолжала тикать «зомби»-цепочкой: трогала уничтоженные объекты и могла записать `PlayerPrefs["Chapter"]` поверх нового прохождения. **Добавляешь в сценарий новый `UniTask.WaitUntil` / `WaitForSeconds` — передавай `cancellationToken: _lifetimeCt`**, иначе зомби вернётся.

`await TalkUI.Say(...)` — такая же точка пробуждения, и токена ей не передашь: реплики ждут не время, а свою очередь. Поэтому [TalkUI.DrainQueue](Assets/Scripts/TalkUI.cs) на разрушении **отменяет** незакрытые ожидания (`TrySetCanceled`), а не завершает их. Пока он их завершал, выход в меню (и выход из плей-мода) будил `StoryManager` в уже уничтоженной сцене, и следующая строка главы падала с `MissingReferenceException` — стабильно ловилось на `TableChapter`, где после реплик идёт `RadioChange.enabled = true`. Отмена вместо завершения разматывает всю цепочку `Story()` наружу, где `Forget()` штатно глотает `OperationCanceledException` (`UniTaskScheduler.PropagateOperationCanceledException` в проекте не включён).

### Событийная система
`EventsLogged` — список строк-событий, на котором держится всё ветвление. Объекты сцены через [MonoBehLogger](Assets/Scripts/MonoBehLogger.cs) (`Log`/`LogOnce`/`LogClear`) пушат события (`"BookPicked"`, `"RadioMusic"`, `"RadioMusic2"`, `"RadioNoise"`, `"RadioSwitched"`, `"Hummed"`, `"Clicked"`, `"LampDisabled"`, `"KitchenDisabled"`, `"KitchenNoise*"`, `"FakeHammer"`, `"FakeUmbrella"`, `"RadioHit"`, `"RadioBroken"`, `"NoteFound"`, `"PepperBroken"`, `"PepperDust"`).

`StoryManager` через `await UniTask.WaitUntil(...)` ожидает появления нужных событий или достижения количества. Между шагами вызываются `TalkUI.Say()`, `TasksUI.ShowTask()`, `TasksUI.CompleteTask()`, и переключается `enabled` у `InteractiveObj`-ов, чтобы вести игрока по строгому рельсу.

`MonoBehLogger.React(msg)` — хелпер для гейм-объектов, чтобы прямо из `UnityEvent` в инспекторе вызывать `TalkUI.instance.Say(...)`.

**Где живут реплики.** Часть — прямо в `StoryManager` (`TalkUI.Say`), часть — в `UnityEvent`-ах префабов (`React`/`ReactOnce`), часть — на триггер-зонах ([ReactionZone](Assets/Scripts/ReactionZone.cs), [PuddleZone](Assets/Scripts/PuddleZone.cs)) и на входной двери ([DoorLockedReaction](Assets/Scripts/DoorLockedReaction.cs)). Реплики из префабов **grep-ом не ищутся** (см. раздел про локализацию: Unity пишет их `\u`-экранированием) — ищи с расшифровкой `\uXXXX` или через `SerializedObject` в редакторе.

**Реплика привязывается к тому моменту, когда игрок реально видит повод.** Три случая с плейтеста:
- «А ванная где..?» раньше говорилась дважды: в `BreakRadioChapter` сразу после подмены комнат и на `Lock.OnDeath` (сбил замок). Оба раза — до того, как игрок откроет дверь и увидит вместо ванной коридор. Теперь это `ReactionZone` на `LabirintRooms/midhallway/LabyrinthEntranceCollider` — бокс во всю ширину коридора, в полуметре за дверным проёмом: срабатывает ровно на входе в лабиринт.
- «Что-то у меня с глазами…» висела на `FakeUmbrella.OnPick` (обманка-молоток в прихожей) и перебивала сюжетное «Пора с ним кончать». Переехала на восемь инстансов `FakeEarstick` внутри [kitchen.prefab](Assets/Prefabs/Rooms/kitchen.prefab) — ватные палочки на кухне. Именно на инстансы кухни, а не на базовый `FakeEarstick.prefab`: такие же обманки лежат в ванной, а `ReactOnce` глобальный и сгорел бы на первой попавшейся.
- «Ванная!» (первый вход в ванную; раньше звучала как «Вот она где…» — текст заменён и в `ReactionZone` внутри [watercloset.prefab](Assets/Prefabs/Rooms/watercloset.prefab), и в паре `LocalizationData`) несёт флаг `ReactionZone.SkipIfBusy`: если в момент входа уже играет чужая реплика (игрок схватил палочку прямо на пороге), реплика-узнавание не встаёт в очередь, а **сгорает совсем** — выехав с опозданием, она попадала невпопад.

**Реплика-открытие говорится один раз — значит `ReactOnce`, а не `React`.** `React` зовёт `TalkUI.Say` при каждом срабатывании события; `ReactOnce` держит статический список уже сказанного (чистится в `StoryManager.Start` через `MonoBehLogger.ResetReactions`). Два случая с плейтеста:
- «Это почерк дяди! Что за?.. Микрочипы? Чихание?» висела на `Pickable.OnPick` записки ([Note.prefab](Assets/Prefabs/Note.prefab)) как `React` и повторялась каждый раз, когда записку брали снова — в том числе уже после главы с чипом, где узнавать нечего. Теперь `ReactOnce`.
- «Раньше тут лежало другое... Всё меняется как по щелчку пальцев» жила только на `OpenableShelf` в `MirrorRoom`, то есть звучала не про первый открытый шкаф, а про случайный шкаф уже в лабиринте. Такой же `ReactOnce` добавлен на `InteractiveObj` шкафа в прихожей ([Hallway.prefab](Assets/Prefabs/Rooms/Hallway.prefab) → `wardrobe_final`) — тот самый шкаф, который сюжет открывает в главе с молотком. Дедуп идёт по тексту реплики, поэтому она достанется тому шкафу, который игрок откроет первым, и второй раз не прозвучит.

### MadnessManager — основная механика безумия
[Assets/Scripts/MadnessManager.cs](Assets/Scripts/MadnessManager.cs) — статический синглтон.

- `Madness` плавно растёт со скоростью `MadnessSpeedPerS * speedMultiplier`, пока `IsMadnessRaising`.
- Ограничивается `TmpMaxMadness` (динамический потолок, которым `StoryManager` дозирует давление по главам: 25 → 35 → 45 → 55 → 100 → 50 → 100).
- Когда `Madness ≥ MaxMadness` (по умолчанию 100) — `StoryManager.Lose()`.
- Два «целительных» инпута:
  - **Клавиши лежат в инспекторе, а не только в коде.** `HummingKey`/`ClickKey` — сериализованные поля на объекте `MadnessManager` в `GameScene`, то есть правка дефолта в скрипте на игру не влияет. Переназначаешь — правь ещё три места: значения в сцене, тексты задач в `StoryManager` (`Подпевайте радиостанции (<b>Q</b>)`, `Щёлкайте в ритм радиостанции (<b>E</b>)`) и подсказку по управлению в паузе (`UICanvas/EscapePanel/Help/Explain` — там И видимый текст, И ключ `LocalizedText.sourceOverride`), а вместе с ними обе пары в [LocalizationData](Assets/Scripts/LocalizationData.cs).
  - **Humming** (`Q` удерживать) — снижает безумие пропорционально `HummingPower`. При мычании запускается анимация `melodyAnimation` и плавный fade `FakeHummingFade` через DOTween. `HummingPower` тратится при использовании и восстанавливается через `RefillHumming`.
  - **Click** (`E`, кулдаун `ClickCooldown` = 1.5 сек) — единичный сброс безумия пропорционально `ClickingPower`, тратит `ClickChillPerClick`.
- Когда выполняется условие «есть молоток» (`hud.HasHammer`), ЛКМ становится ударом по `HittableObj` через `CursorRaycast`. Иначе — холостой замах.
- `UpdateSounds()` (если не `IsVolumesFixed`) масштабирует громкости радио-шума и кликов в зависимости от `Madness / MaxMadness`.
- `DropMadness(maxTime)` — плавное снятие потолка `TmpMaxMadness` к финалу (100 → 0 за `maxTime` секунд).
- `PauseForSeconds(s)` — **служебная пауза**: пока играет анимация, которой игрок не управляет (применил предмет для чиханья, добивает радио), безумие не растёт и проиграть нельзя. В отличие от `IsBookPaused` значок паузы НЕ показывается: это не механика, а страховка от смерти в катсцене. Вызовы складываются по максимуму (серия ударов по радио держит паузу непрерывно) и снимаются сами по времени — пропущенное событие анимации не может «залипнуть». Ставят её [HUD](Assets/Scripts/HUD.cs) (`TriggerPepper`/`TriggerStick`/`TriggerFeather` и `TriggerAnim` по имени триггера — предметы зовут его через `PlayHandAnim`) и [RadioVfx](Assets/Scripts/RadioVfx.cs) (`LogRadioHit`, `LogRadioBroken`).

**Баланс пения и щелчков (замер, а не на глаз).** Полная шкала пения снимала 55.7 единиц безумия, полная шкала щелчков — 22.5: пение было в 2.5 раза мощнее, и это чувствовалось. Причина — связка «сила щелчка = цена щелчка»: обе величины считались от `ClickChillPerClick`, поэтому суммарное лечение с полной шкалы (`0.01·C·100²/(2·1.5·1.67·C)` ≈ 20) **не зависело от неё вообще** — крути сколько угодно, менялся только размер порции. Сейчас цена вынесена в отдельное поле `ClickCostPerClick`, а восстановление обеих шкал идёт по одной формуле (у щелчков был лишний `speedMultiplier`, и шкала копилась в 1.67 раза быстрее).

| | пение | щелчки |
|---|---|---|
| потратить всю шкалу | ~22 с (удерживая `E`) | ~4 с (8 щелчков) |
| снимет безумия | ~63 | ~63 |
| восстановить шкалу | 40 с | 40 с |

Разница осталась только в подаче: щелчки — кнопка паники (вся порция за 4 секунды), пение — марафон. Суммарный запас лечения при этом вырос (щелчки стали втрое полезнее), то есть игра стала мягче — если это перебор, крутить надо `MadnessSpeedPerS`, а не силу отдельной механики.

**Порог `MinHummingPercent` (35%) — против «шкала рассудка перестала подниматься».** Лечение от пения пропорционально остатку шкалы, и на выдохе оно сравнивалось с ростом безумия (равенство наступало ровно при 33% шкалы): игрок держит `E`, шкала пения ещё не пуста, а рассудок уже стоит на месте. Порог держит пение чуть сильнее роста до самого конца шкалы. Заодно: **`TmpMaxMadness` максимум рассудка не занижает** — это потолок безумия, то есть ПОЛ для шкалы рассудка (в первой главе 25 → рассудок не падает ниже 75%), и вверх он ничего не ограничивает никогда.

### Освещение и грейд

Ключевое, что нужно знать: **настоящего GI в сцене нет**. Лайтмапы формально запечены (`LightmapSettings` отдаёт 2 штуки), но реально залайтмаплено 4 рендерера из 703, лайт-проб нет вообще, `reflectionIntensity = 0`. То есть весь свет — рантайм-прямой, и всё, куда лампа не достаёт напрямую, держится только на ambient.

**Два профиля пост-обработки, и это важно:**
- `Assets/Settings/SampleSceneProfile.asset` — дефолтный профиль из `PC_RPAsset`. Действует везде, в том числе в `MainMenu` (там своего `Volume` нет).
- `Assets/Settings/DefaultVolumeProfile.asset` — на объекте `Volume` в `GameScene`, лежит поверх дефолтного и перебивает почти всё. Это тот профиль, который правит `GallucinationManager`. Правишь грейд игры — правишь именно его.

**Опоры сцены** (`RenderSettings`, одинаковые в `GameScene` и `MainMenu`):
- Ambient — режим **Trilight** (градиент): холодный сверху `(0.064, 0.074, 0.098)`, нейтральный по горизонту, тёплый снизу `(0.048, 0.038, 0.029)`. Именно ambient задаёт «дно» картинки: пока он был чистым чёрным (`Flat`, `(0,0,0)`), всё, куда не бьёт лампа, было буквально нулём, и текстура в тени переставала существовать.
- Туман — `ExponentialSquared`, плотность **0.032**, цвет тёмно-тёплый `(0.055, 0.048, 0.042)`. Чёрный туман (как было, при 0.047) неотличим от «ничего не отрисовано» — даль читалась как дыра, а не как воздух.

**Риг ламп.** Все лампы работают через `useColorTemperature`, цвет самих ламп белый — тонировать интенсивностью и цветом одновременно нельзя, иначе не понять, что именно крутишь.

| Лампа | K | Интенсивность | Тени |
|---|---|---|---|
| `ChandelierLightD` | 3300 | 0.78 / 1.05 / 1.32 / 1.55 (по комнатам) | Soft, tier Medium |
| `LampLight` (настольная) | 3000 | 2.08, inner spot 42° — мягкое пятно, а не плоский диск | Soft, tier High |
| `FridgeLight1/2` | 5200 (холодная) | 1.1 | нет |
| `MicrowaveLight` | 2700 | 1.0 | нет |

Холодный холодильник против тёплых ламп — единственный источник цветового контраста на кухне, ради этого он и вытянут с 0.12 до 1.1.

**Бюджет теней.** Шесть точечных ламп × 6 граней + спот = **37 теневых карт**. В атласе 2048 они не помещались, и URP молча резал разрешение вчетверо (в консоли висело `Reduced additional punctual light shadows resolution by 4`). Атлас поднят до **4096**, тиры — 256/512/1024, люстрам выставлен Medium: 37 × 512² влезает. Мелким лампам (холодильник, микроволновка) тени выключены совсем — они всё равно ничего не затеняли, но занимали слоты.

**[BounceFill](Assets/Scripts/Lighting/BounceFill.cs)** — бестеневой «отражённый» свет, живёт дочерним объектом внутри `ChandelierLightD` и `LampLight` в базовых префабах [chandelier_final.prefab](Assets/Prefabs/chandelier_final.prefab) и [Lamp.prefab](Assets/Prefabs/Lamp.prefab). Подхватывает у родительской лампы интенсивность (× `IntensityFactor`), дальность (× `RangeFactor`) и **`renderingLayerMask`** — покомнатная изоляция света от этого не ломается. Он же чинит чёрный потолок: плафон висит между лампой и потолком и полностью его затенял. `[ExecuteAlways]`, так что в редакторе видно сразу.

**[LightFlicker](Assets/Scripts/Lighting/LightFlicker.cs)** — еле заметное дыхание накала (±4.5%), которое с ростом безумия разгоняется до ±17% и начинает давать короткие провалы. Висит на самих лампах в тех же базовых префабах. Компонент пишет накал каждый кадр, поэтому если его меняют снаружи (`light.intensity = x` из сюжетного скрипта), он принимает чужое значение как новую базу — приглушение работает поверх мерцания, а не отменяется им. Гасить лампу лучше всё-таки через `SetActive(false)` на объекте света, как это делает `ToggleOnOff`: тогда вместе с ней уходит и её `BounceFill`.

**Отключение света в главе с проводом** (`StoryManager.ElecticityChapter`) гасит **только настольную лампу**: `Lamp.Set(false)` выключает объект `LampLight` (заодно с дочерним `BounceFill`), `LampEmission.Set(false)` снимает свечение абажура. Люстры комнаты не трогаются — раньше они приглушались до 20% через `RoomLights` / `RoomLightDim` в `StoryObjectsContainer`, от этого отказались: гасла вся комната, хотя из розетки выдернули одну лампу.

**Эмиссия светильников** поднята в HDR, чтобы блуму было за что зацепиться: `Assets/Models/Final/Material.033.mat` (плафон) → `(1.35, 1.08, 0.72)`, `Assets/Materials/lamp.mat` (абажур) → `(0.95, 0.88, 0.30)`. Порог блума 0.75 — эмиссия ниже единицы за него не цепляется.

**Правишь свет — помни:**
- Лампы живут в базовых префабах `chandelier_final` / `Lamp` / `fridge_final` / `microwave_final`, но интенсивность и `renderingLayerMask` перебиты per-instance в префабах комнат (`NormalRooms`, `LabirintRooms`, `MirrorRoom`, `livingroom`, `kitchen`, …). Правка базы цвет и тени раскатает, а яркость — нет.
- [EyeSpawner.DarknessThreshold](Assets/Scripts/EyeSpawner.cs) = 0.08 калиброван под текущий риг: под него попадает ~34% точек стен. Сделаешь сцену заметно светлее — глаз перестанет находить, куда сесть.
- На тех же объектах света, что `LightFlicker` и `BounceFill`, висят [LampHaze](Assets/Scripts/Atmosphere/LampHaze.cs) (дочерний квад `Haze`) и [IdleSway](Assets/Scripts/Atmosphere/IdleSway.cs) — см. «Атмосферный слой». Оба читают интенсивность лампы, так что менять яркость можно спокойно, ореол поедет следом.
- **Ambient и туман в рантайме перебивает [MoodDirector](Assets/Scripts/Atmosphere/MoodDirector.cs)** по номеру главы. Правка `RenderSettings` в сцене задаёт только вид до его первого кадра; настоящие значения — в массиве `Moods` на объекте `MoodDirector`, глава 0 = текущий вид сцены.

### GallucinationManager — визуальные галлюцинации
[Assets/Scripts/GallucinationManager.cs](Assets/Scripts/GallucinationManager.cs) — каждый кадр читает `Madness/MaxMadness`, прогоняет через `gallucinationCurve` (+ небольшой джиттер `randomGal`), и плавно лерпает на URP `VolumeProfile`:
- `FOV` игрока — простой `Lerp(MinFov, MaxFov)` по безумию (60 → 68): мир слегка отодвигается. Дыхания/осцилляции нет (убрано после плейтеста). Пока игрок держит предмет, FOV быстро возвращается к `MinFov` (`FovHoldReturnSpeed`).
- `ChromaticAberration.intensity` через `aberrationCurve` (× `AberrationMax`). Кривая специально «ранняя»: 10% безумия → 0.14, 30% → 0.41, 50% → 0.61. Если эффект пропал — смотри в консоль: `Start` ругается, если в профиле нет Chromatic Aberration, и принудительно ставит `active = true`.
- `DepthOfField.focusDistance` — `Lerp(SaneFocusDistance, MadFocusDistance, curved)`, то есть 12 м → 0.7 м. Раньше формула была `0.7 * (1 - curved)`: фокус стоял на 70 см уже при нулевом безумии, вся комната была расфокусирована с первой секунды, и это читалось не как эффект, а как мыло.
- `ChannelMixer.blueOutBlueIn` / `redOutBlueIn` — уход в багровые тона при росте безумия.

Каждый эффект включается флагом в инспекторе. Профиль берётся из `volume.profile` — это runtime-копия `Assets/Settings/DefaultVolumeProfile.asset`, сам ассет игра не портит.

**Делит профиль с [MoodDirector](Assets/Scripts/Atmosphere/MoodDirector.cs), но не пересекается с ним по параметрам.** `GallucinationManager` владеет ChromaticAberration / ChannelMixer / DepthOfField / FOV, `MoodDirector` — ColorAdjustments / Vignette / FilmGrain. Добавляешь эффект в один — проверь, что второй его не трогает: оба пишут `Override()` каждый кадр, и в случае пересечения победит тот, кто отработал позже, без всякой диагностики.

### MadnessVignette — виньетка по безумию
[Assets/Scripts/MadnessVignette.cs](Assets/Scripts/MadnessVignette.cs) на `UICanvas/OtherUi/Vignette`. Лежит внутри `OtherUi`, поэтому автоматически прячется в паузе и на экранах победы/проигрыша.

**Безумие ведёт и альфу, и скорость перещёлкивания кадров.** Спокойного игрока виньетки не касается вообще, дальше она проявляется и начинает дёргаться всё быстрее:

| Безумие | Альфа рамки | Кадр рамки | Полный цикл |
|---|---|---|---|
| 0–20% | 0 (не видно) | — | — |
| 30% | 0.05 | 1.16 с | 3.5 с |
| 55% | 0.40 | 0.79 с | 2.4 с |
| 70% | 0.64 | 0.57 с | 1.7 с |
| 100% | 1.00 | 0.13 с | 0.4 с |

Постоянная скорость кадров читалась как техническая анимация, идущая сама по себе, — теперь ускорение к пику работает как второй канал давления помимо затемнения. Фазу слои копят сами (`Layer.Phase` в кадрах), а не считают от `Time.unscaledTime`: длительность кадра плавает вместе с безумием, и формула от абсолютного времени скакала бы по кадрам при каждом её изменении. `_alphaCurve` **обязана начинаться с нуля и держать ноль до ~20%** — виньетка на спокойном игроке была прямой правкой по фидбеку.

**Затемнение краёв держит именно рисованная виньетка, а не `Vignette` из Volume.** Сначала было наоборот: пост-процессная виньетка `MoodDirector` росла по главам 0.25 → 0.47 и к финалу забивала картинку ровным чёрным овалом — на плейтесте это читалось как «на экран что-то надели», а рисованный арт под ней терялся. Сейчас пост-процесс срезан до 0.10 → 0.18: он и есть та еле заметная постоянная подложка, пока безумия нет. Правишь одно — смотри на второе, они складываются на экране.

**Виньетка двуслойная и покадровая.** Компонент держит список `Layer` (картинка + кадры + свой потолок альфы, длительность кадра и сдвиг фазы) и ведёт все слои от одной общей альфы:

| Слой | Объект | Кадры | MaxAlpha | Кадр: покой → пик |
|---|---|---|---|---|
| подложка | `OtherUi/VignetteBg` (sibling 0) | `vignette_bg_1..2` | 0.35 | 2.6 с → 0.23 с |
| рамка | `OtherUi/Vignette` (sibling 1) | `vignette_front_1..3` | 1.0 | 1.6 с → 0.13 с |

Арт лежит в `Assets/ToSort/vignette/`. Что поверх чего — решает порядок объектов в иерархии, а не порядок в списке слоёв: подложка обязана стоять раньше рамки. У слоёв разная длительность кадра и разный `StartPhase` — если кадры перещёлкиваются синхронно, это читается как подёргивание всей картинки, а не как живая линия. Потолок подложки занижен до 0.35: это мягкий градиент без характера, то есть ровно то, чего в кадре и было слишком много, — работать должна рамка. Кадры не листаются, пока альфа в нуле: смена спрайта помечает канвас грязным даже у полностью прозрачной картинки.

На экране титров (`WinPanel/Vignette`, статичная альфа 0.8) лежит первый кадр той же рамки `vignette_front_1` — без анимации: там нет `MadnessVignette`, и покадровка ему не нужна. Старый рисунок `Assets/ToSort/ui_eye/vignette.png` в игре больше нигде не используется.

### EyeSpawner — моргающий глаз в тёмных углах
[Assets/Scripts/EyeSpawner.cs](Assets/Scripts/EyeSpawner.cs) — отдельный объект `EyeSpawner` в сцене. Точки по сцене **не расставлены**: раз в `IntervalCalm` → `IntervalPeak` (14 → 5 сек по мере роста безумия, начиная с `MinMadnessPercent` = 25%) кидает до `AttemptsPerTry` рейкастов в периферию взгляда (угол 16–40° от центра, с уклоном влево/вправо), отбрасывает пол/потолок (`MaxUpDot`) и точки вне кадра, и считает освещённость кандидата `EstimateLight()` — сумма вкладов активных `Light` с учётом затухания, `NdotL`, перекрытий и **Rendering Layers** (в сцене свет разделён по комнатам). Перекрытия проверяются только для источников с тенями: свет без теней в URP светит сквозь стены, и рейкаст до лампы там врал бы. Если освещённость ниже `DarknessThreshold` (0.08 — примерно самая тёмная треть стен), там появляется спрайт-глаз: fade-in, 2–4 моргания кадрами `eye_1 → eye_2 → eye_3 → …`, fade-out. Спрайт один и переиспользуется, всегда повёрнут к камере, материал по умолчанию `Sprite-Unlit-Default` (виден в темноте).

### Атмосферный слой

Пять независимых надстроек в [Assets/Scripts/Atmosphere/](Assets/Scripts/Atmosphere), каждая с флагами в инспекторе и каждая отключаемая по отдельности. Общий принцип: **ничего не переписывать в сюжетном коде**. Все пять читают либо `MadnessManager.instance.Madness`, либо интенсивность самой лампы, либо `PlayerPrefs["Chapter"]` — то есть подключаются к тому, что уже есть, и молчат, если этого нет (в `MainMenu` нет ни `MadnessManager`, ни `StoryManager`, и всё продолжает работать).

**1. [DustMotes](Assets/Scripts/Atmosphere/DustMotes.cs) — пыль в воздухе.** Объект `AirDust` в `GameScene`: `ParticleSystem` в **мировом** пространстве симуляции, за игроком едет только область эмиссии (бокс 6×3×6). Уже выпущенные пылинки остаются висеть в комнате, поэтому облако не приклеено к взгляду.
- **Пока игрок сидит за столом, плотность режется до `SeatedRate` (20%).** В туториале камера утыкается в книгу, и пыль на полной плотности висит прямо перед носом, поверх текста. Условие — `FirstPersonController.playerCanMove`, то есть пыль набирается ровно тогда, когда игрок встаёт (начало `ElecticityChapter`), и набирается медленно (`SeatedBlend`, время жизни частиц 12–24 с): заметное появление пыли читалось бы как баг.
- Плотность на старте выставляется **в первом `LateUpdate`, а не в `Start`** — порядок `Start`-ов не гарантирован, и до `StoryManager.Start` поле `playerCanMove` ещё в своём инспекторном значении. Там же облако пересобирается вручную (`Clear` + `Simulate`), потому что встроенный prewarm у `ParticleSystem` считает по полной плотности. Шейдер [DustMote.shader](Assets/Shaders/DustMote.shader) — свой, а не URP'шный `Particles/Lit`, ровно по одной причине: **в нём нет NdotL**. Билборд всегда повёрнут к камере, поэтому у честного Lambert'а пылинка между игроком и лампой выходит чёрной — то есть ровно там, где настоящая пыль светится ярче всего. Берётся только затухание с расстоянием, как и положено мелкой частице, рассеивающей свет во все стороны. Дальше `alpha *= Luminance` — и пыль сама гаснет в тёмном углу и вспыхивает в конусе лампы, без единой строки логики. `renderingLayerMask` рендерера выставлен во все слои: пыль ездит по всей квартире, а свет в сцене разложен по комнатам. Плотность и турбулентность растут с безумием (`CalmRate`/`MadRate`).

**2. [LampHaze](Assets/Scripts/Atmosphere/LampHaze.cs) — объём света.** Дочерний квад `Haze` внутри `ChandelierLightD` и `LampLight` в базовых префабах [chandelier_final](Assets/Prefabs/chandelier_final.prefab) и [Lamp](Assets/Prefabs/Lamp.prefab) — там же, где живут `LightFlicker` и `BounceFill`. Шейдер [LampHaze.shader](Assets/Shaders/LampHaze.shader): аддитивный билборд с радиальным затуханием, мягко подрезанный о геометрию через `_CameraDepthTexture` (иначе виден круглый край квада, воткнувшегося в потолок) и гаснущий у самой камеры. Интенсивность и цвет (через `CorrelatedColorTemperatureToRGB`) берутся у родительской лампы каждый кадр — значит, и мерцание `LightFlicker`, и выключение лампы из `StoryManager` работают сами собой.
- **Ореол должен быть БОЛЬШИМ и ТУСКЛЫМ, а не маленьким и ярким.** Первая версия была радиусом 0.4 м — и оказалась полностью невидимой: у плафона уже есть HDR-эмиссия с блумом, и тесный ореол просто тонет в ней. Смысл появляется только когда ореол заметно шире светильника (люстра — 1.25 м при `IntensityFactor` 0.11) и читается как светящийся воздух вокруг, а не как второй блум.
- `Radius` задан в **метрах**: скрипт компенсирует масштаб родителя, потому что у люстры он ужат до 0.143.

**3. [CameraLife](Assets/Scripts/Atmosphere/CameraLife.cs) — живая камера.** На `FirstPersonController`. Дыхание (±0.22°), перлин-качание (0.16° → 1.15° по безумию), крен при движении вбок и сердцебиение выше 40% безумия (64 → 130 уд/мин, двойной удар «тук-тук»). Десятых долей градуса достаточно: всё, что читается глазом как движение, — уже перебор и укачивает.
- **Пишет в `Joint.localRotation` — и это единственное свободное место.** `FirstPersonController` пишет `joint.localPosition` (headbob) и `PlayerCamera.localEulerAngles` (питч, обнуляя y и z); клип `Sneeze` анимирует `CameraAnimAnchor`; `HUD.AsyncDeath` доворачивает саму камеру через DOTween. Локальный **поворот** `Joint` не трогает никто, поэтому иерархию менять не пришлось.
- Гасит себя в ноль, когда `Controller.cameraCanMove == false` (смерть, чиханье, финал) — иначе выравнивание горизонта в `HUD` дёргалось бы, — и приглушает до `HoldDamping`, пока предмет в руках.

**4. [IdleSway](Assets/Scripts/Atmosphere/IdleSway.cs) — живая квартира.** Висит на `ChandelierLightD` и `LampLight` в тех же базовых префабах. Адресат — не сам объект, а **тени**: весь свет рантайм-прямой, поэтому источник, гуляющий на пару сантиметров, тащит за собой все тени в комнате.
- Два режима, потому что случаи разные. **Снос** (`CalmShift`/`MadShift`, в метрах) — для точечных ламп: у них поворот не значит ничего, значение имеет только позиция. **Поворот** вокруг `PivotOffset` — для настольной лампы: там спот, и его конус чуть гуляет по столу.
- Плафон люстры в этой квартире **прижат к потолку** (толщина 0.133 м, пивот на креплении) — качаться ему нечем, поэтому двигается только свет под ним, а сам светильник стоит неподвижно. Раскачивать плафон бессмысленно.
- Снос задан в мировых метрах и делится на `lossyScale` родителя: у люстры масштаб 0.143, и «полградуса» там означало бы три миллиметра.
- **Шторы сюда добавлять нельзя.** `openable_curtain_livingroom` и `curtain_kitchen` открываются легаси-компонентом `Animation`, а `IdleSway` пишет трансформ в `LateUpdate` — штора перестала бы открываться совсем.

**5. [MoodDirector](Assets/Scripts/Atmosphere/MoodDirector.cs) — спуск по главам.** Объект `MoodDirector` в `GameScene`. Безумие — величина **возвратная**: попел, пощёлкал, и картинка снова как в первую минуту, поэтому вся игра от первой главы до финала выглядела одинаково. Здесь ведётся невозвратная величина — номер главы из `PlayerPrefs["Chapter"]` (опрашивается раз в `PollInterval`, `StoryManager` править не понадобилось). По массиву `Moods` (по записи на главу) плавно ведутся ambient-градиент, плотность и цвет тумана, `ColorAdjustments`, `Vignette` и `FilmGrain`: квартира холодеет, темнеет и теряет цвет от главы к главе.
- **Запись 0 — ровно текущий вид сцены**, поэтому первая глава выглядит так же, как до появления компонента, и спуск не бросается в глаза.
- Диапазон от главы 0 к главе 4: экспозиция +0.3 → −0.3, насыщенность +6 → −35, туман 0.032 → 0.060, виньетка 0.10 → 0.18.
- **Виньетку здесь трогать почти нечем — она намеренно почти выключена.** Затемнение краёв держит рисованная [MadnessVignette](Assets/Scripts/MadnessVignette.cs), тут остался только едва заметный ровный овал под ней. Хочешь темнее к финалу — тяни экспозицию, туман и ambient, а не `VignetteIntensity`: ровный чёрный овал поверх рисованного арта на плейтесте читался как «на экран что-то надели».
- `BlendSpeed` = 0.25 — переход между главами растянут на десятки секунд и в моменте незаметен.
- `OnDisable` возвращает `RenderSettings` как было: они общие для сцены, а `MainMenu` пользуется теми же значениями.

### Радио
- [RadioChanger](Assets/Scripts/RadioChanger.cs) — три источника (`change` шипение перемотки, `normal` музыка, `noise` белый шум). `ChangeToNext()` инкрементит индекс клипа, через 0.75 сек переключения логает `RadioSwitched`. На предпоследнем клипе — плавный кроссфейд через `DOFade` в шум (`RadioNoise`). На индексе `RadioMusic` — синхронизирует `MadnessManager.SyncHumming(normal.time)`, чтобы мычание попало в такт.
- [RadioAudio](Assets/Scripts/RadioAudio.cs) — три громкости (`noiseSource`, `curse1Source`, `curse2Source`) гонит по кривым в зависимости от процента безумия.
- [RadioVfx](Assets/Scripts/RadioVfx.cs) — поворот рандомной ручки и сдвиг полоски при переключении (твин `DOLocalRotate` / `DOLocalMoveX`), анимации удара/разрушения; после `breakClip` подменяет `radioMain` на `note` и логает `RadioBroken`.
- **Меняешь клип радио — сверь громкость замером, а не на слух.** `change` играет на volume 1.0, то есть поднять его нечем: `AudioSource.volume` жёстко клампится в 1 (проверено — присвоение 1.3 читается как 1). Значит, единственный запас — в самом файле, и клип должен приходить с той же громкостью, что и предыдущий. Мерить надо gated RMS (порог −60 dBFS) и RMS самой громкой секунды: обычный RMS по всему файлу врёт на клипах с тишиной в начале/конце. Текущие уровни: `radioChange` −28.5 dB gated при volume 1.0, `radiowave_talk` −31.3 dB при `normal` volume 0.65, `Song` −29.2 dB, `Song_2` −21.1 dB.

### Коллекция книг
Метапрогресс, который живёт между прохождениями: книги на уровне каждый раз стоят на своих местах, а счётчик найденного копится.

- **Что считается книгой.** Любой инстанс префаба [BookGeneral](Assets/Prefabs/BookGeneral.prefab) — на нём висит [CollectableBook](Assets/Scripts/Books/CollectableBook.cs). Идентификатор книги — **имя дочерней модели** (`book_1`, `book_11`, …), оно же имя пары текстур `Book_N_ru`/`Book_N_en`. Меш и материал у всех книг общие, различаются только текстурой обложки. Одна и та же книга встречается и в `NormalRooms`, и в `LabirintRooms` — для счётчика это одна книга. Книга со стола из туториала (`Book.prefab`, модель `table_book`) в коллекцию не входит.
- **Пока книга в руках:** [MadnessManager.IsBookPaused](Assets/Scripts/MadnessManager.cs) — безумие не растёт, пение не тратится, обе шкалы восстанавливаются. На шкале рассудка загорается значок `Pause`, и сама шкала при этом показывается принудительно (иначе значок висел бы на невидимой шкале). Плюс глохнет весь звук — см. [ReadingMuffle](Assets/Scripts/ReadingMuffle.cs) ниже.
- **Реплика — на подъём, а не на опускание.** «Ох... сознание не плывёт, пока я читаю.» висит на `Pickable.OnPick` в [BookGeneral.prefab](Assets/Prefabs/BookGeneral.prefab) (`ReactOnce`, то есть один раз за игру — на первой же взятой книге). Она объясняет механику ровно в тот момент, когда игрок её включил; на опускании реплики больше нет вовсе (была «Ох... я даже отвлёкся от этого жуткого шума.» — она приходила, когда механика уже кончилась).
- **Когда книгу кладут:** id уходит в [BookCollection](Assets/Scripts/Books/BookCollection.cs) (PlayerPrefs `BooksFound`, список через `;`), объект выключается, снизу справа выезжает плашка [BookCollectedUI](Assets/Scripts/Books/BookCollectedUI.cs) со счётчиком «N из M» — выезжает, висит 3 секунды и уезжает. `M` считается по сцене (уникальные id) и кладётся в `BooksTotal`, чтобы меню знало число без загрузки уровня; в инспекторе есть `_totalOverride`, если нужно прибить число вручную.
- **Сброс.** Кнопка «сбросить прогресс» в паузе чистит и главы, и коллекцию.
- **Стол в главном меню.** [MenuBookStacks](Assets/Scripts/Books/MenuBookStacks.cs) на объекте `BookStacks` раскладывает собранные книги неаккуратными стопками. Места стопок — отдельные точки-пустышки (`Stack_1`, `Stack_2`, `Stack_3`), их можно двигать в сцене руками: `OnDrawGizmos` рисует крестик основания и контуры книг, которые туда лягут. Книги распределяются по стопкам по кругу, чтобы стопки росли равномерно; поворот и сдвиг случайные, но зерно фиксированное — раскладка не прыгает между запусками. Данные берутся из [BookCatalog](Assets/Scripts/Books/BookCatalog.cs) (`Assets/Data/BookCatalog.asset`). **Добавил книгу на уровень — открой GameScene и нажми `Tools → Книги → Пересобрать каталог`**, иначе меню не будет знать, как её нарисовать.
- **Стопка живая: вынул книгу — верхние оседают, положил — легла на вершину.** Раньше книги стояли по забитым при спавне местам, и книга из середины оставляла в стопке дыру, а всё, что лежало выше, повисало в воздухе. Теперь `MenuBookStacks` держит списки того, что в какой стопке лежит, и на `Pickable.OnPick`/`OnDrop` пересобирает стопку снизу вверх (`Relayout`): оставшиеся книги проваливаются на место взятой твином DOTween, а взятая возвращается на вершину **своей** стопки — в середину её возвращать уже некуда.
  - Разброс (наклон и сдвиг) привязан к МЕСТУ в стопке, а не к книге: `SlotPose` считает его от зерна плюс номера стопки и уровня. Поэтому осевшая книга наследует наклон того места, куда легла, стопка остаётся такой же неаккуратной, и раскладка по-прежнему одинаковая от запуска к запуску. Гизмо рисует ровно эти же слоты.
  - Точка возврата переназначается через `Pickable.SetHomePose` — `Pickable` запоминает «дом» в `Awake` и без этого утащил бы книгу обратно в исчезнувшую дырку. Ту книгу, которую в этот момент несёт `Pickable.MoveTo`, `Relayout` не твинит (иначе два твина дерутся за трансформ) — ей только назначается новый дом.
- **В меню берётся всё, что лежит на столе.** `MenuBookStacks.PrepareScenePickables` проходит по всем `Pickable` своей сцены и не только снимает сборку и лимит дистанции, но и **включает `InteractiveObj`**: часть предметов приезжает из игровой сцены с выключенным интерактивом (там его включает сюжет) — так в меню не бралась раскрытая книга на столе.
  - **Там же включается и сам компонент `Pickable`, и это не то же самое.** С выключенным `Pickable` предмет прекрасно берётся — `PickUp()` зовут напрямую из `UnityEvent`, а вызов метода работает и на выключенном компоненте, — но положить его уже нельзя: за опускание отвечает `Update`, а он не тикает. Предмет залипал в руках навсегда вместе со статическим `isHolding`. Ровно так вела себя раскрытая книга на столе в меню: её `Pickable` выключен override-ом инстанса в `MainMenu.unity` (в базовом [Book.prefab](Assets/Prefabs/Book.prefab) и в `GameScene` он включён). Диагностический признак — застрявший `_lmbOwnedByPickup = true`.
- **Подсказка «Вы можете рассмотреть все книги!»** — [MenuBooksHint](Assets/Scripts/Books/MenuBooksHint.cs) на `UICanvas/BooksHint` в правом верхнем углу. Верстка черновая (TMP `Old-Soviet` 36, белый с альфой 0.8 — под ней тёмная комната, а не светлая панель меню, поэтому не коричневый цвет кнопок). Уезжает в тот момент, когда игрок впервые взял что-то в руки: ловится не конкретная книга, а статический `FirstPersonController.isHolding` — самого контроллера в меню нет, а статика есть. Флаг `_rememberSeen` кладёт в `PlayerPrefs["MenuBooksHintSeen"]`, что подсказку уже видели; выключить — будет показываться каждый заход.
- **Книги в меню можно брать в руки.** Для этого в MainMenu лежит `PlayerCamera/PickedAnchor` с `PlayerPicker` (как в игре), `Pickable` терпит отсутствие `HUD`, а у разложенных книг стоит `InteractiveObj.IgnoreRange` — прицела в меню нет, курсор свободный, и ограничение дистанции в 1.6 м там только мешает.
- **Плашка коллекции есть в обеих сценах, но поводы разные.** В игре она выезжает, когда книгу КЛАДУТ и та уходит в коллекцию; в меню — когда книгу БЕРУТ в руки, потому что собирать там уже нечего. Различает случаи сам `CollectableBook` по флагу `CollectOnDrop`: собираемая книга показывает плашку при опускании, несобираемая — при поднятии.
- **В меню не собирается ничего, и следит за этим `MenuBookStacks.PrepareScenePickables`.** Он проходит по всем `Pickable` своей сцены и снимает `CollectOnDrop` со всех `CollectableBook` разом. Это не перестраховка: в MainMenu лежит декоративная копия `livingroom` с обычными инстансами `BookGeneral`, у которых `CollectOnDrop` включён. Пока плашки в меню не было, они были безобидны (`CanCollect` требует `BookCollectedUI.instance`), но с её появлением такая книга при опускании ушла бы в коллекцию и выключилась прямо на столе.
- **`BookCollectedUI._countsLevelBooks` в меню обязан быть выключен.** С ним компонент пересчитывает книги по сцене и пишет результат в `BookCollection.Total`; в меню он насчитал бы по декоративной копии комнаты 3 штуки и затёр бы ими настоящее число (19). С выключенным флагом плашка просто читает сохранённое значение.

### ReadingMuffle — мир глохнет, пока читаешь

[ReadingMuffle](Assets/Scripts/ReadingMuffle.cs) на `FirstPersonController/Joint/CameraAnimAnchor/PlayerCamera` — том же объекте, где `AudioListener`. Пока в руках собираемая книга, весь звук игры уходит «за стену».

**Это не громкость.** Приглушить громкость — значит просто «сделать тише», а нужно ощущение расстояния. Издалека до слушателя доходят только низы: верх съедают воздух и стены, а то, что дошло, приходит с отражениями. Поэтому слоя два — `AudioLowPassFilter` (срез 22000 → 700 Гц) и `AudioReverbFilter` (`room` −10000 → −1500 при `dryLevel = 0`, то есть прямой звук не трогаем). Общую громкость компонент не трогает вовсе: `AudioListener.volume` занят ползунком из меню ([VolumeSettings](Assets/Scripts/VolumeSettings.cs)), и лезть туда нельзя.

- **Фильтр на слушателе накрывает и то, что идёт через микшер.** Половина источников в сцене (52 из 104) выведена в группы `MainMixer` — замер белым шумом через группу `Radio`: полоса 3–10 кГц падает в ~2700 раз (−69 dB), полоса 100–500 Гц не меняется. То есть отдельно возиться с микшером не нужно.
- **Срез ведётся по логарифму частоты.** Линейно по герцам путь 22000 → 700 почти весь проходит в неслышимой области и в конце обваливается рывком; в октавах спуск равномерный.
- **`AudioLowPassFilter` и `AudioReverbFilter` создаются в `Awake`, в префабе их нет.** Все ручки — поля самого `ReadingMuffle`, так что настраивать нечего вручную.
- **Ставит и снимает эффект [CollectableBook](Assets/Scripts/Books/CollectableBook.cs)** — то есть только собираемые книги. **Книга со стола из туториала (`Book.prefab`) сюда намеренно не попадает:** вся задача первой главы — на слух поймать музыкальную волну на радио, и глушить звук ровно в этот момент нельзя.
- Состояние самолечится: `ReadingMuffle` хранит ссылку на читаемый `Pickable` и каждый кадр проверяет `IsPicked` — уничтоженная книга читается как null, спрятанная в руках (`Pickable.OnDisable`) перестаёт быть поднятой. «Залипнуть» приглушённым мир не может, отдельный сброс статики не нужен.
- В главном меню компонента нет (там нет `FirstPersonController`), поэтому книги на столе берутся без эффекта — вызовы просто ни на что не попадают.

### Объекты сцены и хранилище ссылок
[StoryObjectsContainer](Assets/Scripts/StoryObjectsContainer.cs) — это просто инспекторный «контракт» ссылок (провода, лампа, краны, двери холодильника/микроволновки, анимации, две версии комнат `NormalRooms`/`LabirintRooms`, радио и точка телепортации, источники перца), на который опирается `StoryManager`.

### Ванная за балконной дверью

В гостиной (`livingroom`, общий префаб для `NormalRooms` и `LabirintRooms`) есть два взаимоисключающих объекта за шторой: `WindowState` (балкон с видом) и `fakeWatercloset` (целая ванная комната со своими стенами, светом, книгой и предметами для чихания).

Сейчас всё просто: **`fakeWatercloset` включён всегда, `WindowState` выключен**, за закрытой балконной дверью всегда ванная, и дверь игрок открывает сам — на `fakeWatercloset/door_balcony` висят `SwingDoor` (94.6°) и `InteractiveObj → SwingDoor.Interact`.

Попасть туда раньше времени нельзя: шторы перечислены в `StoryObjectsContainer.BathroomCurtains`, и их `InteractiveObj` включается ровно там же, где появляются лужи, — в `StoryManager.SetPuddles(true)` (глава с чипом). До этого штора не сдвигается и прицел на ней не подсвечивается.

Раньше содержимое за шторой перещёлкивалось: у `Openable` шторы в списке `states` лежали `WindowState` и `fakeWatercloset`, и щелчок/открытие меняли их местами. От этого отказались — на плейтесте оказалось слишком запутанно. `states` шторы теперь пустой; сама штора по-прежнему открывается и закрывается.

Правится это всё в базовом префабе [livingroom.prefab](Assets/Prefabs/Rooms/livingroom.prefab), но `NormalRooms.prefab` / `LabirintRooms.prefab` / `MirrorRoom.prefab` умеют перебивать `activeSelf` и `states` своими override-ами — после правки базы проверь и их.

**`PrefabUtility.SetPropertyModifications` на комнатах-матрёшках теряет чужие override-ы.** Комнаты вложены на три уровня (`LabirintRooms` → `livingroom` → `fakeWatercloset`/`QuadObj`), и запись отфильтрованного списка модификаций сносит записи, которые Unity не может пересобрать: вместе с осиротевшими `data[1]` уехали живой `ToggleEmission.Toggle` у лампы, `m_IsActive` и `m_Materials` у соседей. Одна правка утащила за собой полтора десятка посторонних, заметны они только в `git diff`.

Сам по себе `LoadPrefabContents` + `SaveAsPrefabAsset` **безопасен** — проверено холостым сохранением `LabirintRooms.prefab`: диффа нет (Unity только переупорядочивает документы в каноническом виде). То есть менять поля, добавлять и удалять объекты через редактор/скрипт можно; нельзя только переписывать списки модификаций целиком. Осиротевшие записи `data[N]` при `Array.size` меньше N безвредны — Unity их не применяет; если всё же хочется убрать, правь YAML текстом и сверяй `git diff` построчно.

### Текущая вода и лужи

Лужа под шторой — не декорация, а след: в ванной забыли закрыть кран. Поэтому вода и лужи включаются **одним вызовом** `StoryManager.SetPuddles(true)` (глава с чипом, там же открываются шторы), а выключает их игрок — перекрыв кран.

- [RunningWater](Assets/Scripts/RunningWater.cs) висит на объекте `Water` внутри [watercloset.prefab](Assets/Prefabs/Rooms/watercloset.prefab) (струя из `watertap_bath/watertap_bath_spout`, `AudioSource` + легаси-`Animation`, обе играют по `PlayOnAwake`). Сюжет держит ссылки в `StoryObjectsContainer.BathroomWaters` и зовёт `SetFlowing(bool)`; игрок жмёт на кран, и его `InteractiveObj.OnClick → RunningWater.StopAll`.
- **Луж пять, и они натекают по очереди, а не появляются разом.** Одна лужа под шторой игрока не находила: он просто не понимал, куда идти. Сейчас `Puddle_1…Puddle_5` (в `livingroom.prefab`) выстроены дорожкой от шторы (мировой z ≈ 9.95 в `NormalRooms`) вглубь гостиной мимо стола (z ≈ 3.5), и вода ползёт к игроку: каждая следующая вырастает скейлом из нуля через `StoryManager.PuddleInterval` (10 сек) после предыдущей. Появление ловится боковым зрением, а дорожка ведёт взгляд обратно к источнику.
- [Puddle](Assets/Scripts/Puddle.cs) — на самой луже. Очередь задаётся полем `Order` (1…5); **лужи с одинаковым номером натекают одновременно** — так синхронно наливается вода в обеих копиях гостиной в лабиринте. `Appear()` растит из нуля за `AppearDuration` (1.5 сек, `OutQuad`), `Dry()` сжимает до 5% и выцветает за 3.5 сек, `DryAll()` сушит всё разом.
- **Сам GameObject лужи никто не включает и не выключает — «есть вода или нет» это масштаб.** Лужа живёт вместе с комнатой, а `OnEnable` приводит её к сухому полу (масштаб 0, зона мокрых шагов выключена). Отсюда два следствия: в редакторе лужи видны всегда (иначе их не разложить), а в игре в первом же кадре их нет; и `HUD.TeleportBack` возвращает игрока в `NormalRooms` с сухим полом сам собой, ничего специально гасить не нужно.
- **Перекрытый кран отменяет и те лужи, что ещё не натекли.** `Puddle.DryAll` не только сушит лежащие, но и убивает очередь (`_floodCts`): закрыл кран на третьей луже — четвёртая и пятая не появятся вовсе. Тот же токен привязан к жизни `StoryManager`, поэтому уход в меню не оставляет тикающую очередь.
- **Кран и лужи не знают друг о друге по ссылкам, и это единственный рабочий способ.** Лужа — переименованный инстанс `QuadObj` внутри `livingroom.prefab`, кран — внутри вложенного `watercloset.prefab`; сослаться из одного на другое в инспекторе нельзя. Оба компонента ведут статические списки того, что сейчас активно (регистрация по `OnEnable`/`OnDisable`), и `StopAll` перекрывает всё, что течёт, а `Puddle.DryAll` сушит всё, что лежит. **Списка луж в инспекторе нет вовсе** (`StoryObjectsContainer.Puddles` убран): натекают все лужи активной сейчас копии квартиры. Добавил лужу в комнату, проставил `Order` — и всё работает, кода трогать не надо.
- **Перекрытый кран останавливает и воду в соседней копии комнаты.** Комнаты лабиринта — копии одной и той же квартиры: журчащий кран в копии рядом читался бы как «закрыл не тот».
- **Компонент `Puddle` лежит в `livingroom.prefab`, а не в `LabirintRooms`/`NormalRooms`.** Лужа приходит из гостиной, и правка базы разошлась во все три инстанса разом — вместо трёх отдельных override-записей `m_AddedComponents`. `QuadObj.prefab` при этом трогать нельзя: он общий с перцем и разбитым радио.
- **Кран кликабелен через `BoxCollider` на `watertap_bath` с `excludeLayers = Default`.** Своего коллайдера у него не было; коробка накрывает корпус, излив и три вентиля (клик ловится с полосы вдоль ванной, ~0.6–1.5 м). Слой игрока исключён по той же причине, что у дверец шкафа и холодильника: кинематик-коллайдер у лица вжимает капсулу в стену. Рейкасты `excludeLayers` не читают, поэтому клики целы.
- **`InteractiveObj` крана лежит в префабе выключенным.** Включает его только `RunningWater.SetFlowing(true)` — в настоящей ванной (`NormalRooms/watercloset`), где воду никто не открывал, прицел на кране не должен подсвечиваться.
- **Мокрые шаги — у каждой лужи свои.** `PuddleTrigger` (BoxCollider-триггер + [PuddleZone](Assets/Scripts/PuddleZone.cs)) висит дочерним объектом на каждой из `Puddle_1…Puddle_5` в базовом `livingroom.prefab`; коробка построена по границам спрайта (0.85 от них — блоб не заполняет углы), высота 3 м с центром 1.2 м над полом. Раньше зона была одна, общая на всю лужу под шторой. Дорожку из пяти луж такой коробкой не накрыть — шлёпало бы по сухому полу между ними.
- **Зону включает сама лужа, и только когда натекла целиком** (`Puddle.Appear` в конце твина). По растущему пятну шлёпать рано, а заодно активный коллайдер не проходит через нулевой масштаб — на нём Unity ругается «BoxCollider does not support negative scale or size». По той же причине `Hide()` гасит зону ДО обнуления масштаба, а не после.
- **`PuddleZone.OnDisable` отпускает игрока из мокрых шагов.** `Footsteps._puddleCount` накопительный, а `OnTriggerExit` на выключение объекта приходит не всегда — и лужа, высохшая (или выключенная вместе с лабиринтом в `HUD.TeleportBack`) прямо под ногами, оставляла бы игрока «мокрым» до конца игры. Поэтому лужа гасит именно объект зоны, а не компонент.
- **Реплика «Откуда вода?..» одна на все лужи.** `PuddleZone._reacted` статический, поэтому говорится она в первой луже, в которую игрок наступил, а не в каждой; сбрасывается в `StoryManager.Start` через `PuddleZone.ResetSaid()`.
- Старые `Puddle` и `Puddle_1ЧЧ` (схема раскладки) в `livingroom.prefab` лежат выключенными и в игре не участвуют: выключенный объект не попадает в статический реестр. Удалять их через `PrefabUtility` не стали — на `Puddle` в `LabirintRooms` висит override-ом старый `PuddleTrigger`, и снос базы утащил бы за собой чужие записи (см. предупреждение про `SetPropertyModifications` выше).
- Звук воды скопирован с кухонного и потому **2D** (`spatialBlend = 0`): течёт слышно из любой точки лабиринта. Если нужно, чтобы звук вёл к ванной, — это одно поле `Spatial Blend` на `AudioSource` объекта `Water`.

### Pickable / PlayerPicker
- [PlayerPicker](Assets/Scripts/PlayerPicker.cs) — синглтон с `Transform pickedPos` (точка перед камерой).
- [Pickable](Assets/Scripts/Pickable.cs) — `TogglePick()` запускает `MoveTo()` (UniTask-лерп позиции и поворота за 0.5 сек), переключает `FirstPersonController.isHolding`, скрывает курсор и руку через `HUD.SetCursorAndHand` (в главном меню HUD-а нет, вызов пропускается).
  - **Предмет в руках управляется одной ЛКМ: драг — вращение, короткий клик — положить.** ПКМ не участвует вовсе. Раньше «положить» висело на ПКМ, и игроки, привыкшие вращать предметы зажатой ПКМ, роняли книгу — а книга при этом уходит в коллекцию и исчезает, то есть случайный клик стоил дочитывания. Клик от вращения отличается по `dropDragTolerance` — **сколько мышь прошла за нажатие** (сумма `|Mouse X| + |Mouse Y|`, при штатной чувствительности осей 0.1 значение 1.2 ≈ 12 экранных пикселей). Порог по движению руки, а не по получившимся градусам поворота: у папок `rotateSpeed` = 400 против 200 у книг, и «порог в градусах» дал бы им вдвое более узкий допуск на клик при том же самом жесте игрока.
  - Нажатие, которым предмет подняли, помечается флагом `_lmbOwnedByPickup` и игнорируется до отпускания — иначе `OnMouseDown` и `GetMouseButtonUp` одного и того же клика подняли бы предмет и тут же положили обратно.
  - Драг копится и пока `MoveTo` тянет предмет к руке (первые ~0.6 сек), хотя вращение в это время не применяется: иначе размах мышью сразу после поднятия не засчитался бы, и отпускание ЛКМ положило бы предмет.
  - **Предмет держится и крутится за геометрический центр**, а не за пивот: центр считается один раз по локальным границам всех рендереров (`CalculateLocalCenter`), поэтому предметам с пивотом снизу (книги, папки) больше не нужно компенсировать это через `shiftPos`. `shiftPos` теперь — чистое художественное смещение от точки в руке.
  - Чувствительность вращения (`rotateSpeed`) не зависит от частоты кадров: `Input.GetAxis("Mouse X")` — это уже дельта за кадр, и умножать её на `deltaTime` было нельзя (на 144 Гц предмет крутился в 2.4 раза медленнее, чем на 60). Значения нормированы на 60 кадров, то есть остались в прежних единицах.
- **`Pickable.OnDisable` принудительно «кладёт» предмет, если тот выключили прямо в руках** (сюжет спрятал комнату-родителя, уход в меню выгрузил сцену): возвращает трансформ на стартовую позицию, восстанавливает слои/коллайдеры и снимает статический `isHolding`. Без этого у неактивного предмета переставал тикать `Update`, положить его было нельзя, и `isHolding` навсегда блокировал движение, камеру и все клики — включая книги в главном меню после выхода туда с предметом в руках. `OnPick`/`OnDrop` при принудительном сбросе сознательно не зовутся: скрытие предмета не должно двигать сюжет или коллекцию.
- [HeldItemLight](Assets/Scripts/HeldItemLight.cs) — мягкий широкий спот на камере (`PlayerCamera/HeldItemLight`), который плавно разгорается, пока предмет в руках, и гаснет при опускании. Угол широкий (120°, внутренний 85°) и источник сдвинут на 0.25 м за камеру — так свет ложится на предмет ровно, без яркого пятна посередине. Дальность 1.5 м, чтобы комнате доставалось по минимуму. Есть в обеих сценах — и в игре, и в меню.
- ЛКМ делает всё: короткий клик — взять/положить, зажать и вести мышью — вращать. ПКМ не занята ничем (см. выше про `dropRotateTolerance`).

### CursorRaycast и интерактив
- [CursorRaycast](Assets/Scripts/CursorRaycast.cs) — в `LateUpdate` рейкастит от камеры на дистанцию `RangeStatic` (1.6 м), переключает спрайт прицела между `defaultSprite` / `canInteract` / `canHit` (если есть `HittableObj` и игрок с молотком).
- [InteractiveObj](Assets/Scripts/InteractiveObj.cs) — простой `UnityEvent OnClick`, срабатывает по `OnMouseDown` (со встроенной проверкой дистанции).
  - **Клик сквозь интерфейс отсекается здесь и только здесь.** `OnMouseDown` приходит от физики и про UI не знает ничего: книгу на столе в меню можно было взять прямо сквозь панель, а мир — потыкать сквозь экран паузы. `IsPointerOverUI()` сам гоняет `EventSystem.RaycastAll` по `Input.mousePosition` — не `IsPointerOverGameObject()`, потому что тот отдаёт состояние с прошлого кадра и зависит от input-модуля (здесь стоит `InputSystemUIInputModule`).
  - **Из-за этого любая `raycastTarget`-графика теперь съедает клик под собой.** В игре это безопасно: курсор прибит к центру прицела, а в центре нет ни одной такой графики (проверено рейкастом). Но у декоративного HUD `raycastTarget` надо держать выключенным — прицел, `OtherUi/Hand` (её прямоугольник подходит к центру на 114 px) и т.п. Добавляешь полноэкранную картинку — либо снимай `raycastTarget`, либо веди её через `CanvasGroup.blocksRaycasts`, как это делает `FadePanel`.
  - Экран паузы гасит клики по миру целиком: `EscapePanel/BlockPanel` — невидимый `Image` 10000×10000 поверх всего.
- [HittableObj](Assets/Scripts/HittableObj.cs) — HP, события `OnHit` и `OnDeath`. Удар инициируется из `MadnessManager.Update` через `CursorRaycast.CanHit`.
- [Openable](Assets/Scripts/Openable.cs) — шкаф/полка с галлюцинаторной подменой содержимого. Клик мышью открывает и закрывает дверцу, щелчок пальцем (`E` → `SnapAllVisible`) меняет содержимое по кругу у всего, что сейчас в кадре. **Один щелчок = одна смена содержимого, независимо от того, открыт предмет или закрыт**: у закрытого играется хлопок дверцей и подмена идёт через `swapAt`, у открытого дверца закрывается и подмена идёт в конце анимации закрытия (`CloseDuration` = длина того клипа, который реально играет `Close`). Раньше на открытом шкафу уходило два щелчка — первый только закрывал, второй менял. Пока идёт анимация, `_isChanging` блокирует и повторный щелчок, и клик мышью. Флаг `ReactsToSnap` выключает реакцию на щелчок совсем — он снят у обеих штор (`openable_curtain_livingroom`, `curtain_kitchen`): содержимое за ними больше не перещёлкивается (`states` пуст), а дёргаться на каждый щелчок они продолжали. `PlayerInside` проверяется прямо перед подменой: если игрок за время анимации зашёл внутрь комнаты-состояния, прятать её нельзя — он провалится в пустоту.
- **Дверцы шкафа в прихожей (`wardrobe_door_l/r` в [Hallway.prefab](Assets/Prefabs/Rooms/Hallway.prefab)) и холодильника (`fridge_door` в [fridge_final.prefab](Assets/Prefabs/fridge_final.prefab)) не коллизятся с игроком**: на их коллайдерах `excludeLayers = Default` (игрок на Default). Анимированная дверца — кинематик-коллайдер, она вжимала капсулу игрока в стену, и он застревал (фидбек с плейтеста). Клики (`OnMouseDown`) это не ломает — рейкасты `excludeLayers` не читают. Добавляешь новую сюжетно-анимированную дверцу — сразу исключай на её коллайдере слой игрока.
- [SwingDoor](Assets/Scripts/SwingDoor.cs) — распашная дверь: клик открывает, ещё клик закрывает. Крутит объект вокруг мировой вертикали через собственный пивот (у дверных FBX пивот стоит в петлях), твин DOTween. В отличие от `Openable` не нужен ни легаси-`Animation`, ни клип — что важно для дверей с «повёрнутым» из FBX локальным поворотом вида `(90, y, 0)`. Поза в сцене = закрытая дверь.
- [MonoBehLogger](Assets/Scripts/MonoBehLogger.cs) — хелперы для `UnityEvent` (`Log`/`React`/`ReactOnce`/`ChangeQuest`/`PlayHandAnim`). **Все терпят отсутствие адресата**: те же префабы (книги) стоят и в главном меню, где нет ни `StoryManager`, ни `TalkUI`, ни `HUD`. Без проверок падал весь `UnityEvent` целиком — и, например, `Pickable.TogglePick` не успевал снять `isHolding`, после чего в меню переставали кликаться вообще все предметы.

### Ломаемые предметы (BreakableProp)

Декоративные картинки-квады, которые игрок с молотком может разбить (ЛКМ): чайник, солонка и кружка на кухне ([kitchen.prefab](Assets/Prefabs/Rooms/kitchen.prefab)), телевизор и часы в гостиной ([livingroom.prefab](Assets/Prefabs/Rooms/livingroom.prefab)), два телевизора в [MirrorRoom.prefab](Assets/Prefabs/Rooms/MirrorRoom.prefab) (у стены и в полке `OpenableShelf_3`) и ваза — объект прямо в корне `GameScene`. Поломка = подмена спрайта на `*_broken` + короткая пружинка масштабом (`DOPunchScale`) + стук молотка + звон стекла. Молоток после третьей главы не отбирается (`SetHammer(false)` в сценарии нет), так что бить можно и в обычной квартире в главе с чипом.

- Устройство: [BreakableProp](Assets/Scripts/BreakableProp.cs) рядом с `HittableObj` (Hp = 1). Звон стекла лежит в `HittableObj.deathClips` (`hit_glass.wav` — тот же, что у банки перца), стук — в `BreakableProp.hammerClips` (`HitBarrel (mp3cut.net).mp3` — тот же, что на ударах по радио). `Break` подписывается на `OnDeath` кодом в `Awake` — в инспекторе событие пустое, руками ничего не вешать. После поломки `HittableObj.enabled = false`: прицел не предлагает бить осколки.
- **Коллайдер под прицел создаётся в рантайме** (`Start`, коробка по границам спрайта, глубина 0.05) **и только если в сцене есть `MadnessManager`**: та же гостиная стоит декорацией в главном меню, и лишняя коробка там перехватывала бы клики по книгам. Слой Default исключён через `excludeLayers` — коробка не толкает капсулу игрока (рейкасты `excludeLayers` не читают, прицел и удар работают).
- Сломанные спрайты — `Assets/Sprites/QuadObjects/Broken/`, настройки импорта скопированы с оригиналов, а Pixels Per Unit подобран так, чтобы мировая **высота** совпала с целым предметом. Ваза — исключение: её сломанный арт — низкая кучка осколков, поэтому у неё родная плотность пикселей (PPU 1000) и пивот BottomCenter, как у целой, — осколки ложатся низом ровно туда, где стояло дно.
- **Настольная лампа намеренно не ломается**, хотя `lamp_broken.png` лежит в `Broken/`: все «лампы» в игре — инстансы сюжетного [Lamp.prefab](Assets/Prefabs/Lamp.prefab) с выключателем (клик = вкл/выкл, а с молотком в руке клик превращается в удар), рабочим светом и эмиссией по контуру целого абажура; финал вдобавок мигает именно этой лампой на титрах. Ломаемая лампа потребовала бы отдельного решения (гасить свет, глушить эмиссию, беречь титры).

### Прочие хелперы
- [HUD](Assets/Scripts/HUD.cs) — управляет анимациями руки (триггеры `Click`/`Hit`/`Swing`/`Win`/`Death`/`HasHammer`), звуками, fade-курсором/рукой через DOTween, анимацией мелодии. Содержит `AsyncDeath`/`AsyncSneeze`: выравнивает камеру до горизонта DOTween-ом `DORotate`, потом проигрывает заранее заданный AnimationClip.
  - **Чих усиливается от предмета к предмету.** `sneezeClips` — массив по номеру применённого предмета: первый чих сдержанный, второй сильнее, третий (записанный, `sneeze (mp3cut.net)`) — во всю силу. Номер берётся из `StoryManager.SneezeItemsUsed`, и это работает только потому, что событие предмета логается в `OnDrop` **раньше**, чем запускается анимация руки. Список короче — берётся последний клип; пустой — играет клип, назначенный на сам `AudioSource`. Все три клипа теперь живые записи: первые два (`sneeze_weak.wav`, `sneeze_mid.wav`) вырезаны из голосовых заметок `try_sneeze_0/1.m4a` (в записях по несколько дублей — взят самый чистый одиночный всплеск) и нормализованы к прежним уровням заглушек по gated RMS: −31.9 и −27.6 dB против −23.8 у третьего. **Замена шла перезаписью файлов поверх старых** — GUID и ссылки в `HUD.sneezeClips` не трогались; кладёшь новую запись — так же перезаписывай файл и держи её тише третьего чиха.
- [UI](Assets/Scripts/UI.cs) — Win/Lose/Escape панели, пауза по `Esc` с заморозкой `Time.timeScale = 0` и сохранением предыдущих состояний `playerCanMove/cameraCanMove`. Кнопки рестарта, открытия itch.io, выхода, сброса прогресса. В `Update` кормит `BarsPanel` тремя шкалами и включает значок «рассудок восстанавливается».
  - **Плашка «Внимание! Выход обнулит прогресс!»** — `EscapePanel/Container/ExitWarning`, между переключателями языка и кнопкой выхода. Рамка — спрайт [warning_frame.png](Assets/Sprites/Ui/newUI/warning_frame.png) (375×115, только контур, внутри прозрачно — бумага плашки просвечивает), текст красный `#691917`. Сохранений в игре нет вообще, выход из паузы означает начать заново — предупреждение висит ровно там, где на это можно нарваться.
  - **Кегль текста автоподбираемый (15…26), и это не украшательство:** рамка фиксированного размера, а английская строка длиннее русской. При жёстком кегле русская садилась в две строки, а английская вылезала за рамку третьей. Сейчас RU берёт 26, EN — 22.8, обе в две строки.
  - **Все три кнопки выхода в игре — «Выйти в меню» и ведут в главное меню (`GoToMainMenu`)**: финальный экран, экран смерти и пауза. Иначе игрок просто закроет игру и не увидит собранные книги на столе в меню. Из приложения выходит только «Выйти из игры» в главном меню (`MainMenu.Exit`); `UI.ExitGame` остался, но ни к одной кнопке не привязан.
  - Раз приложение между прохождениями больше не перезапускается, `StoryManager.Start` чистит статику, которая переживает смену сцены: `MonoBehLogger.ResetReactions()`, `Openable.ResetRunState()`, `ReactionZone.ResetSaid()`, `PuddleZone.ResetSaid()`. **Добавил новую статическую «сказано один раз» — добавь и сброс сюда**, иначе второе прохождение пройдёт молча.
  - **На финальных экранах ставится свой курсор** (`EndScreenCursor` = `Assets/ToSort/ui_eye/cursor_titles.png`, остриё в (5,5)): системная белая стрелка полностью терялась в белом шуме `ScreenStatic`. У текстуры под стрелкой мягкий тёмный ореол — он выбивает под курсором пятно и отделяет его от помех. Курсор ставится в `ShowTitlesScreen`/`ShowLoseScreen`. **Настройка курсора глобальная и переживает смену сцены**, поэтому `GoToMainMenu` и `Restart` обязаны вернуть системный (`Cursor.SetCursor(null, ...)`). Размер 32×32 — не из вкуса, а потому что более крупные аппаратные курсоры поддерживаются не на всех платформах, а в билдах есть WebGL.
  - **Экран титров — это `WinPanel`** (полноэкранная панель с нулевой альфой, сквозь неё видно `TitlesCamera`, снимающую стол). Виньетки на нём больше нет: рисованный спрайт с постоянной альфой 0.8 лежал первым ребёнком, поверх 3D-титров и помех, и на плейтесте читался как баг рендера, а не как приём. Пост-обработки на титрах нет вовсе (`TitlesCamera.renderPostProcessing = false`), так что затемнять края там теперь нечем — и не надо.
  - **Помехи в финале ([ScreenStatic](Assets/Scripts/ScreenStatic.cs)) — строчные, а не пиксельные.** Чистый рандом по точкам читался как техническая заглушка «нет сигнала», а не как телевизор, и вдобавок закрывал кадр целиком. Сейчас зерно собирается строками развёртки: у каждой строки своя яркость (`level` ± `rowContrast`), внутри строки идут горизонтальные штрихи длиной `minRun…maxRun` точек, через строку проходит гребёнка (`scanlineDarkness`), сверху ползёт мягкая полоса кадровой развёртки (`rollSpeed`/`barWidth`/`barLift`), и редко проскакивает строка-срыв (`tearChance`). Это те признаки, по которым глаз узнаёт ТВ-шум; **править «хаотичность» надо длиной штриха и `rowContrast`, а не частотой обновления**.
    - Помехи полупрозрачные: `Show()` доводит альфу не до единицы, а до `maxAlpha` (0.62), и вдобавок каждая точка тем прозрачнее, чем она темнее (`minPixelAlpha`). Шум ложится ПОВЕРХ картинки, а не вместо неё. Хочешь плотнее — `maxAlpha`, а не `level`: `level` красит шум в белое, а не делает его непрозрачнее.
    - Текстура широкая (`columns`×`lines` = 384×216) и фильтруется **Bilinear**, а не Point: строк меньше, чем пикселей на экране, и жёсткая лесенка читалась бы как низкое разрешение, а не как развёртка.
  - **Заголовок меняется на моргании света.** «Вы спасли свой разум!» → «…разум?» происходит не само по себе: `StoryManager.FlickerAndDoubt` трижды гасит и зажигает настольную лампу (`Lamp` + `LampEmission` — единственный источник света в кадре титров) и подменяет знак **в темноте**, на предпоследнем моргании. Свет возвращается — надпись уже другая. Число морганий и длительности фаз — поля `FinalFlickers` / `FinalFlickerDark` / `FinalFlickerLit` на `StoryManager`.
- [VolumeSettings](Assets/Scripts/VolumeSettings.cs) — слайдер общей громкости в меню, пишет в `AudioListener.volume` и `PlayerPrefs["Volume"]`. Реальная громкость = позиция слайдера × `MasterScale` (сейчас `0.225`, поднимали по фидбеку плейтестов: 0.10 → 0.15 → 0.225) — MasterScale и есть «комфортный потолок» всей игры, крутить общую громкость нужно им.
- [BarsPanel](Assets/Scripts/BarsPanel.cs) — три шкалы в правом верхнем углу: пение (`HummSlider`), щелчки (`ClickSlider`) и рассудок (`SanitySlider` = `1 - Madness/MaxMadness`). Каждая — сериализуемый `Bar` (CanvasGroup + Slider + Image заливки), логика одна на всех. Все три невидимы, пока всё хорошо: альфа берётся из общей `_alphaCurve` (0 при значении ≥ 0.75, 1 при ≤ 0.3), цвет заливки — из `_colorGradient`. Внутри иконки рассудка две под-иконки: `Increase` (всплывает, пока игрок реально поёт или в течение `UI.IncreaseIconTime` после щелчка) и `Pause` — задел на будущее, наружу торчит только `SetPause(bool)`. Обе лежат внутри `CanvasGroup` шкалы рассудка, то есть видны только когда видна сама шкала.
- [BlendItem](Assets/Scripts/BlendItem.cs) — две `Renderer` материала, кроссфейд альфы через DOTween. Используется для «галлюцинаторных» подмен (молоток ↔ фейк-молоток, зонт ↔ фейк-зонт): в третьей главе `StoryManager` включает их `InteractiveObj` и блендит обратно.
- [HintUI](Assets/Scripts/HintUI.cs) / [TalkUI](Assets/Scripts/TalkUI.cs) / [TasksUI](Assets/Scripts/TasksUI.cs) — TMP-текстовые UI: подсказки, реплики персонажа (typewriter + две паузы, см. ниже), активные задачи со зачёркиванием `<s>` и анимированной галочкой через `DOScaleX`.

### Иконка «Пробел» в конце реплики

Реплика [TalkUI](Assets/Scripts/TalkUI.cs) набирается, потом висит ещё **1 сек** (`spaceIconDelay`), потом рядом с последней буквой загорается иконка клавиши пробела, и только после неё идёт основная пауза в **2.5 сек** (`holdAfterTyping`). Разложено так по фидбеку с плейтеста: реплики уезжали раньше, чем их успевали дочитать, а про возможность промотать пробелом никто не знал.

- **Иконка — это отдельный `Image` (`Talk Text/Talk/TalkText/SpaceIcon`), а не спрайт внутри текста.** Позиция берётся из разметки TMP: `characterInfo` последнего видимого символа даёт `xAdvance` и `baseLine`, а `characterInfo` лежит в локальных координатах самого TMP — иконка его дочерний объект, поэтому `localPosition` совпадает один в один. Значит она сама попадает на нужную строку при любом переносе и любом выравнивании.
- **Цвет берётся из вершинного цвета последней буквы (`characterInfo[i].color`), а не из `text.color`.** Так иконка автоматически перекрашивается вместе с репликами дяди, завёрнутыми в тег `<color>`, и наследует альфу текста (у субтитров она 0.455).
- **Плашка под текстом хаггит текст, поэтому под иконку зарезервировано место.** На `Talk` висят `HorizontalLayoutGroup` + `ContentSizeFitter`, то есть ширина плашки = 15 + ширина текста + padding справа. Иконке нужно `gap + ширина` = 40·0.35 + 40·0.62·2.316 ≈ 71 px, поэтому **правый padding поднят с 15 до 86**: 71 на иконку и те же 15 полей, что слева. Без этого на однострочной реплике (или когда последняя строка самая длинная) иконка висела ровно за краем плашки — замерено, 56 px наружу. **Меняешь `spaceIconHeight`/`spaceIconGap` — пересчитай padding.**
- Арт — `Assets/ToSort/ui_eye/icon_space.png` (88×38, чисто белый с альфой, поэтому красится тинтом).
- Промотал пробелом до появления иконки — иконка не показывается вовсе, реплика просто закрывается.
- `minTotalDuration` (3 сек) теперь пол для всей реплики целиком и на практике не срабатывает: `1 + 2.5` его и так перекрывают. Он вычитается из ПЕРВОЙ паузы, чтобы не растягивать показ иконки.
- **Плашки «Пропустить текст (Пробел)» в правом верхнем углу больше нет** (был объект `UICanvas/SkipHint` и `StoryManager.ShowSkipHint`) — проматыванию учит сама иконка, и учит в тот момент, когда это нужно.
- [PlayAnim](Assets/Scripts/PlayAnim.cs) / [ToggleEmission](Assets/Scripts/ToggleEmission.cs) / [ToggleOnOff](Assets/Scripts/ToggleOnOff.cs) / [FaceCamera](Assets/Scripts/FaceCamera.cs) — тонкие обёртки для вызова из `UnityEvent` в инспекторе. У `ToggleOnOff` есть опциональные `onClip`/`offClip` — щелчок при реальной смене состояния (повторный `Set` в то же состояние молчит), играется через `PlayClipAtPoint` в точке цели. Подключены у настольной лампы в [Lamp.prefab](Assets/Prefabs/Lamp.prefab) (`onOffbuttonPressed`) — лампа щёлкает и при клике игрока, и при сюжетном выключении за провод.

## Локализация (RU/EN)

Двуязычная система (RU/EN), переключение в любой момент в меню или в паузе; весь текст и переводимые текстуры меняются мгновенно. Выбор хранится в `PlayerPrefs["Language"]`. Подробности — [Assets/Scripts/Localization/README.md](Assets/Scripts/Localization/README.md).

- **Принцип «русский исходник = ключ».** Реплики пишутся по-русски как обычно (в коде или в `React`/`ChangeQuest` префабов), а при показе прогоняются через [`Language.Get(ru)`](Assets/Scripts/Language.cs). Нет перевода — остаётся русский.
- [Language.cs](Assets/Scripts/Language.cs) — статический движок: `Get`, `ChangeLanguage(LangCode)`, событие `OnLanguageChanged`, ленивая инициализация из PlayerPrefs.
- [LocalizationData.cs](Assets/Scripts/LocalizationData.cs) — таблица переводов `RU → EN` (массив пар). **Чтобы добавить перевод — одна строка сюда.** Все существующие реплики/задачи/реакции/UI уже переведены (123 пары).
- **Sinks локализуются сами:** `TalkUI.Say`, `TasksUI.ShowTask`, `HintUI.ShowHint`, `WinPanel.SetText` переводят на показе и перерисовываются при смене языка (хранят русский ключ). Дедуп реплик/задач идёт по русскому ключу — независим от языка.
  - **Ловушка паузы:** язык переключают в паузе, а пауза прячет `TaskPanel`/`OtherUi` целиком — спрятанный sink в `OnDisable` отписан от `OnLanguageChanged` и пропускает переключение (задача возвращалась на старом языке). Поэтому `TasksUI` и `HintUI` перерисовываются ещё и в `OnEnable`. Добавляешь новый sink, который прячется на паузу, — добавляй и ему добор в `OnEnable`.
- **Русские строки в аргументах `UnityEvent` grep-ом НЕ ищутся.** Unity сериализует их в YAML `\u`-экранированием (`m_StringArgument: "Вдо..."`), поэтому поиск по «ПКМ» или «перец» в `.prefab` не найдёт ничего, хотя строка там есть. Так и уехала правка про ПКМ: три подсказки к предметам для чиханья (`Pepper`, `Earstick`, `Feather`) висят на `Pickable.OnPick` в префабах, поиск их не увидел, и они пережили смену управления. **Ищи такие строки через редактор** — обойти `SerializedObject` всех компонентов и смотреть свойства типа String; на префабах правь базовый ассет, инстансы подхватят сами.
- **Компоненты:** [LocalizedText](Assets/Scripts/Localization/LocalizedText.cs) — статичные TMP-подписи (кнопки меню, заголовки; ключ берётся из текста подписи). [LocalizedTexture](Assets/Scripts/Localization/LocalizedTexture.cs) — текстура на материале 3D-меша через `MaterialPropertyBlock`. [LocalizedSprite](Assets/Scripts/Localization/LocalizedSprite.cs) — спрайт в UI/2D. [LanguageToggle](Assets/Scripts/Localization/LanguageToggle.cs) — подсветка кнопок RU/EN.
- **Переводимые текстуры:** пары `*_ru`/`*_en` в `Assets/Models/TranslatableModels/` (`note`, `newspaper`, `table_book`); `LocalizedTexture` навешан на эти меши в `NormalRooms` и `LabirintRooms`.
- **Шрифт:** игровой `Old-Soviet` статичный и без латинского апострофа/кавычек, поэтому к нему добавлен fallback `LiberationSans SDF` — английский с сокращениями (`I'll`, `won't`) рендерится корректно. При замене шрифта сохранить fallback.

## Шрифт и кернинг

`Assets/Fonts/Old Soviet/Old-Soviet.asset` — статичный TMP-ассет (атлас 2048×2048, sampling point size 219, 140 глифов).

**Кернинг в ассете собран вручную, а не импортом Unity.** Нативный font engine (`FontEngine.GetPairAdjustmentRecords`) неправильно разбирает GPOS-кернинг формата 2 (class-based PairPos) у этого шрифта: в ассет попадало 17 854 записи вместо 2 138 — с индексами глифов вне диапазона шрифта (до 1917 при 149 глифах), с положительными подвижками и с подвижками на парах, которых в шрифте нет. Из-за этого русский текст слипался (`Ат`, `Ак`, `Ар`, `Ас`, `Аф`, `БИ`, `БК` получали по −21 px при sampling 219), а реально кернящиеся пары (`АВ`, `УА`, `АЧ`) не сдвигались вообще. На латинице это было заметно меньше — мусор попадал в основном на редкие сочетания вроде `Cq`/`CM`.

Правильная таблица достаётся из самого OTF через `fontTools` скриптом [Tools/fix_tmp_kerning.py](Tools/fix_tmp_kerning.py): 2 138 пар, все отрицательные, −1.1…−26.9 px, формат тот же, что пишет Font Asset Creator (`значение_в_юнитах × pointSize / unitsPerEM`). Заодно скрипт чистит `m_LigatureSubstitutionRecords` — Unity насочиняла 614 лигатур, хотя в шрифте вообще нет GSUB-фич.

```bash
pip install fonttools
python Tools/fix_tmp_kerning.py
```

**Перегенерировал ассет шрифта через Font Asset Creator — прогони скрипт заново**, иначе мусорный кернинг вернётся.

Известное ограничение: в `Old-Soviet.otf` нет глифа `_` (U+005F), которым TMP рисует underline/strikethrough. Поэтому зачёркивание `<s>` в `TasksUI` не рисуется и в консоль капает предупреждение «Unable to add underline or strikethrough».

**Лицензия шрифта уезжает в билд файлами:** `Assets/StreamingAssets/Font/` (`FONT_LICENSE.txt` + `LICENSE.txt`, SIL OFL 1.1 на Old Soviet). Именно StreamingAssets, а не Resources: Resources пакуется в бинарные архивы и читаемых файлов в папке игры не оставляет, а StreamingAssets копируется как есть — в `<Game>_Data/StreamingAssets/Font/` на Windows, внутрь .app на macOS и в раздачу WebGL.

## Ассеты

- **Папки (досье дяди).** Варианты от общего `FolderBase` в `Assets/Prefabs/Folders/`, различаются только моделью и текстурами обложки. Из сцены и комнат убраны `box`, `glass`, `papers_1`, `papers_2`, `rings` — правка шла по базовым префабам ([kitchen](Assets/Prefabs/Rooms/kitchen.prefab) — box, [fridge_final](Assets/Prefabs/fridge_final.prefab) — rings, [watercloset](Assets/Prefabs/Rooms/watercloset.prefab) — glass, [drawers](Assets/Prefabs/drawers.prefab) — papers_2) плюс один инстанс papers_1 в корне `LabirintRooms`. **Сами префабы-варианты и модели остались на месте** — вернуть папку в сцену можно перетаскиванием. Папки `empty` в сцене не было вовсе: `folder_empty.fbx` лежит в ассетах, но ни разу не инстансирован (объекты `Empty` внутри `OpenableShelf` — это пустое состояние полки, а не папка).
- **Префабы:** Book, Broshure, Carpet, FakeHammer, FakeUmbrella, Frog, Hallway/LabirintRooms/NormalRooms, Lamp, Lock, NewsPaper, Note, Pepper, Radio + комнаты в `Prefabs/Rooms/` (kitchen, livingroom, hallway, midhallway, watercloset) и предметы мебели (chandelier, fridge, microwave, table, watertap, провода).
- **Анимации:** двери, фридж/микроволновка work/close, кран, RadioHit/RadioBreak, WardrobeOpen, рука с молотком (Idle/Swing/Hit), Sneeze, MelodyIdle, Fall.
- **Аудио:** музыкальные клипы Music1–3, Song/Song_1/Song_2, Curse + Curse_ambient, Humming/Clicks (несколько вариантов), шумы холодильника/микроволновки/крана, fallBody, sneeze, swing, HitBarrel, radioChange, radiowave_talk, whiteNoiseBass, whiteNoseCycle. Микшер `MainMixer.mixer`.
- **Модели/материалы:** в `Assets/Models/Final/` лежат FBX и текстуры для всей сцены — холодильник, плита, микроволновка, кран, диван, шкаф, занавески, лампа люстра, столы, табуретки, провода, дверь, розетка.
- **Шрифты + TMP Essentials** в `Assets/Fonts/` и `Assets/TextMesh Pro/`.
- **Иконка приложения** — `Assets/Sprites/Ui/logo.png`: единственный слот Default Icon в `ProjectSettings` смотрит на её GUID, поэтому обновление иконки = перезапись этого файла (сейчас там арт `catchytune_icon_1000`; запасная `catchytune_icon_250.png` лежит рядом и нигде не используется). В UI сцен logo.png не участвует.

## CI/CD

[.github/workflows/main.yml](.github/workflows/main.yml) — три параллельные джобы (`build-windows`, `build-macos`, `build-webgl`) на `game-ci/unity-builder@v4`, версия Unity берётся из `vars.UNITY_VERSION`, лицензия и креды — из секретов. После сборок четвёртая джоба `deploy-itch` качает артефакты и пушит их через `butler` на itch.io по каналам `windows`, `macos`, `html`. Триггеры: `workflow_dispatch` и `repository_dispatch` типа `unity-build-trigger`.

## Замеченные шероховатости

- В [StoryManager.cs:140](Assets/Scripts/StoryManager.cs:140) ожидание `EventsLogged.All(l => l != "BookPicked")` срабатывает, только если события вообще нет — а `BookPicked` так нигде в коде и не пушится строкой `"BookPicked"`. Похоже, логика держится на префабе/UnityEvent — стоит проверить, что префаб книги действительно логает это событие.
- `SafeArea.UpdateSafeArea` содержит выражение `safeArea.position = safeArea.size`, которое мутирует локальную копию `Rect` и легко спутать с настоящей логикой.
- Старая save-инфраструктура (`SaveLoadManager` + `GameSaveProfile`) фактически не используется игрой — параллельно живёт `PlayerPrefs["Chapter"]` в `StoryManager`.
- `manifest.json` тянет и `InputSystem`, и `visualscripting`, но игра использует старый `UnityEngine.Input` — лишние пакеты можно удалить.
- README.md — однострочный `# mobile-template` (видимо, проект вырос из мобильного шаблона).
