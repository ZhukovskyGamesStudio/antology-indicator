using System;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

public class PlayerPicker : MonoBehaviour
{
    public static PlayerPicker instance;

    public Transform pickedPos;
    
    private void Awake() {
        instance = this;
    }
}
