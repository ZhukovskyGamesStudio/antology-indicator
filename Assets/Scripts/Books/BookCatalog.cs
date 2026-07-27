using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Справочник всех книг уровня: id → как эта книга выглядит.
/// Нужен главному меню, чтобы разложить на столе собранные книги,
/// не загружая игровую сцену.
///
/// Заполняется кнопкой <c>Tools → Книги → Пересобрать каталог</c>
/// (см. BookCatalogBuilder) — сканирует открытую GameScene.
/// </summary>
[CreateAssetMenu(fileName = "BookCatalog", menuName = "Antology/Book Catalog")]
public class BookCatalog : ScriptableObject {
    [Serializable]
    public class Entry {
        [Tooltip("Имя модели внутри BookGeneral: book_3, book_11 и т.п.")]
        public string Id;

        public Mesh Mesh;
        public Texture2D Ru;
        public Texture2D En;
    }

    [Tooltip("Префаб BookGeneral — из него делаются книги в меню")]
    public GameObject BookPrefab;

    public List<Entry> Books = new();

    public Entry Find(string id) {
        for (int i = 0; i < Books.Count; i++) {
            if (Books[i] != null && Books[i].Id == id) {
                return Books[i];
            }
        }

        return null;
    }
}
