using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TalkUI : MonoBehaviour {
    public TextMeshProUGUI text;
    public Image back;

    [Header("Speakers")]
    [Tooltip("Цвет реплик дяди. Реплики ГГ показываются цветом самого TMP по умолчанию.\n" +
             "Спикер задаётся префиксом в начале строки: \"Дядя: ...\" или \"ГГ: ...\" — " +
             "префикс убирается при показе, а реплики дяди перекрашиваются в этот цвет.")]
    public Color uncleColor = new Color(0.45f, 0.76f, 0.70f);

    [Header("Typing")]
    [Tooltip("Скорость набора символов в секунду.")]
    public float charsPerSecond = 35f;

    [Tooltip("Сколько секунд держать реплику ПОСЛЕ того, как рядом с ней появилась иконка пробела.")]
    public float holdAfterTyping = 2.5f;

    [Tooltip("Минимальное суммарное время показа реплики (typewriter + обе паузы).")]
    public float minTotalDuration = 3f;

    [Header("Иконка «Пробел» после реплики")]
    [Tooltip("Картинка клавиши пробела. Появляется, когда реплика набралась целиком — " +
             "сразу за последней буквой. Пусто — иконки не будет")]
    public Image spaceIcon;

    [Tooltip("Пауза между концом набора и появлением иконки, сек")]
    public float spaceIconDelay = 0.4f;

    [Tooltip("Высота иконки в долях кегля текста")]
    public float spaceIconHeight = 0.62f;

    [Tooltip("Отступ от последней буквы до иконки, в долях кегля")]
    public float spaceIconGap = 0.35f;

    [Tooltip("Насколько центр иконки поднят над базовой линией, в долях кегля")]
    public float spaceIconRise = 0.28f;

    [Header("Humanization")]
    [Tooltip("Множитель паузы после . ! ? - длинный 'вдох' в конце предложения.")]
    public float sentenceEndPauseMultiplier = 9f;

    [Tooltip("Множитель паузы после , ; : - короткая пауза в середине фразы.")]
    public float clausePauseMultiplier = 4f;

    [Tooltip("Множитель паузы после тире/многоточия.")]
    public float dashPauseMultiplier = 5f;

    [Tooltip("Множитель паузы после перевода строки.")]
    public float newlinePauseMultiplier = 6f;

    [Tooltip("Множитель паузы после пробела.")]
    public float spacePauseMultiplier = 1.2f;

    [Tooltip("Случайный джиттер длительности обычной буквы (0 = ровный набор, 0.3 = +-30%).")]
    [Range(0f, 0.5f)]
    public float letterJitter = 0.18f;

    [Header("Ширина плашки")]
    [Tooltip("Предельная ширина текста в единицах канваса (1920 = вся ширина экрана).\n" +
             "Строка длиннее — реплика переносится на вторую строку, а плашка перестаёт расти.\n" +
             "Запас до края нужен: справа к последней букве ещё пристраивается иконка пробела.")]
    public float maxTextWidth = 1500f;

    [Header("Skip")]
    [Tooltip("Клавиша скипа в дополнение к ЛКМ. Первое нажатие во время печати " +
             "мгновенно дописывает реплику, второе во время hold-фазы — закрывает её.")]
    public KeyCode skipKey = KeyCode.Space;

    [Tooltip("Разрешать ли скипать реплику по ЛКМ. Если false — только по skipKey.")]
    public bool skipOnLeftClick = true;

    public static TalkUI instance;

    private readonly Queue<Entry> _queue = new Queue<Entry>();
    private bool _isPlaying;
    // Латч для скипа: пока true, удержание клавиши уже «съедено» и не считается
    // новым нажатием, пока её не отпустят. Без него одно нажатие за один кадр
    // каскадно проматывало бы всю очередь реплик (GetKeyDown держится весь кадр,
    // а проматывание/закрытие реплики происходит синхронно без ожидания кадра).
    private bool _skipLatched;
    // Фраза и tcs текущей проигрываемой реплики — нужны для дедупа
    // подряд идущих одинаковых вставок.
    private string _currentPhrase;
    private UniTaskCompletionSource _currentTcs;
    // Русский ключ реплики, которая сейчас на экране — чтобы перерисовать её
    // при смене языка "на лету". null — на экране ничего нет.
    private string _onScreenSource;

    // Очередь реплик крутится в fire-and-forget UniTask, а он не привязан к
    // времени жизни объекта: после выгрузки сцены цикл продолжал тикать и лез
    // в уже уничтоженный TMP. Токен гасит ожидания на Destroy.
    private CancellationToken _lifetime;

    /// <summary>Есть ли что-то в очереди или активно проигрывается.</summary>
    public bool IsBusy => _isPlaying || _queue.Count > 0;

    /// <summary>Объект ещё жив и есть куда писать текст.</summary>
    private bool IsAlive => this != null && text != null;

    // Через него навязывается предельная ширина: без ограничения плашка тянулась
    // по длине строки и на длинных английских репликах уезжала за края экрана.
    private LayoutElement _textLayout;

    private void Awake() {
        instance = this;
        _lifetime = this.GetCancellationTokenOnDestroy();
        _textLayout = TextWidthLimit.EnsureElement(text);
    }

    private void OnEnable() {
        Language.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnDestroy() {
        if (instance == this) {
            instance = null;
        }

        // Никто не должен остаться висеть в await реплики, которая уже не покажется.
        DrainQueue();
    }

    /// <summary>
    /// Разрывает все незакрытые ожидания — реплики показывать больше негде.
    /// Зовётся только на разрушении (OnDestroy и обрыв Process), поэтому именно
    /// ОТМЕНА, а не завершение: `await TalkUI.Say(...)` в сюжете — это точка,
    /// с которой сценарий продолжает трогать объекты сцены. Завершив ожидание,
    /// мы будили StoryManager в уже уничтоженной сцене, и следующая же строка
    /// главы падала с MissingReferenceException (при выходе из плей-мода это
    /// стабильно ловилось на TableChapter). Отмена вместо этого разматывает всю
    /// цепочку Story() наружу, где UniTask штатно глотает OperationCanceledException.
    /// </summary>
    private void DrainQueue() {
        while (_queue.Count > 0) {
            _queue.Dequeue().Tcs.TrySetCanceled(_lifetime);
        }

        if (_currentTcs != null) {
            _currentTcs.TrySetCanceled(_lifetime);
        }

        _currentPhrase = null;
        _currentTcs = null;
        _onScreenSource = null;
    }

    // Смена языка во время показа реплики: мгновенно перерисовываем её целиком
    // на нужном языке (без повторного эффекта печати).
    private void OnLanguageChanged() {
        if (_onScreenSource == null || text == null) {
            return;
        }

        text.text = BuildDisplay(_onScreenSource);
        TextWidthLimit.Apply(text, _textLayout, maxTextWidth);
        text.ForceMeshUpdate();
        text.maxVisibleCharacters = text.textInfo.characterCount;

        // Реплика перерисовалась другой длины — иконка осталась бы висеть там,
        // где кончался прежний текст.
        if (spaceIcon != null && spaceIcon.gameObject.activeSelf) {
            ShowSpaceIcon();
        }
    }

    private const string UnclePrefix = "Дядя:";
    private const string HeroPrefix = "ГГ:";

    /// <summary>
    /// Из «сырой» реплики достаёт чистый текст (ключ для перевода) и спикера.
    /// Префикс "Дядя:"/"ГГ:" в начале строки убирается; "Дядя:" помечает реплику
    /// дяди. Без префикса — реплика ГГ (спикер по умолчанию).
    /// </summary>
    private static void ParseSpeaker(string raw, out string clean, out bool isUncle) {
        isUncle = false;
        clean = raw;
        if (string.IsNullOrEmpty(raw)) {
            return;
        }

        if (raw.StartsWith(UnclePrefix, System.StringComparison.Ordinal)) {
            isUncle = true;
            clean = raw.Substring(UnclePrefix.Length).Trim();
        } else if (raw.StartsWith(HeroPrefix, System.StringComparison.Ordinal)) {
            clean = raw.Substring(HeroPrefix.Length).Trim();
        }
    }

    /// <summary>
    /// Готовит финальный текст для показа: убирает префикс спикера, переводит
    /// на текущий язык и, если говорит дядя, заворачивает в тег цвета.
    /// </summary>
    private string BuildDisplay(string rawSource) {
        ParseSpeaker(rawSource, out string clean, out bool isUncle);
        string translated = Language.Get(clean);
        // Подсветка важных для прохождения слов (по русскому ключу clean).
        translated = HighlightData.Apply(clean, translated);
        if (isUncle) {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(uncleColor) + ">" + translated + "</color>";
        }

        return translated;
    }

    /// <summary>
    /// Ставит реплику в очередь. Возвращаемый UniTask завершается, когда
    /// именно эта реплика будет показана и снята. Реплика никогда не
    /// прерывает уже играющую - она встаёт в очередь.
    /// Можно не ждать (вызывать без await) - реплика всё равно отыграет.
    /// </summary>
    public UniTask Say(string phrase) {
        // Сцена уже выгружается (финал уводит в меню) — показывать реплику некуда.
        // Возвращаем завершённый таск, чтобы сюжетный скрипт не завис в await.
        if (!IsAlive) {
            return UniTask.CompletedTask;
        }

        // Дедуп подряд идущих дублей: если такая же реплика уже играет или
        // уже сидит в очереди, не добавляем — возвращаем её tcs, чтобы все
        // ожидающие синхронно завершились с реальной репликой.
        UniTaskCompletionSource existingTcs = FindPendingTcs(phrase);
        if (existingTcs != null) {
            return existingTcs.Task;
        }

        UniTaskCompletionSource tcs = new UniTaskCompletionSource();
        _queue.Enqueue(new Entry(phrase, tcs));
        if (!_isPlaying) {
            Process().Forget();
        }

        return tcs.Task;
    }

    private UniTaskCompletionSource FindPendingTcs(string phrase) {
        if (phrase == null) {
            return null;
        }

        if (_currentPhrase == phrase) {
            return _currentTcs;
        }

        foreach (Entry e in _queue) {
            if (e.Phrase == phrase) {
                return e.Tcs;
            }
        }

        return null;
    }

    /// <summary>
    /// Ждёт, пока все поставленные на момент вызова реплики не отыграют.
    /// Если очередь пуста - возвращает завершённый UniTask мгновенно.
    /// </summary>
    public UniTask WaitUntilDone() {
        if (!IsBusy || !IsAlive) {
            return UniTask.CompletedTask;
        }

        UniTaskCompletionSource sentinel = new UniTaskCompletionSource();
        _queue.Enqueue(new Entry(null, sentinel));
        if (!_isPlaying) {
            Process().Forget();
        }

        return sentinel.Task;
    }

    private async UniTask Process() {
        _isPlaying = true;
        try {
            while (_queue.Count > 0) {
                if (!IsAlive) {
                    DrainQueue();
                    return;
                }

                Entry e = _queue.Dequeue();

                if (e.Phrase == null) {
                    // WaitUntilDone-маркер: просто завершаем его таск.
                    e.Tcs.TrySetResult();
                    continue;
                }

                _currentPhrase = e.Phrase;
                _currentTcs = e.Tcs;
                try {
                    await PlayOne(e.Phrase);
                }
                catch (System.OperationCanceledException) {
                    // Объект уничтожили посреди реплики — штатный выход, а не ошибка.
                    DrainQueue();
                    return;
                }
                catch (System.Exception ex) {
                    Debug.LogException(ex);
                }
                _currentPhrase = null;
                _currentTcs = null;

                e.Tcs.TrySetResult();
            }
        }
        finally {
            _isPlaying = false;
        }
    }

    private async UniTask PlayOne(string phrase) {
        if (!IsAlive) {
            return;
        }

        if (back != null) {
            back.gameObject.SetActive(true);
        }

        HideSpaceIcon();
        _onScreenSource = phrase;
        text.text = BuildDisplay(phrase);
        TextWidthLimit.Apply(text, _textLayout, maxTextWidth);
        text.maxVisibleCharacters = 0;
        // TMP сам разделит rich-text-теги от видимых символов после ForceMeshUpdate.
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;

        float baseDelay = charsPerSecond > 0 ? 1f / charsPerSecond : 0f;
        float typed = 0f;
        bool fastForwarded = false;

        for (int i = 1; i <= total; i++) {
            if (!fastForwarded && ConsumeSkip()) {
                fastForwarded = true;
                text.maxVisibleCharacters = total;
                break;
            }

            text.maxVisibleCharacters = i;
            if (baseDelay > 0f) {
                char shown = text.textInfo.characterInfo[i - 1].character;
                float delay = baseDelay * GetDelayMultiplier(shown);
                typed += delay;
                bool skipped = await WaitOrSkip(delay);
                if (skipped) {
                    fastForwarded = true;
                    text.maxVisibleCharacters = total;
                    break;
                }
            }
        }

        // Реплика набралась. Дальше две паузы: первая — чтобы игрок дочитал
        // сам текст, вторая — уже с иконкой пробела рядом с последней буквой,
        // как приглашение промотать. Жалоба с плейтеста была ровно про это:
        // реплики уезжали раньше, чем их успевали дочитать.
        float beforeIcon = Mathf.Max(spaceIconDelay, minTotalDuration - typed - holdAfterTyping);
        bool closed = false;
        if (beforeIcon > 0f) {
            closed = await WaitOrSkip(beforeIcon);
        }

        if (!IsAlive) {
            return;
        }

        // Промотали до появления иконки — показывать её уже некому.
        if (!closed) {
            ShowSpaceIcon();
            if (holdAfterTyping > 0f) {
                await WaitOrSkip(holdAfterTyping);
            }
        }

        if (!IsAlive) {
            return;
        }

        HideSpaceIcon();
        _onScreenSource = null;
        text.text = "";
        text.maxVisibleCharacters = 0;
        if (back != null) {
            back.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ставит иконку пробела вплотную за последней видимой буквой реплики.
    /// Позиция берётся из разметки TMP, поэтому иконка сама попадает на нужную
    /// строку при любом переносе и любом выравнивании, а цвет — из вершинного
    /// цвета этой буквы, то есть учитывает и тег цвета реплик дяди.
    /// </summary>
    private void ShowSpaceIcon() {
        if (spaceIcon == null || text == null) {
            return;
        }

        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        int last = -1;
        for (int i = info.characterCount - 1; i >= 0; i--) {
            if (info.characterInfo[i].isVisible) {
                last = i;
                break;
            }
        }

        if (last < 0) {
            return; // реплика из одних пробелов — цепляться не за что
        }

        TMP_CharacterInfo ci = info.characterInfo[last];
        // При автосайзе TMP кладёт вычисленный кегль обратно в fontSize, так что
        // иконка масштабируется вместе с текстом.
        float size = text.fontSize;
        float height = size * spaceIconHeight;
        float aspect = spaceIcon.sprite != null && spaceIcon.sprite.rect.height > 0f
            ? spaceIcon.sprite.rect.width / spaceIcon.sprite.rect.height
            : 1f;

        RectTransform rt = spaceIcon.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(height * aspect, height);
        // characterInfo лежит в локальных координатах самого TMP, а иконка —
        // его дочерний объект, поэтому localPosition совпадает один в один.
        rt.localPosition = new Vector3(ci.xAdvance + size * spaceIconGap,
            ci.baseLine + size * spaceIconRise, 0f);

        spaceIcon.color = ci.color;
        spaceIcon.gameObject.SetActive(true);
    }

    private void HideSpaceIcon() {
        if (spaceIcon != null) {
            spaceIcon.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ждёт указанное время, проверяя каждый кадр скип-инпут.
    /// Возвращает true, если был нажат скип — иначе false.
    /// </summary>
    private async UniTask<bool> WaitOrSkip(float seconds) {
        float remaining = seconds;
        while (remaining > 0f) {
            if (ConsumeSkip()) {
                return true;
            }
            // Yield с токеном: на Destroy бросит OperationCanceledException и
            // разорвёт цикл, вместо того чтобы дальше тикать в мёртвой сцене.
            await UniTask.Yield(_lifetime);
            remaining -= Time.deltaTime;
        }

        return false;
    }

    /// <summary>
    /// Edge-детект скипа с латчем: возвращает true ровно один раз на физическое
    /// нажатие. Пока клавишу держат, повторные вызовы (в т.ч. в том же кадре)
    /// возвращают false до отпускания. Это не даёт одному нажатию проскочить
    /// сразу несколько реплик — за нажатие проматывается максимум один шаг.
    /// </summary>
    private bool ConsumeSkip() {
        // ЛКМ отключена намеренно — она занята игровым взаимодействием/ударом.
        // bool down = (skipOnLeftClick && Input.GetMouseButton(0)) ||
        //             (skipKey != KeyCode.None && Input.GetKey(skipKey));
        bool down = skipKey != KeyCode.None && Input.GetKey(skipKey);

        if (!down) {
            _skipLatched = false;
            return false;
        }

        if (_skipLatched) {
            return false;
        }

        _skipLatched = true;
        return true;
    }

    private float GetDelayMultiplier(char c) {
        switch (c) {
            case '.':
            case '!':
            case '?':
                return sentenceEndPauseMultiplier;

            case ',':
            case ';':
            case ':':
                return clausePauseMultiplier;

            case '-':
            case '–': // –
            case '—': // —
            case '…': // …
                return dashPauseMultiplier;

            case '\n':
            case '\r':
                return newlinePauseMultiplier;

            case ' ':
            case ' ': // non-breaking space
                return spacePauseMultiplier;

            default:
                if (letterJitter <= 0f) {
                    return 1f;
                }
                return 1f + Random.Range(-letterJitter, letterJitter);
        }
    }

    private readonly struct Entry {
        public readonly string Phrase;
        public readonly UniTaskCompletionSource Tcs;

        public Entry(string phrase, UniTaskCompletionSource tcs) {
            Phrase = phrase;
            Tcs = tcs;
        }
    }
}
