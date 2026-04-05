
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

public class StoryObjectsContainer : MonoBehaviour {
    public GameObject LampWire,FakeRadioWire;
    public GameObject KitchenWire;
    public ToggleOnOff Lamp;
    public ToggleEmission LampEmission;
    public InteractiveObj RadioChange, RadioOnOff;
    
    public InteractiveObj Watertap,LampInteractive, FridgeDoor, MicrowaveDoor;
    public GameObject KitchenWater;
    public PlayAnim microwaveAnim, FridgeAnim;
    public AudioSource fridgeOpen;

    public GameObject NormalRooms, LabirintRooms;
    public GameObject Radio, NextRadioPoint;

}