
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

public class StoryObjectsContainer : MonoBehaviour {
    public GameObject LampWire,FakeRadioWire;
    public GameObject KitchenWire;
    public ToggleOnOff Lamp;
    
    
    public InteractiveObj SinkVentil,LampInteractive, FridgeDoor, MicrowaveDoor;
    public GameObject KitchenWater;
    public PlayAnim microwaveAnim, FridgeAnim;
    public AudioSource fridgeOpen;

    public GameObject NormalRooms, LabirintRooms;

}