using UnityEngine;

public class CardLandAudio : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Play(AudioClip clip)
    {
        var source = GetComponent<AudioSource>();
        source.clip = clip;
        source.Play();
        Destroy(gameObject, clip.length + 0.1f);
    }
}
