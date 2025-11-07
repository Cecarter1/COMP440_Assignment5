using UnityEngine;

public class UISoundPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip clickClip;
    public AudioClip hoverClip; // optional hover sound

    public void PlayClick()
    {
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip);
    }

    public void PlayHover()
    {
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip);
    }
}

