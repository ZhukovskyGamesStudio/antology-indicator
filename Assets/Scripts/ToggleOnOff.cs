using UnityEngine;

public class ToggleOnOff : MonoBehaviour {
    public GameObject target;

    public void Toggle() {
        target.SetActive(!target.activeSelf);
    }

    public void Set(bool isOn) {
        target.SetActive(isOn);
    }
}