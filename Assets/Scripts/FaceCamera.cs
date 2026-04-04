using System;
using UnityEngine;

public class FaceCamera : MonoBehaviour
{
   private void Update() {
      transform.forward = Camera.main.transform.forward;
      var rot = transform.rotation.eulerAngles;
      rot.z = 0;
      transform.rotation = Quaternion.Euler(rot);
      
   }
}
