using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Пересобирает <see cref="BookCatalog"/> по книгам, расставленным в открытых сценах.
/// Каталог нужен главному меню, чтобы разложить собранные книги на столе.
///
/// Добавил на уровень новую книгу — открой GameScene и нажми
/// <c>Tools → Книги → Пересобрать каталог</c>.
/// </summary>
public static class BookCatalogBuilder {
    private const string MenuPath = "Tools/Книги/Пересобрать каталог";

    [MenuItem(MenuPath)]
    public static void Rebuild() {
        BookCatalog catalog = FindCatalog();
        if (catalog == null) {
            Debug.LogError("[BookCatalog] Не найден ни один ассет BookCatalog. Создай его: Create → Antology → Book Catalog");
            return;
        }

        Dictionary<string, BookCatalog.Entry> entries = new();
        GameObject prefab = null;

        for (int s = 0; s < EditorSceneManager.sceneCount; s++) {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(s);
            if (!scene.isLoaded) {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects()) {
                foreach (CollectableBook book in root.GetComponentsInChildren<CollectableBook>(true)) {
                    if (!book.CollectOnDrop) {
                        continue;
                    }

                    Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(book.gameObject);
                    if (source is GameObject sourceGo) {
                        prefab = sourceGo;
                    }

                    string id = string.IsNullOrEmpty(book.Id) ? CollectableBook.ResolveId(book.transform) : book.Id;
                    if (string.IsNullOrEmpty(id) || entries.ContainsKey(id)) {
                        continue;
                    }

                    entries[id] = BuildEntry(book, id);
                }
            }
        }

        if (entries.Count == 0) {
            Debug.LogWarning("[BookCatalog] В открытых сценах не нашлось ни одной книги — открой GameScene и повтори");
            return;
        }

        List<string> ids = new(entries.Keys);
        ids.Sort(CompareIds);

        catalog.Books.Clear();
        foreach (string id in ids) {
            catalog.Books.Add(entries[id]);
        }

        if (prefab != null) {
            catalog.BookPrefab = prefab;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BookCatalog] Собрано книг: {catalog.Books.Count} ({string.Join(", ", ids)})", catalog);
    }

    private static BookCatalog.Entry BuildEntry(CollectableBook book, string id) {
        BookCatalog.Entry entry = new() { Id = id };

        Transform model = book.transform.childCount > 0 ? book.transform.GetChild(0) : null;
        if (model != null) {
            MeshFilter filter = model.GetComponent<MeshFilter>();
            if (filter != null) {
                entry.Mesh = filter.sharedMesh;
            }
        }

        LocalizedTexture localized = book.GetComponent<LocalizedTexture>();
        if (localized != null) {
            SerializedObject so = new(localized);
            entry.Ru = so.FindProperty("ru").objectReferenceValue as Texture2D;
            entry.En = so.FindProperty("en").objectReferenceValue as Texture2D;
        }

        return entry;
    }

    /// <summary>book_2 должна идти перед book_10, а не после — сортируем по числу в имени.</summary>
    private static int CompareIds(string a, string b) {
        int na = NumberIn(a);
        int nb = NumberIn(b);
        return na != nb ? na.CompareTo(nb) : string.CompareOrdinal(a, b);
    }

    private static int NumberIn(string id) {
        int i = id.Length;
        while (i > 0 && char.IsDigit(id[i - 1])) {
            i--;
        }

        return i < id.Length && int.TryParse(id.Substring(i), out int value) ? value : int.MaxValue;
    }

    private static BookCatalog FindCatalog() {
        string[] guids = AssetDatabase.FindAssets("t:BookCatalog");
        if (guids.Length == 0) {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<BookCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
