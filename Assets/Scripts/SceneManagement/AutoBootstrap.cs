using UnityEngine;
using UnityEngine.SceneManagement;

//Автоматически переключает сцену на сцену 0
[DefaultExecutionOrder(-9999)]
public class AutoBootstrap : MonoBehaviour {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad() {
        if (SceneManager.GetActiveScene().name != SceneManager.GetSceneByBuildIndex(0).name) {
            SceneManager.LoadScene(0);
        }
    }
}