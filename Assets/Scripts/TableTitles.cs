using UnityEngine;

public class TableTitles : MonoBehaviour {
    public InteractiveObj lamp;

    //called by anim
    public void ToggleLight() {
        lamp.OnClick?.Invoke();
    }
}