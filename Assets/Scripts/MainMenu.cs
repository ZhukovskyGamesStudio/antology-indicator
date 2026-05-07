using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour {
    void Awake() {
        const string buildVersionObjectName = "BuildVersionText";
        if (transform.Find(buildVersionObjectName) != null) {
            return;
        }

        GameObject buildVersionObject = new GameObject(buildVersionObjectName);
        buildVersionObject.transform.SetParent(transform, false);
        buildVersionObject.transform.SetAsLastSibling();

        RectTransform rectTransform = buildVersionObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(1f, 0f);
        rectTransform.anchoredPosition = new Vector2(-24f, 24f);
        rectTransform.sizeDelta = new Vector2(480f, 40f);

        Text buildVersionText = buildVersionObject.AddComponent<Text>();
        buildVersionText.text = "Build " + Application.version;
        buildVersionText.fontSize = 22;
        buildVersionText.alignment = TextAnchor.LowerRight;
        buildVersionText.color = new Color(1f, 1f, 1f, 0.85f);
        buildVersionText.raycastTarget = false;

        buildVersionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public void Play() {
        SceneManager.LoadScene(2);
    }

    public void Rate() {
        Application.OpenURL("https://grafenters.itch.io/zhukovsky-games");
    }

    public void Exit() {
        Application.Quit();
    }
}