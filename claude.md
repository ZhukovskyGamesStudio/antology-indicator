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
- `DropMadness(maxTime)` — функция плавного снятия потолка к финалу. **Содержит баг**: цикл `while (time > maxTime)` никогда не входит при `time = 0` — должен быть `<`.

### GallucinationManager — визуальные галлюцинации
[Assets/Scripts/GallucinationManager.cs](Assets/Scripts/GallucinationManager.cs) — каждый кадр читает `Madness/MaxMadness`, прогоняет через `gallucinationCurve` (+ небольшой джиттер `randomGal`), и плавно лерпает на URP `VolumeProfile`:
- `FOV` игрока (`MinFov` → `MaxFov`).
- `ChromaticAberration.intensity` через `aberrationCurve`.
- `DepthOfField.focusDistance` (`0.7 * (1 - curved)`).
- `ChannelMixer.blueOutBlueIn` / `redOutBlueIn` — уход в багровые тона при росте безумия.

Каждый эффект включается флагом в инспекторе.

### Радио
- [RadioChanger](Assets/Scripts/RadioChanger.cs) — три источника (`change` шипение перемотки, `normal` музыка, `noise` белый шум). `ChangeToNext()` инкрементит индекс клипа, через 0.75 сек переключения логает `RadioSwitched`. На предпоследнем клипе — плавный кроссфейд через `DOFade` в шум (`RadioNoise`). На индексе `RadioMusic` — синхронизирует `MadnessManager.SyncHumming(normal.time)`, чтобы мычание попало в такт.
- [RadioAudio](Assets/Scripts/RadioAudio.cs) — три громкости (`noiseSource`, `curse1Source`, `curse2Source`) гонит по кривым в зависимости от процента безумия.
- [RadioVfx](Assets/Scripts/RadioVfx.cs) — поворот рандомной ручки и сдвиг полоски при переключении (твин `DOLocalRotate` / `DOLocalMoveX`), анимации удара/разрушения; после `breakClip` подменяет `radioMain` на `note` и логает `RadioBroken`.

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
- [UI](Assets/Scripts/UI.cs) — Win/Lose/Escape панели, пауза по `Esc` с заморозкой `Time.timeScale = 0` и сохранением предыдущих состояний `playerCanMove/cameraCanMove`. Кнопки рестарта, открытия itch.io, выхода, сброса прогресса.
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
- `MadnessManager.DropMadness` — цикл `while (time > maxTime)` при `time = 0` мгновенно выходит (см. выше). Лернинг с потолком безумия на финале не работает.
- `SafeArea.UpdateSafeArea` содержит выражение `safeArea.position = safeArea.size`, которое мутирует локальную копию `Rect` и легко спутать с настоящей логикой.
- Старая save-инфраструктура (`SaveLoadManager` + `GameSaveProfile`) фактически не используется игрой — параллельно живёт `PlayerPrefs["Chapter"]` в `StoryManager`.
- В `MainMenu.Play()` сцена грузится по индексу `2` — если порядок сцен в build settings изменится, кнопка сломается. Лучше по имени.
- `manifest.json` тянет и `InputSystem`, и `visualscripting`, но игра использует старый `UnityEngine.Input` — лишние пакеты можно удалить.
- README.md — однострочный `# mobile-template` (видимо, проект вырос из мобильного шаблона).
