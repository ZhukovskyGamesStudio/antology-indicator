using UnityEngine;

/// <summary>
/// Проигрывание случайного клипа из набора.
/// Если источник не задан — звук играется через <see cref="AudioSource.PlayClipAtPoint"/>
/// в указанной точке (удобно для разовых SFX без отдельного AudioSource на объекте).
/// </summary>
public static class SoundUtil {
    public static void PlayRandom(AudioSource source, AudioClip[] clips, Vector3 position, float volume = 1f) {
        if (clips == null || clips.Length == 0) {
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) {
            return;
        }

        if (source != null) {
            source.PlayOneShot(clip, volume);
        } else {
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
