using TMPro;
using UnityEngine;

public class WinPanel : MonoBehaviour {
    [SerializeField]
    private TextMeshProUGUI _endText;

    public void SetText(string text) {
        _endText.text = text;
    }
}