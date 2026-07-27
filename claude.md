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
[Assets/Scripts/MainMenu.cs](Assets/Scripts/MainMenu.cs) — три кнопки: `Play` (грузит сцену с индексом 2 = GameScene), `Rate` (ссылка на itch.io), `Exit`.

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

1. **TableChapter** ([StoryManager.cs:118](Assets/Scripts/StoryManager.cs:118)) — обучение: игрок берёт книгу (`Pickable`), листает радио, учится мычать (`E`) и щёлкать (`Q`) в такт, ловит шум, безумие растёт.
2. **ElecticityChapter** ([StoryManager.cs:207](Assets/Scripts/StoryManager.cs:207)) — отключает свет/радио по проводам, кухонный шум (кран, холодильник, микроволновка).
3. **BreakRadioChapter** ([StoryManager.cs:267](Assets/Scripts/StoryManager.cs:267)) — поиск молотка, переключение `NormalRooms` → `LabirintRooms` (галлюцинаторный лабиринт), `TeleportRadio()`, разбивание радио ударами.
4. **ChipChapter** ([StoryManager.cs:304](Assets/Scripts/StoryManager.cs:304)) — записка, перец, чиханье ×3.
5. **WinChapter** — отключает движение, показывает экран победы.

Прогресс между главами сохраняется через `PlayerPrefs["Chapter"]` (0..4), флаг `isDropProgress` в инспекторе сбрасывает прогресс при `Start`, `StartingChapter` позволяет начать с любой главы (дев-инструмент).

### Событийная система
`EventsLogged` — список строк-событий, на котором держится всё ветвление. Объекты сцены через [MonoBehLogger](Assets/Scripts/MonoBehLogger.cs) (`Log`/`LogOnce`/`LogClear`) пушат события (`"BookPicked"`, `"RadioMusic"`, `"RadioMusic2"`, `"RadioNoise"`, `"RadioSwitched"`, `"Hummed"`, `"Clicked"`, `"LampDisabled"`, `"KitchenDisabled"`, `"KitchenNoise*"`, `"FakeHammer"`, `"FakeUmbrella"`, `"RadioHit"`, `"RadioBroken"`, `"NoteFound"`, `"PepperBroken"`, `"PepperDust"`).

`StoryManager` через `await UniTask.WaitUntil(...)` ожидает появления нужных событий или достижения количества. Между шагами вызываются `TalkUI.Say()`, `TasksUI.ShowTask()`, `TasksUI.CompleteTask()`, и переключается `enabled` у `InteractiveObj`-ов, чтобы вести игрока по строгому рельсу.

`MonoBehLogger.React(msg)` — хелпер для гейм-объектов, чтобы прямо из `UnityEvent` в инспекторе вызывать `TalkUI.instance.Say(...)`.

### MadnessManager — основная механика безумия
[Assets/Scripts/MadnessManager.cs](Assets/Scripts/MadnessManager.cs) — статический синглтон.

- `Madness` плавно растёт со скоростью `MadnessSpeedPerS * speedMultiplier`, пока `IsMadnessRaising`.
- Ограничивается `TmpMaxMadness` (динамический потолок, которым `StoryManager` дозирует давление по главам: 25 → 35 → 45 → 55 → 100 → 50 → 100).
- Когда `Madness ≥ MaxMadness` (по умолчанию 100) — `StoryManager.Lose()`.
- Два «целительных» инпута:
  - **Humming** (`E` удерживать) — снижает безумие пропорционально `HummingPower`. При мычании запускается анимация `melodyAnimation` и плавный fade `FakeHummingFade` через DOTween. `HummingPower` тратится при использовании и восстанавливается через `RefillHumming`.
  - **Click** (`Q`, кулдаун `ClickCooldown` = 1.5 сек) — единичный сброс безумия пропорционально `ClickingPower`, тратит `ClickChillPerClick`.
- Когда выполняется условие «есть молоток» (`hud.HasHammer`), ЛКМ становится ударом по `HittableObj` через `CursorRaycast`. Иначе — холостой замах.
- `UpdateSounds()` (если не `IsVolumesFixed`) масштабирует громкости радио-шума и кликов в зависимости от `Madness / MaxMadness`.
- `DropMadness(maxTime)` — плавное снятие потолка `TmpMaxMadness` к финалу (100 → 0 за `maxTime` секунд).

### GallucinationManager — визуальные галлюцинации
[Assets/Scripts/GallucinationManager.cs](Assets/Scripts/GallucinationManager.cs) — каждый кадр читает `Madness/MaxMadness`, прогоняет через `gallucinationCurve` (+ небольшой джиттер `randomGal`), и плавно лерпает на URP `VolumeProfile`:
- `FOV` игрока — простой `Lerp(MinFov, MaxFov)` по безумию (60 → 68): мир слегка отодвигается. Дыхания/осцилляции нет (убрано после плейтеста). Пока игрок держит предмет, FOV быстро возвращается к `MinFov` (`FovHoldReturnSpeed`).
- `ChromaticAberration.intensity` через `aberrationCurve` (× `AberrationMax`). Кривая специально «ранняя»: 10% безумия → 0.14, 30% → 0.41, 50% → 0.61. Если эффект пропал — смотри в консоль: `Start` ругается, если в профиле нет Chromatic Aberration, и принудительно ставит `active = true`.
- `DepthOfField.focusDistance` (`0.7 * (1 - curved)`).
- `ChannelMixer.blueOutBlueIn` / `redOutBlueIn` — уход в багровые тона при росте безумия.

Каждый эффект включается флагом в инспекторе. Профиль берётся из `volume.profile` — это runtime-копия `Assets/Settings/DefaultVolumeProfile.asset`, сам ассет игра не портит.

### MadnessVignette — виньетка по безумию
[Assets/Scripts/MadnessVignette.cs](Assets/Scripts/MadnessVignette.cs) на `UICanvas/OtherUi/Vignette` (первый ребёнок, рисуется под остальным HUD). Альфа полноэкранной картинки `Assets/ToSort/ui_eye/vignette.png` ведётся по `_alphaCurve` от процента безумия: до 35% нуль, дальше плавно до 1. Лежит внутри `OtherUi`, поэтому автоматически прячется в паузе и на экранах победы/проигрыша.

### EyeSpawner — моргающий глаз в тёмных углах
[Assets/Scripts/EyeSpawner.cs](Assets/Scripts/EyeSpawner.cs) — отдельный объект `EyeSpawner` в сцене. Точки по сцене **не расставлены**: раз в `IntervalCalm` → `IntervalPeak` (14 → 5 сек по мере роста безумия, начиная с `MinMadnessPercent` = 25%) кидает до `AttemptsPerTry` рейкастов в периферию взгляда (угол 16–40° от центра, с уклоном влево/вправо), отбрасывает пол/потолок (`MaxUpDot`) и точки вне кадра, и считает освещённость кандидата `EstimateLight()` — сумма вкладов активных `Light` с учётом затухания, `NdotL`, перекрытий и **Rendering Layers** (в сцене свет разделён по комнатам). Перекрытия проверяются только для источников с тенями: свет без теней в URP светит сквозь стены, и рейкаст до лампы там врал бы. Если освещённость ниже `DarknessThreshold` (0.08 — примерно самая тёмная треть стен), там появляется спрайт-глаз: fade-in, 2–4 моргания кадрами `eye_1 → eye_2 → eye_3 → …`, fade-out. Спрайт один и переиспользуется, всегда повёрнут к камере, материал по умолчанию `Sprite-Unlit-Default` (виден в темноте).

### Радио
- [RadioChanger](Assets/Scripts/RadioChanger.cs) — три источника (`change` шипение перемотки, `normal` музыка, `noise` белый шум). `ChangeToNext()` инкрементит индекс клипа, через 0.75 сек переключения логает `RadioSwitched`. На предпоследнем клипе — плавный кроссфейд через `DOFade` в шум (`RadioNoise`). На индексе `RadioMusic` — синхронизирует `MadnessManager.SyncHumming(normal.time)`, чтобы мычание попало в такт.
- [RadioAudio](Assets/Scripts/RadioAudio.cs) — три громкости (`noiseSource`, `curse1Source`, `curse2Source`) гонит по кривым в зависимости от процента безумия.
- [RadioVfx](Assets/Scripts/RadioVfx.cs) — поворот рандомной ручки и сдвиг полоски при переключении (твин `DOLocalRotate` / `DOLocalMoveX`), анимации удара/разрушения; после `breakClip` подменяет `radioMain` на `note` и логает `RadioBroken`.

### Коллекция книг
Метапрогресс, который живёт между прохождениями: книги на уровне каждый раз стоят на своих местах, а счётчик найденного копится.

- **Что считается книгой.** Любой инстанс префаба [BookGeneral](Assets/Prefabs/BookGeneral.prefab) — на нём висит [CollectableBook](Assets/Scripts/Books/CollectableBook.cs). Идентификатор книги — **имя дочерней модели** (`book_1`, `book_11`, …), оно же имя пары текстур `Book_N_ru`/`Book_N_en`. Меш и материал у всех книг общие, различаются только текстурой обложки. Одна и та же книга встречается и в `NormalRooms`, и в `LabirintRooms` — для счётчика это одна книга. Книга со стола из туториала (`Book.prefab`, модель `table_book`) в коллекцию не входит.
- **Пока книга в руках:** [MadnessManager.IsBookPaused](Assets/Scripts/MadnessManager.cs) — безумие не растёт, пение не тратится, обе шкалы восстанавливаются. На шкале рассудка загорается значок `Pause`, и сама шкала при этом показывается принудительно (иначе значок висел бы на невидимой шкале).
- **Когда книгу кладут:** id уходит в [BookCollection](Assets/Scripts/Books/BookCollection.cs) (PlayerPrefs `BooksFound`, список через `;`), объект выключается, снизу справа выезжает плашка [BookCollectedUI](Assets/Scripts/Books/BookCollectedUI.cs) со счётчиком «N из M» — выезжает, висит 3 секунды и уезжает. `M` считается по сцене (уникальные id) и кладётся в `BooksTotal`, чтобы меню знало число без загрузки уровня; в инспекторе есть `_totalOverride`, если нужно прибить число вручную.
- **Сброс.** Кнопка «сбросить прогресс» в паузе чистит и главы, и коллекцию.
- **Стол в главном меню.** [MenuBookStacks](Assets/Scripts/Books/MenuBookStacks.cs) на объекте `BookStacks` раскладывает собранные книги неаккуратными стопками. Места стопок — отдельные точки-пустышки (`Stack_1`, `Stack_2`, `Stack_3`), их можно двигать в сцене руками: `OnDrawGizmos` рисует крестик основания и контуры книг, которые туда лягут. Книги распределяются по стопкам по кругу, чтобы стопки росли равномерно; поворот и сдвиг случайные, но зерно фиксированное — раскладка не прыгает между запусками. Данные берутся из [BookCatalog](Assets/Scripts/Books/BookCatalog.cs) (`Assets/Data/BookCatalog.asset`). **Добавил книгу на уровень — открой GameScene и нажми `Tools → Книги → Пересобрать каталог`**, иначе меню не будет знать, как её нарисовать.
- **Книги в меню можно брать в руки.** Для этого в MainMenu лежит `PlayerCamera/PickedAnchor` с `PlayerPicker` (как в игре), `Pickable` терпит отсутствие `HUD`, а у разложенных книг стоит `InteractiveObj.IgnoreRange` — прицела в меню нет, курсор свободный, и ограничение дистанции в 1.6 м там только мешает. В коллекцию такие книги не уходят: `CollectableBook` собирает только там, где есть `BookCollectedUI`, то есть в игровой сцене.

### Объекты сцены и хранилище ссылок
[StoryObjectsContainer](Assets/Scripts/StoryObjectsContainer.cs) — это просто инспекторный «контракт» ссылок (провода, лампа, краны, двери холодильника/микроволновки, анимации, две версии комнат `NormalRooms`/`LabirintRooms`, радио и точка телепортации, источники перца), на который опирается `StoryManager`.

### Pickable / PlayerPicker
- [PlayerPicker](Assets/Scripts/PlayerPicker.cs) — синглтон с `Transform pickedPos` (точка перед камерой).
- [Pickable](Assets/Scripts/Pickable.cs) — `IDragHandler`. `TogglePick()` запускает `MoveTo()` (UniTask-лерп позиции и поворота за 0.5 сек), переключает `FirstPersonController.isHolding`, скрывает курсор и руку через `HUD.SetCursorAndHand(false)`. `OnDrag` в режиме держания крутит объект вокруг мира.
- ПКМ — положить (`Input.GetMouseButtonDown(1)`).

### CursorRaycast и интерактив
- [CursorRaycast](Assets/Scripts/CursorRaycast.cs) — в `LateUpdate` рейкастит от камеры на дистанцию `RangeStatic` (1.6 м), переключает спрайт прицела между `defaultSprite` / `canInteract` / `canHit` (если есть `HittableObj` и игрок с молотком).
- [InteractiveObj](Assets/Scripts/InteractiveObj.cs) — простой `UnityEvent OnClick`, срабатывает по `OnMouseDown` (со встроенной проверкой дистанции).
- [HittableObj](Assets/Scripts/HittableObj.cs) — HP, события `OnHit` и `OnDeath`. Удар инициируется из `MadnessManager.Update` через `CursorRaycast.CanHit`.

### Прочие хелперы
- [HUD](Assets/Scripts/HUD.cs) — управляет анимациями руки (триггеры `Click`/`Hit`/`Swing`/`Win`/`Death`/`HasHammer`), звуками, fade-курсором/рукой через DOTween, анимацией мелодии. Содержит `AsyncDeath`/`AsyncSneeze`: выравнивает камеру до горизонта DOTween-ом `DORotate`, потом проигрывает заранее заданный AnimationClip.
- [UI](Assets/Scripts/UI.cs) — Win/Lose/Escape панели, пауза по `Esc` с заморозкой `Time.timeScale = 0` и сохранением предыдущих состояний `playerCanMove/cameraCanMove`. Кнопки рестарта, открытия itch.io, выхода, сброса прогресса. В `Update` кормит `BarsPanel` тремя шкалами и включает значок «рассудок восстанавливается».
- [BarsPanel](Assets/Scripts/BarsPanel.cs) — три шкалы в правом верхнем углу: пение (`HummSlider`), щелчки (`ClickSlider`) и рассудок (`SanitySlider` = `1 - Madness/MaxMadness`). Каждая — сериализуемый `Bar` (CanvasGroup + Slider + Image заливки), логика одна на всех. Все три невидимы, пока всё хорошо: альфа берётся из общей `_alphaCurve` (0 при значении ≥ 0.75, 1 при ≤ 0.3), цвет заливки — из `_colorGradient`. Внутри иконки рассудка две под-иконки: `Increase` (всплывает, пока игрок реально поёт или в течение `UI.IncreaseIconTime` после щелчка) и `Pause` — задел на будущее, наружу торчит только `SetPause(bool)`. Обе лежат внутри `CanvasGroup` шкалы рассудка, то есть видны только когда видна сама шкала.
- [BlendItem](Assets/Scripts/BlendItem.cs) — две `Renderer` материала, кроссфейд альфы через DOTween. Используется для «галлюцинаторных» подмен (молоток ↔ фейк-молоток, зонт ↔ фейк-зонт): в третьей главе `StoryManager` включает их `InteractiveObj` и блендит обратно.
- [HintUI](Assets/Scripts/HintUI.cs) / [TalkUI](Assets/Scripts/TalkUI.cs) / [TasksUI](Assets/Scripts/TasksUI.cs) — TMP-текстовые UI: подсказки, реплики персонажа (5 сек таймер с CancellationToken), активные задачи со зачёркиванием `<s>` и анимированной галочкой через `DOScaleX`.
- [PlayAnim](Assets/Scripts/PlayAnim.cs) / [ToggleEmission](Assets/Scripts/ToggleEmission.cs) / [ToggleOnOff](Assets/Scripts/ToggleOnOff.cs) / [FaceCamera](Assets/Scripts/FaceCamera.cs) — тонкие обёртки для вызова из `UnityEvent` в инспекторе.

## Локализация (RU/EN)

Двуязычная система (RU/EN), переключение в любой момент в меню или в паузе; весь текст и переводимые текстуры меняются мгновенно. Выбор хранится в `PlayerPrefs["Language"]`. Подробности — [Assets/Scripts/Localization/README.md](Assets/Scripts/Localization/README.md).

- **Принцип «русский исходник = ключ».** Реплики пишутся по-русски как обычно (в коде или в `React`/`ChangeQuest` префабов), а при показе прогоняются через [`Language.Get(ru)`](Assets/Scripts/Language.cs). Нет перевода — остаётся русский.
- [Language.cs](Assets/Scripts/Language.cs) — статический движок: `Get`, `ChangeLanguage(LangCode)`, событие `OnLanguageChanged`, ленивая инициализация из PlayerPrefs.
- [LocalizationData.cs](Assets/Scripts/LocalizationData.cs) — таблица переводов `RU → EN` (массив пар). **Чтобы добавить перевод — одна строка сюда.** Все существующие реплики/задачи/реакции/UI уже переведены (62 пары).
- **Sinks локализуются сами:** `TalkUI.Say`, `TasksUI.ShowTask`, `HintUI.ShowHint`, `WinPanel.SetText` переводят на показе и перерисовываются при смене языка (хранят русский ключ). Дедуп реплик/задач идёт по русскому ключу — независим от языка.
- **Компоненты:** [LocalizedText](Assets/Scripts/Localization/LocalizedText.cs) — статичные TMP-подписи (кнопки меню, заголовки; ключ берётся из текста подписи). [LocalizedTexture](Assets/Scripts/Localization/LocalizedTexture.cs) — текстура на материале 3D-меша через `MaterialPropertyBlock`. [LocalizedSprite](Assets/Scripts/Localization/LocalizedSprite.cs) — спрайт в UI/2D. [LanguageToggle](Assets/Scripts/Localization/LanguageToggle.cs) — подсветка кнопок RU/EN.
- **Переводимые текстуры:** пары `*_ru`/`*_en` в `Assets/Models/TranslatableModels/` (`note`, `newspaper`, `table_book`); `LocalizedTexture` навешан на эти меши в `NormalRooms` и `LabirintRooms`.
- **Шрифт:** игровой `Old-Soviet` статичный и без латинского апострофа/кавычек, поэтому к нему добавлен fallback `LiberationSans SDF` — английский с сокращениями (`I'll`, `won't`) рендерится корректно. При замене шрифта сохранить fallback.

## Ассеты

- **Префабы:** Book, Broshure, Carpet, FakeHammer, FakeUmbrella, Frog, Hallway/LabirintRooms/NormalRooms, Lamp, Lock, NewsPaper, Note, Pepper, Radio + комнаты в `Prefabs/Rooms/` (kitchen, livingroom, hallway, midhallway, watercloset) и предметы мебели (chandelier, fridge, microwave, table, watertap, провода).
- **Анимации:** двери, фридж/микроволновка work/close, кран, RadioHit/RadioBreak, WardrobeOpen, рука с молотком (Idle/Swing/Hit), Sneeze, MelodyIdle, Fall.
- **Аудио:** музыкальные клипы Music1–3, Song/Song_1/Song_2, Curse + Curse_ambient, Humming/Clicks (несколько вариантов), шумы холодильника/микроволновки/крана, fallBody, sneeze, swing, HitBarrel, radioChange, radiowave_talk, whiteNoiseBass, whiteNoseCycle. Микшер `MainMixer.mixer`.
- **Модели/материалы:** в `Assets/Models/Final/` лежат FBX и текстуры для всей сцены — холодильник, плита, микроволновка, кран, диван, шкаф, занавески, лампа люстра, столы, табуретки, провода, дверь, розетка.
- **Шрифты + TMP Essentials** в `Assets/Fonts/` и `Assets/TextMesh Pro/`.

## CI/CD

[.github/workflows/main.yml](.github/workflows/main.yml) — три параллельные джобы (`build-windows`, `build-macos`, `build-webgl`) на `game-ci/unity-builder@v4`, версия Unity берётся из `vars.UNITY_VERSION`, лицензия и креды — из секретов. После сборок четвёртая джоба `deploy-itch` качает артефакты и пушит их через `butler` на itch.io по каналам `windows`, `macos`, `html`. Триггеры: `workflow_dispatch` и `repository_dispatch` типа `unity-build-trigger`.

## Замеченные шероховатости

- В [StoryManager.cs:140](Assets/Scripts/StoryManager.cs:140) ожидание `EventsLogged.All(l => l != "BookPicked")` срабатывает, только если события вообще нет — а `BookPicked` так нигде в коде и не пушится строкой `"BookPicked"`. Похоже, логика держится на префабе/UnityEvent — стоит проверить, что префаб книги действительно логает это событие.
- `SafeArea.UpdateSafeArea` содержит выражение `safeArea.position = safeArea.size`, которое мутирует локальную копию `Rect` и легко спутать с настоящей логикой.
- Старая save-инфраструктура (`SaveLoadManager` + `GameSaveProfile`) фактически не используется игрой — параллельно живёт `PlayerPrefs["Chapter"]` в `StoryManager`.
- В `MainMenu.Play()` сцена грузится по индексу `2` — если порядок сцен в build settings изменится, кнопка сломается. Лучше по имени.
- `manifest.json` тянет и `InputSystem`, и `visualscripting`, но игра использует старый `UnityEngine.Input` — лишние пакеты можно удалить.
- README.md — однострочный `# mobile-template` (видимо, проект вырос из мобильного шаблона).
