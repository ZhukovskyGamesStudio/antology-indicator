using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StoryObjectsContainer : MonoBehaviour {
    public GameObject LampWire, FakeRadioWire;
    public GameObject KitchenWire;
    public ToggleOnOff Lamp;
    public ToggleEmission LampEmission;
    public InteractiveObj RadioChange, RadioOnOff;
    

    public InteractiveObj Watertap, LampInteractive, FridgeDoor, MicrowaveDoor;
    public GameObject KitchenWater;
    public PlayAnim microwaveAnim, FridgeAnim;
    public AudioSource fridgeOpen;

    public PlayAnim WardrobeAnim;
    public GameObject NormalRooms, LabirintRooms;
    public GameObject Radio, FirstRadioPoint, NextRadioPoint;
    public InteractiveObj[] SneezeObjects => FindObjectsByType<InteractiveObj>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(o=>o.CompareTag("Sneeze")).ToArray();

    public GameObject[] Puddles;
    public Pickable ChipOnTable, BookMoved, BookUnmoved;
    public InteractiveObj ApartmentsExit;
    public PlayAnim TitlesAnimation;

}