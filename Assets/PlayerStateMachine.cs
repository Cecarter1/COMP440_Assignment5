using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState { Idle, Run, Jump, Fall, WallSlide }
    public PlayerState State { get; private set; } = PlayerState.Idle;

    [Header("Animator (optional)")]
    public Animator animator;
    static readonly int AnimState = Animator.StringToHash("State");

    public void Tick(bool grounded, bool wallSliding, float vAlongUp, float speedAlongRight)
    {
        var prev = State;

        switch (State)
        {
            case PlayerState.Idle:
                if (!grounded) State = vAlongUp > 0f ? PlayerState.Jump : PlayerState.Fall;
                else if (speedAlongRight > 0.05f) State = PlayerState.Run;
                break;

            case PlayerState.Run:
                if (!grounded) State = vAlongUp > 0f ? PlayerState.Jump : PlayerState.Fall;
                else if (speedAlongRight <= 0.05f) State = PlayerState.Idle;
                break;

            case PlayerState.Jump:
                if (wallSliding) State = PlayerState.WallSlide;
                else if (vAlongUp <= 0f) State = PlayerState.Fall;
                break;

            case PlayerState.Fall:
                if (wallSliding) State = PlayerState.WallSlide;
                else if (grounded) State = speedAlongRight > 0.05f ? PlayerState.Run : PlayerState.Idle;
                break;

            case PlayerState.WallSlide:
                if (grounded) State = speedAlongRight > 0.05f ? PlayerState.Run : PlayerState.Idle;
                else if (!wallSliding) State = vAlongUp > 0f ? PlayerState.Jump : PlayerState.Fall;
                break;
        }

        if (prev != State && animator) animator.SetInteger(AnimState, (int)State);
    }
}
