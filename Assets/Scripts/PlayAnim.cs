using UnityEngine;

public class PlayAnim : MonoBehaviour {
    public Animation anim;
    public AnimationClip clip;

    public void Play() {
        anim.Play(clip.name);
    }
}
