using UnityEngine;

namespace ZhukovskyGamesPlugin{
    public class CustomMonoBehaviour : MonoBehaviour{
        
        public virtual int InitPriority => 0;
        public static void DestroyChildren(Transform parent){
            GameObject[] allChildren = new GameObject[parent.childCount];
            int i = 0;
            foreach (Transform child in parent){
                allChildren[i] = child.gameObject;
                i += 1;
            }

            foreach (GameObject obj in allChildren){
                Destroy(obj);
            }
        }
    }
}