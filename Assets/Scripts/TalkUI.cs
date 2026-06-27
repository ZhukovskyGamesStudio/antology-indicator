using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TalkUI : MonoBehaviour {
    public TextMeshProUGUI text;
    public Image back;

    [Header("Typing")]
    [Tooltip("Скорость набора символов в секунду.")]
    public float charsPerSecond = 35f;

    [Tooltip("Сколько секунд держать полностью набранную фразу перед скрытием.")]
    public float holdAfterTyping = 2.5f;

    [Tooltip("Минимальное суммарное время показа реплики (typewriter + hold).")]
    public float minTotalDuration = 3f;

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

    /// <summary>Есть ли что-то в очереди или активно проигрывается.</summary>
    public bool IsBusy => _isPlaying || _queue.Count > 0;

    private void Awake() {
        instance = this;
    }

    /// <summary>
    /// Ставит реплику в очередь. Возвращаемый UniTask завершается, когда
    /// именно эта реплика будет показана и снята. Реплика никогда не
    /// прерывает уже играющую - она встаёт в очередь.
    /// Можно не ждать (вызывать без await) - реплика всё равно отыграет.
    /// </summary>
    public UniTask Say(string phrase) {
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
        if (!IsBusy) {
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
        if (back != null) {
            back.gameObject.SetActive(true);
        }

        text.text = phrase;
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

        float hold = Mathf.Max(holdAfterTyping, minTotalDuration - typed);
        if (hold > 0f) {
            await WaitOrSkip(hold);
        }

        text.text = "";
        text.maxVisibleCharacters = 0;
        if (back != null) {
            back.gameObject.SetActive(false);
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
            await UniTask.Yield();
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
