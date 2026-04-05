using UnityEngine;

public class ToggleEmission : MonoBehaviour {
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public bool isOn = true;

    public void Toggle() {
        isOn = !isOn;
        GetComponent<MeshRenderer>().material.SetColor(EmissionColor, isOn ? Color.white : Color.black);
    }
    
    public void Set(bool arg) {
        isOn = arg;
        GetComponent<MeshRenderer>().material.SetColor(EmissionColor, arg ? Color.white : Color.black);
    }
}