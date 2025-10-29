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

    [Header("Optional: Hook to State Machine (auto SFX on enter/exit)")]
    public PlayerStateMachine stateMachine;   // assign in Inspector (optional)
    public bool playSlideOnEnterState = true; // plays slide SFX when entering WallSlide

    private AudioSource oneShot;
    private float lastLandTime = -999f;
    private float lastSlideTime = -999f;

    private void Awake()
    {
        oneShot = GetComponent<AudioSource>();
        if (oneShot)
        {
            oneShot.playOnAwake = false;
            oneShot.loop = false;
            oneShot.spatialBlend = 0f; // 2D
        }
    }

    private void OnEnable()
    {
        // Optional: auto-wire to state machine if assigned and it exposes OnStateChanged
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged -= HandleStateChanged; // avoid double-subscribe
            stateMachine.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (stateMachine != null)
            stateMachine.OnStateChanged -= HandleStateChanged;
    }

    // === Public API (used by your PlayerController) ===
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
    private void Play(AudioClip clip, Vector2 pitchRange, float perClipVol)
    {
        if (!clip || !oneShot) return;
        float old = oneShot.pitch;
        oneShot.pitch = Random.Range(pitchRange.x, pitchRange.y);
        oneShot.PlayOneShot(clip, perClipVol * sfxVolume);
        oneShot.pitch = old;
    }

    // === Optional: react to state machine changes for convenience ===
    private void HandleStateChanged(PlayerStateMachine.PlayerState prev, PlayerStateMachine.PlayerState next)
    {
        if (!playSlideOnEnterState) return;

        // Fire a one-shot slide SFX when entering WallSlide
        if (next == PlayerStateMachine.PlayerState.WallSlide)
        {
            PlaySlide();
        }
        // You could add land SFX here if you weren't already playing it from controller.
        // We intentionally don't to avoid double-triggering with PlayerController.TryLandSfx().
    }
}
