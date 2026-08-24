using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class TasksUI : MonoBehaviour {
    public TextMeshProUGUI text;
    public RectTransform Ckeck;
    private CancellationTokenSource cts;
    private List<string> _shownTasks = new();
    // Русский ключ текущей задачи — для перерисовки при смене языка.
    private string _currentTaskSource;

    private void OnEnable() {
        Language.OnLanguageChanged += OnLanguageChanged;
        // Пауза прячет TaskPanel целиком (UI.OnPressedEsc), и OnDisable нас отписывает:
        // смена языка в паузе проходила мимо, задача возвращалась на старом языке.
        // Вернулись на экран — перерисовываемся по текущему языку сами.
        OnLanguageChanged();
    }

    private void OnDisable() {
        Language.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() {
        if (_currentTaskSource != null && text != null) {
            text.text = Language.Get(_currentTaskSource);
        }
    }

    public void ShowTask(string taskName) {
        _currentTaskSource = taskName;
        Ckeck.gameObject.SetActive(false);
        text.text = Language.Get(taskName);
    }

    public void ShowTaskOnce(string taskName) {
        if (_shownTasks.Contains(taskName)) {
            return;
        }

        _shownTasks.Add(taskName);
        ShowTask(taskName);
    }

    public void CompleteTask() {
        cts?.Cancel();
        Ckeck.gameObject.SetActive(true);
        Ckeck.transform.localScale = new Vector3(0, 1, 1);
        cts = new CancellationTokenSource();
        Ckeck.DOScaleX(1, 0.5f).WithCancellation(cts.Token);
    }
}