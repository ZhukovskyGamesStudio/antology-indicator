using UnityEditor;
using UnityEngine;

/// <summary>
/// Физика поверхностей для материалов, ВСТРОЕННЫХ в FBX (вся мебель, книги, папки,
/// сантехника): такие материалы не редактируются в инспекторе, поэтому таблица
/// «поверхность → smoothness/metallic/спекуляр» живёт здесь и применяется
/// на каждом импорте модели. Обычные .mat-файлы проекта настроены напрямую и сюда
/// не попадают.
///
/// Карты нормалей здесь НЕ раздаются: пробовали генерировать их из albedo
/// (Create From Grayscale) — на плейлуке рисованные текстуры превращались
/// в «шагрень» и штукатурку, откатили. Тюнер правит только отражения.
///
/// Поменял правила — подними Version (иначе Unity не переимпортирует модели)
/// и запусти Tools/Материалы/Переимпортировать модели.
/// </summary>
public class MaterialImportTuner : AssetPostprocessor {
    private const uint Version = 5;

    public override uint GetVersion() {
        return Version;
    }

    private class Rule {
        public string PathContains; // фильтр по пути fbx (null — любой)
        public string Tex;          // точное имя основной текстуры
        public string TexPrefix;    // либо префикс имени текстуры
        public float Smooth;
        public float Metallic;
        public bool SpecularOff;
    }

    // Порядок важен: применяется ПЕРВОЕ подошедшее правило.
    private static readonly Rule[] Rules = {
        // ─── Бумага и картон: книги, папки досье, записка/газета/брошюра/книга стола.
        // Матовые и без спекуляра совсем — прямой блик от HeldItemLight в руках
        // на бумаге выглядел как глянцевая плёнка.
        new Rule { PathContains = "/Books/", Smooth = 0.05f, SpecularOff = true },
        new Rule { PathContains = "Models/Folders/", Smooth = 0.05f, SpecularOff = true },
        new Rule { PathContains = "TranslatableModels/", Smooth = 0.05f, SpecularOff = true },

        // ─── Обои, крашеные стены, потолок: побелка/бумага не бликует вовсе.
        new Rule { Tex = "wallpaper", Smooth = 0.03f, SpecularOff = true },
        new Rule { Tex = "wallpaper_color.002", Smooth = 0.03f, SpecularOff = true },
        new Rule { Tex = "BATHROOM_WALLS_texture", Smooth = 0.03f, SpecularOff = true },
        new Rule { TexPrefix = "room_", Smooth = 0.03f, SpecularOff = true },
        new Rule { Tex = "ceiling", Smooth = 0.03f, SpecularOff = true },

        // ─── Пол: паркет с лёгким сатином, плитка ванной глянцевее.
        new Rule { Tex = "floor", Smooth = 0.25f },
        new Rule { Tex = "floor_bathroom", Smooth = 0.45f },

        // ─── Корпусная мебель: крашеное дерево, лёгкая шелковистость вместо блеска.
        new Rule { Tex = "wardrobe main", Smooth = 0.18f },
        new Rule { Tex = "door_left", Smooth = 0.18f },
        new Rule { Tex = "door_right", Smooth = 0.18f },
        new Rule { Tex = "cupboard color", Smooth = 0.18f },
        new Rule { Tex = "cupboard_shelf", Smooth = 0.18f },
        new Rule { TexPrefix = "cupboard_shelf.002", Smooth = 0.18f },
        new Rule { Tex = "cupboard_new_color", Smooth = 0.18f },
        new Rule { Tex = "door.002", Smooth = 0.18f },
        new Rule { Tex = "kithcen_cupboard ", Smooth = 0.18f },
        new Rule { Tex = "pedestal_color", Smooth = 0.18f },
        new Rule { TexPrefix = "drawers", Smooth = 0.18f },
        new Rule { TexPrefix = "drawer_1", Smooth = 0.18f },
        new Rule { Tex = "wardrobe_new Base Color", Smooth = 0.18f },
        new Rule { TexPrefix = "wardrobe_new_door", Smooth = 0.18f },
        new Rule { Tex = "wardrobe_bottom", Smooth = 0.18f },

        // ─── Лакированная мебель: стол и табуретки держат блик настольной лампы.
        new Rule { Tex = "Table Color hand", Smooth = 0.32f },
        new Rule { Tex = "taburet_color", Smooth = 0.32f },
        new Rule { Tex = "spinka.001", Smooth = 0.32f },

        // ─── Двери: старая эмаль.
        new Rule { Tex = "door color", Smooth = 0.28f },
        new Rule { Tex = "front door", Smooth = 0.28f },

        // ─── Сантехника: керамика и эмаль обязаны бликовать.
        new Rule { TexPrefix = "toilet", Smooth = 0.55f },
        new Rule { Tex = "rakovina Base Color", Smooth = 0.55f },
        new Rule { Tex = "sink_base_color", Smooth = 0.55f },
        new Rule { Tex = "bath Base Color", Smooth = 0.5f },
        new Rule { Tex = "nojki Base Color", Smooth = 0.3f },

        // ─── Краны и металл. Metallic умеренный сознательно: отражений в сцене нет
        // (reflectionIntensity = 0), и полный металлик стал бы почти чёрным.
        new Rule { TexPrefix = "watertap", Smooth = 0.55f, Metallic = 0.35f },
        new Rule { Tex = "shower_leika Base Color", Smooth = 0.55f, Metallic = 0.35f },
        new Rule { Tex = "hose_shower_img", Smooth = 0.4f, Metallic = 0.2f },
        new Rule { Tex = "derzalka", Smooth = 0.4f, Metallic = 0.2f },
        new Rule { Tex = "truba Base Color", Smooth = 0.3f, Metallic = 0.3f },
        new Rule { Tex = "pipe", Smooth = 0.3f, Metallic = 0.3f },
        new Rule { Tex = "radiator Base Color", Smooth = 0.3f },

        // ─── Техника: эмаль корпусов, стекло дверц.
        new Rule { Tex = "washing Base Color", Smooth = 0.4f },
        new Rule { Tex = "washing_door Base Color", Smooth = 0.6f },
        new Rule { Tex = "texture stove", Smooth = 0.35f },
        new Rule { Tex = "fridge_main", Smooth = 0.4f },
        new Rule { Tex = "fridge_door", Smooth = 0.4f },
        new Rule { Tex = "microwave_main", Smooth = 0.3f },
        new Rule { Tex = "microwave_door", Smooth = 0.6f },
        new Rule { Tex = "fridge_polka", Smooth = 0.65f },

        // ─── Стекло и зеркало.
        new Rule { Tex = "stakan Base Color", Smooth = 0.7f },
        new Rule { Tex = "mirror Base Color", Smooth = 0.5f },

        // ─── Ткань: диван матовый.
        new Rule { Tex = "sofa_main Base Color", Smooth = 0.05f, SpecularOff = true },
        new Rule { Tex = "sofa_pillows", Smooth = 0.05f, SpecularOff = true },

        // ─── Мелочь.
        new Rule { Tex = "socket Base Color", Smooth = 0.35f },
        new Rule { Tex = "wire_plug_color", Smooth = 0.2f },
        new Rule { Tex = "soapbox Base Color", Smooth = 0.3f },

        // ─── Дефолт для остальных встроенных (безтекстурные вставки, старые стены):
        // матовые, но со спекуляром — вместо фбх-шного 0.5.
        new Rule { Smooth = 0.15f },
    };

    private static bool IsTunedModel(string assetPath) {
        if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return assetPath.Contains("Assets/Models/")
            || assetPath.Contains("/Books/")
            || assetPath.Contains("Assets/Prefabs/Rooms/");
    }

    /// <summary>
    /// Хук именно на модель, а не OnPostprocessMaterial: пайплайн MaterialDescription
    /// (дефолт для FBX с 2019.3) материальный колбэк не зовёт.
    /// </summary>
    private void OnPostprocessModel(GameObject root) {
        string path = assetImporter.assetPath;
        if (!IsTunedModel(path)) {
            return;
        }

        var seen = new System.Collections.Generic.HashSet<Material>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) {
            foreach (Material m in r.sharedMaterials) {
                if (m == null || !seen.Add(m)) {
                    continue;
                }

                // Только ВСТРОЕННЫЕ материалы этого fbx. Часть моделей ремапит
                // слоты на внешние .mat (плафон люстры, картина за окном) —
                // во время импорта они видны в sharedMaterials как обычные
                // материалы, и без фильтра тюнер перезаписывал бы их значения,
                // выставленные напрямую.
                string matPath = AssetDatabase.GetAssetPath(m);
                if (!string.IsNullOrEmpty(matPath) && matPath != path) {
                    continue;
                }

                TuneMaterial(path, m);
            }
        }
    }

    private void TuneMaterial(string path, Material material) {
        Texture main = material.mainTexture;
        string texName = main != null ? main.name : null;

        foreach (Rule r in Rules) {
            if (r.PathContains != null && !path.Replace('\\', '/').Contains(r.PathContains)) {
                continue;
            }

            if (r.Tex != null && texName != r.Tex) {
                continue;
            }

            if (r.TexPrefix != null && (texName == null || !texName.StartsWith(r.TexPrefix))) {
                continue;
            }

            Apply(material, r);
            return;
        }
    }

    private void Apply(Material m, Rule r) {
        if (m.HasProperty("_Smoothness")) {
            m.SetFloat("_Smoothness", r.Smooth);
        }

        if (m.HasProperty("_Metallic")) {
            m.SetFloat("_Metallic", r.Metallic);
        }

        if (m.HasProperty("_SpecularHighlights")) {
            m.SetFloat("_SpecularHighlights", r.SpecularOff ? 0f : 1f);
            if (r.SpecularOff) {
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            } else {
                m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
        }
    }

    [MenuItem("Tools/Материалы/Переимпортировать модели")]
    public static void ReimportTunedModels() {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model")) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsTunedModel(path)) {
                continue;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            count++;
        }

        Debug.Log("MaterialImportTuner: переимпортировано моделей: " + count);
    }
}
