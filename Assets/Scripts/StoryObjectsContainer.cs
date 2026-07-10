using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StoryObjectsContainer : MonoBehaviour {
    public GameObject LampWire, FakeRadioWire;
    public GameObject KitchenWire;
    public ToggleOnOff Lamp;
    public ToggleEmission LampEmission;

    [Tooltip("Свет комнаты (люстры), который приглушается вместе с лампой при отключении из розетки")]
    public Light[] RoomLights;

    [Tooltip("Во сколько раз приглушать свет комнаты при отключении (0.2 = до 20%)")]
    public float RoomLightDim = 0.2f;
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