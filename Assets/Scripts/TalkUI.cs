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

    public static TalkUI instance;

    private readonly Queue<Entry> _queue = new Queue<Entry>();
    private bool _isPlaying;

    /// <summary>Есть ли что-то в очереди или активно проигрывается.</summary>
    public bool IsBusy => _isPlaying || _queue.Count > 0;

    private void Awake() {
        instance = this;
    }

    /// <summary>
    /// Ставит реплику в очередь. Возвращаемый UniTask завершается, когда
    /// именно эта реплика будет показана и снята. Реплика никогда не
    /// прерывает уже играющую — она встаёт в очередь.
    /// Можно не ждать (вызывать без await) — реплика всё равно отыграет.
    /// </summary>
    public UniTask Say(string phrase) {
        UniTaskCompletionSource tcs = new UniTaskCompletionSource();
        _queue.Enqueue(new Entry(phrase, tcs));
        if (!_isPlaying) {
            Process().Forget();
        }

        return tcs.Task;
    }

    /// <summary>
    /// Ждёт, пока все поставленные на момент вызова реплики не отыграют.
    /// Если очередь пуста — возвращает завершённый UniTask мгновенно.
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

                try {
                    await PlayOne(e.Phrase);
                }
                catch (System.Exception ex) {
                    Debug.LogException(ex);
                }

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

        float charDelay = charsPerSecond > 0 ? 1f / charsPerSecond : 0f;

        for (int i = 1; i <= total; i++) {
            text.maxVisibleCharacters = i;
            if (charDelay > 0) {
                await UniTask.WaitForSeconds(charDelay);
            }
        }

        float typed = total * charDelay;
        float hold = Mathf.Max(holdAfterTyping, minTotalDuration - typed);
        if (hold > 0) {
            await UniTask.WaitForSeconds(hold);
        }

        text.text = "";
        text.maxVisibleCharacters = 0;
        if (back != null) {
            back.gameObject.SetActive(false);
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
