using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerAudioManager : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip jump, land, slide, hurt, powerup;
    public AudioClip footstep1, footstep2;

    [Header("Cooldowns (anti-spam)")]
    public float landCooldown = 0.20f;
    public float slideCooldown = 0.25f;

    [Header("Pitch Randomization (min..max)")]
    public Vector2 jumpPitch = new Vector2(0.98f, 1.02f);
    public Vector2 landPitch = new Vector2(0.95f, 1.05f);
    public Vector2 slidePitch = new Vector2(0.95f, 1.05f);
    public Vector2 hurtPitch = new Vector2(0.97f, 1.03f);
    public Vector2 powerupPitch = new Vector2(0.98f, 1.04f);
    public Vector2 footstepPitch = new Vector2(0.95f, 1.05f);

    [Header("Volumes")]
    [Range(0f, 1.5f)] public float sfxVolume = 1.0f;   // global
    [Range(0f, 1.5f)] public float jumpVol = 1.0f;
    [Range(0f, 1.5f)] public float landVol = 0.6f;     // turn these down here
    [Range(0f, 1.5f)] public float slideVol = 0.5f;    //  ^^^
    [Range(0f, 1.5f)] public float hurtVol = 1.0f;
    [Range(0f, 1.5f)] public float powerupVol = 1.0f;
    [Range(0f, 1.5f)] public float footstepVol = 0.9f;

    AudioSource oneShot;
    float lastLandTime = -999f;
    float lastSlideTime = -999f;

    void Awake()
    {
        oneShot = GetComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.loop = false;
        oneShot.spatialBlend = 0f; // 2D
    }

    // === Public API ===
    public void PlayJump() => Play(jump, jumpPitch, jumpVol);
    public void PlayHurt() => Play(hurt, hurtPitch, hurtVol);
    public void PlayPowerup() => Play(powerup, powerupPitch, powerupVol);

    public void PlayLand()
    {
        if (Time.time - lastLandTime >= landCooldown)
        {
            Play(land, landPitch, landVol);
            lastLandTime = Time.time;
        }
    }

    public void PlaySlide()
    {
        if (Time.time - lastSlideTime >= slideCooldown)
        {
            Play(slide, slidePitch, slideVol);
            lastSlideTime = Time.time;
        }
    }

    // Call from Run animation events (optional)
    public void PlayFootstep()
    {
        AudioClip step = (Random.value < 0.5f ? footstep1 : footstep2);
        if (step) Play(step, footstepPitch, footstepVol);
    }

    // === Internal ===
    void Play(AudioClip clip, Vector2 pitchRange, float perClipVol)
    {
        if (!clip || !oneShot) return;
        float old = oneShot.pitch;
        oneShot.pitch = Random.Range(pitchRange.x, pitchRange.y);
        oneShot.PlayOneShot(clip, perClipVol * sfxVolume);
        oneShot.pitch = old;
    }
}
