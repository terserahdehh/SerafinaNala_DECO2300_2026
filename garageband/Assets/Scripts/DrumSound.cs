using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DrumSound : MonoBehaviour
{
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlaySound()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning(
                gameObject.name + " HAS NO AUDIO CLIP"
            );

            return;
        }

        audioSource.PlayOneShot(audioSource.clip);

        DrumRecorder.Instance?.RecordHit(audioSource.clip);

        Debug.Log(
            gameObject.name
            + " HIT: "
            + audioSource.clip.name
        );
    }
}