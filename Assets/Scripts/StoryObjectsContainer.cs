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

    [Tooltip("Кухня в NormalRooms — гасим её целиком в финале (все источники шума/шёпота), не трогая гостиную с титрами")]
    public GameObject NormalKitchen;
    public GameObject Radio, FirstRadioPoint, NextRadioPoint;
    public InteractiveObj[] SneezeObjects => FindObjectsByType<InteractiveObj>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(o=>o.CompareTag("Sneeze")).ToArray();

    // Список луж здесь не нужен: Puddle ведёт свой статический реестр и
    // натекает по номеру Order — см. Puddle и StoryManager.SetPuddles.

    [Tooltip("Вода из кранов в ванных — та, что натекла лужами. Включается ровно тогда же, " +
             "когда появляются лужи; перекрывает её игрок сам (см. RunningWater)")]
    public RunningWater[] BathroomWaters;

    [Tooltip("Шторы, за которыми ванная. Сдвинуть их можно только с того момента, как появляются лужи")]
    public InteractiveObj[] BathroomCurtains;
    public Pickable ChipOnTable, BookMoved, BookUnmoved;
    public InteractiveObj ApartmentsExit;
    public PlayAnim TitlesAnimation;

}