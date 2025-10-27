using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState { Idle, Run, Jump, Fall, WallSlide }
    public PlayerState State { get; private set; } = PlayerState.Idle;

    [Header("Animator (optional)")]
    public Animator animator;
    private static readonly int AnimState = Animator.StringToHash("State");

    [Header("Thresholds")]
    [Tooltip("Min horizontal speed (along Right) to be considered 'running'.")]
    public float runSpeedEpsilon = 0.06f;
    [Tooltip("Min vertical speed (along Up) to be considered 'rising' vs falling.")]
    public float upSpeedEpsilon = 0.05f;

    // Optional: expose a one-line event if you ever want hooks (SFX/VFX) on state changes
    public System.Action<PlayerState, PlayerState> OnStateChanged;

    /// <summary>
    /// Call once per frame (your controller already passes the correct values).
    /// grounded: true if on ground THIS frame
    /// wallSliding: true only if airborne, touching climbable, moving down, and pushing into wall
    /// vAlongUp: velocity projected onto Up (positive = rising, negative = falling)
    /// speedAlongRight: horizontal speed magnitude along Right (>= 0)
    /// </summary>
    public void Tick(bool grounded, bool wallSliding, float vAlongUp, float speedAlongRight)
    {
        var prev = State;

        // Small dead-zones to avoid flip-flopping around zero
        bool hasRunSpeed = speedAlongRight > runSpeedEpsilon;
        bool rising = vAlongUp > upSpeedEpsilon;
        bool falling = vAlongUp < -upSpeedEpsilon;

        // ***** RULE 1: Ground always wins over wall logic *****
        // If grounded, we must be Idle or Run, never WallSlide/Jump/Fall.
        if (grounded)
        {
            State = hasRunSpeed ? PlayerState.Run : PlayerState.Idle;
        }
        else
        {
            // Airborne
            if (wallSliding)
            {
                State = PlayerState.WallSlide;
            }
            else
            {
                // Free airborne motion
                if (rising) State = PlayerState.Jump;
                else if (falling) State = PlayerState.Fall;
                else
                {
                    // Perfect vertical zero (rare): prefer Fall so gravity blends correctly
                    State = PlayerState.Fall;
                }
            }
        }

        if (prev != State)
        {
            // Optional animator integer if you wire it up
            if (animator) animator.SetInteger(AnimState, (int)State);
            OnStateChanged?.Invoke(prev, State);
        }
    }
}
