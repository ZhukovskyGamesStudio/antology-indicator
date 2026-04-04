using TMPro;
using UnityEngine;

public class HintUI : MonoBehaviour {

    public TextMeshProUGUI tmp;
    
    public void ShowHint(string text) {
        tmp.text = text;
    }

    public void Hide() {
        tmp.text = "";
    }
    
}
