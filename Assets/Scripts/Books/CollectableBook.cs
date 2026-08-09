using UnityEngine;

/// <summary>
/// Книга, которую можно забрать в коллекцию. Висит на префабе BookGeneral.
///
/// Пока книга в руках — безумие стоит на месте, а щелчки и пение
/// восстанавливаются (см. <see cref="MadnessManager.SetBookPause"/>).
/// Как только игрок кладёт книгу, она уходит в коллекцию и выключается.
/// </summary>
[RequireComponent(typeof(Pickable))]
public class CollectableBook : MonoBehaviour {
    [Tooltip("Идентификатор книги. Пусто — берётся имя дочерней модели (book_3 и т.п.)")]
    public string Id;

    [Tooltip("Выключить, чтобы книга просто бралась в руки и не уходила в коллекцию (книги в меню)")]
    public bool CollectOnDrop = true;

    private Pickable _pickable;

    /// <summary>
    /// Собирать книги можно только там, где есть плашка коллекции, то есть в игре.
    /// В главном меню <see cref="MenuBookStacks"/> снимает <see cref="CollectOnDrop"/>
    /// со всех книг сцены — их можно вертеть, но не собирать.
    /// </summary>
    private bool CanCollect => CollectOnDrop && BookCollectedUI.instance != null;

    /// <summary>
    /// Книга, которая никогда не уйдёт в коллекцию (меню), — единственный повод
    /// показать плашку у неё это момент, когда её берут в руки. У собираемой книги
    /// плашка выезжает при опускании, вместе с самим фактом «книга найдена».
    /// </summary>
    private bool ShowsPlateOnPick => !CollectOnDrop && BookCollectedUI.instance != null;

    /// <summary>Имя модели внутри — оно же идентификатор книги.</summary>
    public static string ResolveId(Transform bookRoot) {
        for (int i = 0; i < bookRoot.childCount; i++) {
            Transform child = bookRoot.GetChild(i);
            if (child.GetComponent<MeshRenderer>() != null) {
                return child.name;
            }
        }

        return bookRoot.childCount > 0 ? bookRoot.GetChild(0).name : bookRoot.name;
    }

    private void Awake() {
        if (string.IsNullOrEmpty(Id)) {
            Id = ResolveId(transform);
        }

        _pickable = GetComponent<Pickable>();
        // Подписываемся один раз на всю жизнь объекта: отписка в OnDisable
        // ломала бы себя же — книга выключается прямо из обработчика OnDrop.
        _pickable.OnPick.AddListener(HandlePick);
        _pickable.OnDrop.AddListener(HandleDrop);
    }

    private void HandlePick() {
        if (CanCollect && MadnessManager.instance != null) {
            MadnessManager.instance.SetBookPause(true);
        }

        // Мир глохнет, пока игрок читает (см. ReadingMuffle). Книга со стола из
        // туториала сюда не попадает — и не должна: там задача как раз в том,
        // чтобы на слух поймать музыкальную волну на радио.
        ReadingMuffle.SetReading(_pickable, true);

        if (ShowsPlateOnPick) {
            // isNew = false: в меню ничего не находится заново, плашка просто
            // напоминает, сколько книг собрано.
            BookCollectedUI.instance.Show(false);
        }
    }

    private void HandleDrop() {
        if (MadnessManager.instance != null) {
            MadnessManager.instance.SetBookPause(false);
        }

        ReadingMuffle.SetReading(_pickable, false);

        if (!CanCollect) {
            return;
        }

        BookCollectedUI.instance.Show(BookCollection.Add(Id));
        gameObject.SetActive(false);
    }
}
